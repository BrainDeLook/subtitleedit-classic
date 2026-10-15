using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.Ocr
{
    public static class ClassicNOcrBitmap
    {
        public static List<ImageSplitterItem> Split(NikseBitmap bitmap,int spaces,bool rtl,bool topToBottom,int minHeight,bool autoHeight,int lineHeight=-1)
        {
            var list=NikseBitmapImageSplitter.SplitBitmapToLettersNew(bitmap,spaces,rtl,topToBottom,minHeight,autoHeight,lineHeight);
            while(list.Count>0 && list[0].NikseBitmap==null && string.IsNullOrWhiteSpace(list[0].SpecialCharacter))list.RemoveAt(0);
            while(list.Count>0 && list[list.Count-1].NikseBitmap==null && string.IsNullOrWhiteSpace(list[list.Count-1].SpecialCharacter))list.RemoveAt(list.Count-1);
            SetBaselines(list);return list;
        }
        // Classic uses System.Drawing; the stable matcher only needs the alpha plane.
        public static byte[] GetPixelData(this NikseBitmap bitmap) {
            var data=new byte[bitmap.Width*bitmap.Height*4];
            for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++) data[(y*bitmap.Width+x)*4+3]=(byte)bitmap.GetAlpha(x,y);
            return data;
        }
        public static NikseBitmap UnItalic(this NikseBitmap bitmap,double factor) {
            var w=bitmap.Width+(int)(bitmap.Height*factor)+4;
            var data=new byte[w*bitmap.Height*4];
            for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++) {
                var a=(x+(int)Math.Round(y*factor,MidpointRounding.AwayFromZero)+y*w)*4;
                var p=bitmap.GetPixelColors(x,y);Buffer.BlockCopy(p,0,data,a,4);
            }
            var result=new NikseBitmap(w,bitmap.Height,data);result.CropSidesAndBottom(0,Color.FromArgb(0,0,0,0),false);return result;
        }
        private sealed class Metrics {public int Above;}
        private static readonly ConditionalWeakTable<ImageSplitterItem,Metrics> Layout=new ConditionalWeakTable<ImageSplitterItem,Metrics>();
        public static int HeightAboveBaseline(ImageSplitterItem item) => Layout.TryGetValue(item,out var m) ? m.Above : item.NikseBitmap?.Height ?? 0;
        public static void SetBaselines(List<ImageSplitterItem> items) {
            var line=new List<ImageSplitterItem>();
            Action flush=()=>{var bottoms=line.Where(x=>x.NikseBitmap!=null).Select(x=>x.Top+x.NikseBitmap.Height).OrderBy(x=>x).ToList();if(bottoms.Count>0){var baseline=bottoms[(bottoms.Count-1)/2];foreach(var x in line){Layout.Remove(x);Layout.Add(x,new Metrics{Above=Math.Min(x.NikseBitmap?.Height ?? 0,Math.Max(0,baseline-x.Top))});}}line.Clear();};
            foreach(var item in items){if(item.SpecialCharacter==Environment.NewLine || item.SpecialCharacter=="\n")flush();else line.Add(item);}flush();
        }
        public static int SpacePixels(List<ImageSplitterItem> items, ImageSplitterItem item) {
            var i=items.IndexOf(item);if(i<=0 || i+1>=items.Count)return 0;
            var a=items[i-1];var b=items[i+1];return a.NikseBitmap==null || b.NikseBitmap==null ? 0 : Math.Max(0,b.X-a.X-a.NikseBitmap.Width);
        }
    }
}
