using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using Nikse.SubtitleEdit.Core.BluRaySup;
namespace Nikse.SubtitleEdit.Logic
{
    // Keeps only the current overlap cluster in memory. Disjoint windows retain independent palettes.
    public sealed class ClassicSupCompositionWriter:IDisposable
    {
        private sealed class Entry {public long Start,End;public BluRaySupCompositionObject Object;}
        private readonly Stream _output;
        private readonly List<Entry> _pending=new List<Entry>();
        private readonly int _width,_height,_nits;private readonly double _fps;
        private int _composition;
        public ClassicSupCompositionWriter(Stream output,int width,int height,double fps,int nits){_output=output;_width=width;_height=height;_fps=fps;_nits=nits;}
        public void Add(long start,long end,SKBitmap bitmap,int x,int y,SKColor color,bool forced)
        {
            if(end<=start)throw new ArgumentException("SUP cue must have a positive duration.");
            if(_pending.Count>0 && start<_pending[_pending.Count-1].Start)throw new ArgumentException("SUP cues must be ordered by start time.");
            if(_pending.Count>0 && start>=_pending.Max(e=>e.End))Flush();
            _pending.Add(new Entry{Start=start,End=end,Object=new BluRaySupCompositionObject{Bitmap=bitmap.Copy(),X=x,Y=y,FontColor=color,IsForced=forced}});
        }
        public void Flush()
        {
            if(_pending.Count==0)return;
            var boundaries=_pending.SelectMany(e=>new[]{e.Start,e.End}).Distinct().OrderBy(t=>t).ToArray();
            try
            {
                for(var i=0;i<boundaries.Length-1;i++)
                {
                    var start=boundaries[i];var end=boundaries[i+1];
                    var objects=_pending.Where(e=>e.Start<=start && e.End>start).Select(e=>e.Object).ToList();
                    if(objects.Count==0)continue;
                    SKBitmap merged=null;
                    try
                    {
                        var overlap=objects.Count>2 || objects.Count==2 && Intersects(objects[0],objects[1]);
                        if(overlap)
                        {
                            var left=objects.Min(o=>o.X);var top=objects.Min(o=>o.Y);
                            var right=objects.Max(o=>o.X+o.Bitmap.Width);var bottom=objects.Max(o=>o.Y+o.Bitmap.Height);
                            merged=new SKBitmap(right-left,bottom-top);
                            using(var canvas=new SKCanvas(merged)){canvas.Clear(SKColors.Transparent);foreach(var o in objects)canvas.DrawBitmap(o.Bitmap,o.X-left,o.Y-top);}
                            objects=new List<BluRaySupCompositionObject>{new BluRaySupCompositionObject{Bitmap=merged,X=left,Y=top,FontColor=objects[0].FontColor,IsForced=objects.Any(o=>o.IsForced)}};
                        }
                        var pic=new BluRaySupPicture{StartTime=start,EndTime=end,Width=_width,Height=_height,HdrWhiteNits=_nits,CompositionNumber=_composition};
                        var bytes=BluRaySupPicture.CreateSupFrame(pic,objects,_fps,writeClear:i==boundaries.Length-2);
                        _output.Write(bytes,0,bytes.Length);_composition=pic.NextCompositionNumber;
                    }
                    finally{merged?.Dispose();}
                }
            }
            finally{Dispose();}
        }
        private static bool Intersects(BluRaySupCompositionObject a,BluRaySupCompositionObject b)=>a.X<b.X+b.Bitmap.Width && b.X<a.X+a.Bitmap.Width && a.Y<b.Y+b.Bitmap.Height && b.Y<a.Y+a.Bitmap.Height;
        public void Dispose(){foreach(var e in _pending)e.Object.Bitmap.Dispose();_pending.Clear();}
    }
}
