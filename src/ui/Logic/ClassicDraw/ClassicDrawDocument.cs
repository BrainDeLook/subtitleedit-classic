using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.ClassicDraw
{
    public static class ClassicDrawDocument
    {
        public static List<DrawShape> Read(IEnumerable<Paragraph> lines)
        {
            var shapes = new List<DrawShape>();
            foreach (var cue in lines)
            {
                var text = cue.Text;
                var animated = DrawAnimation.TryParseMarker(text, out var animation, out var anchorX, out var anchorY);
                var anchored = animated && DrawAnimation.IsAnchoredDrawingLine(text);
                if (animated) text = DrawAnimation.RemoveFromLine(text);
                var ids = cue.Effect != null && cue.Effect.StartsWith("SeGroups:", StringComparison.Ordinal)
                    ? cue.Effect.Substring(9).Split('|')[0].Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var id) ? id : 0).ToArray() : new int[0];
                if (AssaDrawTextLine.TryParse(text, out var parsed))
                {
                    var box = parsed.Box;
                    var host = shapes.Where(s => s.Layer == cue.Layer && !s.IsEraser && !s.HasText && s.HitTest((box.Left + box.Right)/2, (box.Top + box.Bottom)/2,0)).OrderBy(s => {var b=s.GetBounds();return (b.Right-b.Left)*(b.Bottom-b.Top);}).FirstOrDefault();
                    if (host == null)
                    {
                        host = new DrawShape { IsTextBox=true, Layer=cue.Layer };
                        host.AddPoint(DrawCoordinateType.Move,box.Left,box.Top,System.Drawing.Color.IndianRed);
                        host.AddPoint(DrawCoordinateType.Line,box.Right,box.Top,System.Drawing.Color.IndianRed);
                        host.AddPoint(DrawCoordinateType.Line,box.Right,box.Bottom,System.Drawing.Color.IndianRed);
                        host.AddPoint(DrawCoordinateType.Line,box.Left,box.Bottom,System.Drawing.Color.IndianRed);
                        shapes.Add(host);
                    }
                    host.Text=parsed.Text; host.FontName=parsed.FontName;host.FontSize=parsed.FontSize;host.FontBold=parsed.Bold;
                    host.TextColor=parsed.Color ?? DrawColors.White;host.ShrinkToFit=parsed.ShrinkToFit;
                    host.OutlineWidth=parsed.OutlineWidth;host.OutlineColor=parsed.OutlineColor;
                    host.TextFrame=new DrawTextFrame((box.Left+box.Right)/2,(box.Top+box.Bottom)/2,box.Right-box.Left,box.Bottom-box.Top,parsed.Angle);
                    host.Hidden=cue.IsComment;
                    if (ids.Length>0) host.GroupId=ids[0];
                    host.Animation=animated ? animation : host.Animation;
                    continue;
                }
                var color=DrawColors.White;
                var colorMatch=Regex.Match(cue.Text,@"\\(?:1c|c)&H([0-9a-fA-F]{6})&");
                if(colorMatch.Success){var value=int.Parse(colorMatch.Groups[1].Value,NumberStyles.HexNumber,CultureInfo.InvariantCulture);color=DrawColor.FromArgb(255,(byte)value,(byte)(value>>8),(byte)(value>>16));}
                var alphaMatch=Regex.Match(cue.Text,@"\\(?:1a|alpha)&H([0-9a-fA-F]{2})&");
                if(alphaMatch.Success)color=DrawColor.FromArgb((byte)(255-int.Parse(alphaMatch.Groups[1].Value,NumberStyles.HexNumber,CultureInfo.InvariantCulture)),color.R,color.G,color.B);
                var clip=Regex.Match(cue.Text,@"\\iclip\(([^)]*)\)");
                if(clip.Success && !shapes.Any(s=>s.Layer==cue.Layer && s.IsEraser)) shapes.AddRange(DrawShape.ParseAssa(clip.Groups[1].Value,cue.Layer,color,true));
                if(!Regex.IsMatch(text,@"\\p[1-9]"))continue;
                var parts=DrawShape.ParseAssa(Regex.Replace(text,@"\{[^}]*\}",""),cue.Layer,color,false);
                AssaDrawTextLine.ParseOutline(cue.Text,out var outline,out var outlineColor);
                for(var i=0;i<parts.Count;i++)
                {
                    var shape=parts[i];shape.GroupId=i<ids.Length?ids[i]:0;shape.Animation=animated?animation.Clone():null;
                    shape.Hidden=cue.IsComment;shape.OutlineWidth=outline;shape.OutlineColor=outlineColor;
                    if(anchored)shape.Offset(anchorX,anchorY);
                }
                shapes.AddRange(parts);
            }
            return shapes;
        }
        public static List<Paragraph> Write(IEnumerable<DrawShape> source,Paragraph cue,int width,int height,bool includeHidden=false)
        {
            var shapes=source.Where(s=>(includeHidden || !s.Hidden) && s.Points.Count>0).ToList();
            var result=new List<Paragraph>();var duration=(int)Math.Max(1,cue.Duration.TotalMilliseconds);
            foreach(var layer in shapes.GroupBy(s=>s.Layer).OrderBy(g=>g.Key))
            {
                var animation=layer.Select(s=>s.Animation).FirstOrDefault(a=>a!=null && !a.IsNone);
                foreach(var shape in layer.Where(s=>!s.IsEraser))
                {
                    var members=shape.GroupId>0?shapes.Where(s=>s.GroupId==shape.GroupId && !s.IsEraser):layer.Where(s=>!s.IsEraser);
                    var bounds=DrawShape.GetBounds(members);var x=(bounds.Left+bounds.Right)/2;var y=(bounds.Top+bounds.Bottom)/2;
                    var marker=animation?.FormatMarker(x,y) ?? "";
                    string text;
                    if(!shape.IsTextBox)
                    {
                        var origin=animation?.GetOrigin(x,y,bounds,width,height);
                        var tags=new StringBuilder("{\\an7");
                        tags.Append(animation==null?"\\pos(0,0)":animation.GetPositionTag(x,y,duration,bounds,width,height));
                        if(origin.HasValue && animation.OriginKind!=DrawAnimationOrigin.Anchor)tags.Append("\\org(").Append(N(origin.Value.X)).Append(',').Append(N(origin.Value.Y)).Append(')');
                        tags.Append(animation?.GetEffectTags(duration,0,bounds,width,height));tags.Append("\\shad0\\1c").Append(AssaDrawTextLine.ToAssaColor(shape.ForeColor)).Append("\\1a").Append(AssaDrawTextLine.ToAssaAlpha(shape.ForeColor));
                        AssaDrawTextLine.AppendOutline(tags,shape.OutlineWidth,shape.OutlineColor);
                        var erase=string.Join(" ",layer.Where(s=>s.IsEraser).Select(s=>s.ToAssa()));
                        if(erase.Length>0)tags.Append("\\iclip(").Append(erase).Append(')');
                        tags.Append('}').Append(marker).Append("{\\p1}").Append(animation==null?shape.ToAssa():shape.ToAssa(x,y)).Append("{\\p0}");
                        text=tags.ToString();
                        result.Add(new Paragraph(cue,true){Layer=shape.Layer,IsComment=shape.Hidden,Extra="ClassicDraw",Text=text,Effect="SeGroups:"+string.Join(" ",Enumerable.Repeat(shape.GroupId,Math.Max(1,shape.Points.Count(p=>p.DrawType==DrawCoordinateType.Move))))});
                    }
                    if(shape.HasText)
                    {
                        text=AssaDrawTextLine.Format(shape);
                        if(animation!=null)
                        {
                            var frame=shape.GetTextFrame();var baseAngle=(float)Math.Round(-frame.Angle,2);
                            var origin=animation.GetOrigin(x,y,bounds,width,height) ?? (x,y);
                            var pos=animation.GetPositionTag(frame.CenterX,frame.CenterY,duration,bounds,width,height,(px,py)=>DrawAnimation.MapAroundOrigin(px,py,origin.Item1,origin.Item2,baseAngle));
                            text=Regex.Replace(text,@"\\pos\([^)]*\)",m=>pos);
                            var end=text.IndexOf('}')+1;
                            text=text.Insert(end,marker+"{\\org("+N(origin.Item1)+","+N(origin.Item2)+")"+animation.GetEffectTags(duration,baseAngle,bounds,width,height)+"}");
                        }
                        result.Add(new Paragraph(cue,true){Layer=shape.Layer,IsComment=shape.Hidden,Extra="ClassicDrawText",Text=text,Effect="SeGroups:"+shape.GroupId});
                    }
                }
            }
            return result;
        }
        private static string N(double value)=>Math.Abs(value)<.005?"0":value.ToString("0.##",CultureInfo.InvariantCulture);
    }
}
