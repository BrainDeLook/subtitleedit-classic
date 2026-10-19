using SkiaSharp;

namespace Nikse.SubtitleEdit.Logic.ClassicImageExport
{
    internal static class SkiaExt
    {
        internal struct NonTransparentBounds
        {
            public int Top, Left, Right, Bottom;
            public bool IsEmpty => Top > Bottom || Left > Right;
        }

        public static unsafe NonTransparentBounds GetNonTransparentBounds(this SKBitmap bitmap)
        {
            var bounds = new NonTransparentBounds { Top = bitmap.Height, Left = bitmap.Width, Right = -1, Bottom = -1 };
            if((bitmap.ColorType == SKColorType.Bgra8888 || bitmap.ColorType == SKColorType.Rgba8888) && bitmap.GetPixels()!=System.IntPtr.Zero)
            {
                var pixels=(byte*)bitmap.GetPixels().ToPointer();
                for(var y=0;y<bitmap.Height;y++)
                {
                    var row=pixels+y*bitmap.RowBytes;var first=0;while(first<bitmap.Width && row[first*4+3]==0)first++;
                    if(first==bitmap.Width)continue;var last=bitmap.Width-1;while(last>first && row[last*4+3]==0)last--;
                    bounds.Top=System.Math.Min(bounds.Top,y);bounds.Bottom=y;bounds.Left=System.Math.Min(bounds.Left,first);bounds.Right=System.Math.Max(bounds.Right,last);
                }
                return bounds;
            }
            // GetPixel also handles opaque, indexed and packed pixel formats correctly.
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y).Alpha != 0)
                    {
                        bounds.Top = System.Math.Min(bounds.Top, y);
                        bounds.Left = System.Math.Min(bounds.Left, x);
                        bounds.Right = System.Math.Max(bounds.Right, x);
                        bounds.Bottom = y;
                    }
            return bounds;
        }

        public static SKBitmap AddTransparentMargins(this SKBitmap bitmap, int left, int top, int right, int bottom)
        {
            var result = new SKBitmap(bitmap.Width + left + right, bitmap.Height + top + bottom, bitmap.ColorType, bitmap.AlphaType);
            using (var canvas = new SKCanvas(result))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bitmap, left, top);
            }
            return result;
        }
    }
}
