using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.SpellCheck;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicGridSpellHit
    {
        public Rectangle Bounds { get; internal set; }
        public string Word { get; internal set; }
        public string Source { get; internal set; }
        public List<Tuple<int,int>> SourceRanges { get; internal set; }
        public string Replace(string replacement)
        {
            if(SourceRanges==null || SourceRanges.Count==0) return Source;
            var result=Source;
            for(var i=SourceRanges.Count-1;i>=0;i--)result=result.Remove(SourceRanges[i].Item1,SourceRanges[i].Item2-SourceRanges[i].Item1);
            return result.Insert(SourceRanges[0].Item1,replacement);
        }
    }

    public sealed partial class ClassicGridPainter
    {
        public static List<Tuple<int,int>> MapWord(List<ClassicGridRun> runs,int start,int length)
        {
            var ranges=new List<Tuple<int,int>>();var pos=0;
            foreach(var run in runs)
            {
                for(var i=Math.Max(0,start-pos);i<run.Text.Length && pos+i<start+length;i++)
                {
                    if(run.Tag || run.SourceStarts==null || i>=run.SourceStarts.Length || run.SourceStarts[i]<0)continue;
                    var a=run.SourceStarts[i];var b=run.SourceEnds[i];
                    if(ranges.Count>0 && a<=ranges[ranges.Count-1].Item2)
                        ranges[ranges.Count-1]=Tuple.Create(ranges[ranges.Count-1].Item1,Math.Max(b,ranges[ranges.Count-1].Item2));
                    else ranges.Add(Tuple.Create(a,b));
                }
                pos+=run.Text.Length;
            }
            return ranges;
        }

        private void DrawSpelling(Graphics graphics,Rectangle bounds,string source,Font font,bool selected,bool rtl,
            List<ClassicGridRun> runs,Func<string,List<SpellCheckWord>> check,Action<ClassicGridSpellHit> hit)
        {
            var display=new StringBuilder();
            foreach(var run in runs)display.Append(run.Tag?new string(' ',run.Text.Length):run.Text);
            var wrong=check(display.ToString());if(wrong.Count==0)return;
            var state=graphics.Save();graphics.SetClip(bounds,System.Drawing.Drawing2D.CombineMode.Intersect);
            var flags=TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.PreserveGraphicsClipping;
            if(rtl)flags|=TextFormatFlags.RightToLeft|TextFormatFlags.Right;
            var bearing=Math.Max(1,(int)Math.Ceiling(font.GetHeight(graphics)/6f));
            var x=rtl?bounds.Right-bearing:bounds.Left+bearing;var offset=0;
            try
            {
                using(var pen=new Pen(selected?Color.Yellow:Color.OrangeRed))
                foreach(var run in runs)
                {
                    Font styled;
                    var size=run.Size.HasValue?Math.Max(8,Math.Min(24,run.Size.Value*.8f)):font.Size;
                    try{styled=new Font(string.IsNullOrWhiteSpace(run.Face)?font.FontFamily.Name:run.Face,size,font.Style|run.Style,GraphicsUnit.Point);}
                    catch(ArgumentException){styled=new Font(font.FontFamily,font.Size,font.Style);}
                    using(styled)
                    {
                        Func<string,int> measure=t=>t.Length==0?0:TextRenderer.MeasureText(graphics,t,styled,new Size(int.MaxValue,int.MaxValue),flags).Width;
                        var width=measure(run.Text);var available=rtl?x-bounds.Left:bounds.Right-x;
                        if(available<=0)break;
                        foreach(var word in wrong)
                        {
                            var a=Math.Max(word.Index,offset);var b=Math.Min(word.Index+word.Length,offset+run.Text.Length);
                            if(run.Tag || b<=a)continue;
                            var prefix=measure(run.Text.Substring(0,a-offset));var end=measure(run.Text.Substring(0,b-offset));
                            var left=rtl?x-end:x+prefix;var right=rtl?x-prefix:x+end;
                            // Leave the ellipsis alone when the run is clipped.
                            if(width>available && (rtl?left<bounds.Left+12:right>bounds.Right-12))continue;
                            var rect=Rectangle.Intersect(bounds,new Rectangle(left,bounds.Top,Math.Max(1,right-left),bounds.Height));
                            if(rect.Width==0)continue;
                            var y=Math.Min(bounds.Bottom-2,bounds.Top+(int)Math.Ceiling(styled.GetHeight(graphics))-1);
                            for(var px=rect.Left;px<rect.Right-1;px+=4)
                            {graphics.DrawLine(pen,px,y,Math.Min(px+2,rect.Right-1),y-1);graphics.DrawLine(pen,Math.Min(px+2,rect.Right-1),y-1,Math.Min(px+4,rect.Right-1),y);}
                            hit?.Invoke(new ClassicGridSpellHit{Bounds=rect,Word=word.Text,Source=source,SourceRanges=MapWord(runs,word.Index,word.Length)});
                        }
                        offset+=run.Text.Length;x+=rtl?-width:width;
                    }
                }
            }
            finally{graphics.Restore(state);}
        }
    }
}
