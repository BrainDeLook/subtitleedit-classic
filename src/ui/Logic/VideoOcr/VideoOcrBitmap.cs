using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Nikse.SubtitleEdit.Logic.VideoOcr
{
    // GDI+ adapter for the Skia bitmap operations in the stable SE5 grouping code.
    internal sealed class VideoOcrBitmap : IDisposable
    {
        private readonly Bitmap _bitmap;
        private VideoOcrBitmap(Bitmap bitmap) { _bitmap=bitmap; }
        public int Width => _bitmap.Width;
        public int Height => _bitmap.Height;
        public static VideoOcrBitmap Decode(string path)
        {
            using(var image=Image.FromFile(path))
            {
                var bitmap=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);
                using(var graphics=Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(image,0,0);
                return new VideoOcrBitmap(bitmap);
            }
        }
        public VideoOcrBitmap Resize(int width,int height)
        {
            var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb);
            using(var graphics=Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode=InterpolationMode.Bilinear;
                graphics.PixelOffsetMode=PixelOffsetMode.Half;
                graphics.DrawImage(_bitmap,new Rectangle(0,0,width,height),0,0,Width,Height,GraphicsUnit.Pixel);
            }
            return new VideoOcrBitmap(bitmap);
        }
        public unsafe Color[] Pixels
        {
            get
            {
                var result=new Color[Width*Height];
                var bits=_bitmap.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                try { for(int y=0;y<Height;y++) { var row=(int*)((byte*)bits.Scan0+y*bits.Stride); for(int x=0;x<Width;x++) result[y*Width+x]=Color.FromArgb(row[x]); } }
                finally { _bitmap.UnlockBits(bits); }
                return result;
            }
            set
            {
                if(value.Length!=Width*Height) throw new ArgumentException("Pixel buffer size mismatch");
                var bits=_bitmap.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                try { for(int y=0;y<Height;y++) { var row=(int*)((byte*)bits.Scan0+y*bits.Stride); for(int x=0;x<Width;x++) row[x]=value[y*Width+x].ToArgb(); } }
                finally { _bitmap.UnlockBits(bits); }
            }
        }
        public void SaveJpeg(string path)
        {
            var codec=Array.Find(ImageCodecInfo.GetImageEncoders(),c=>c.MimeType=="image/jpeg");
            using(var parameters=new EncoderParameters(1))
            {
                parameters.Param[0]=new EncoderParameter(Encoder.Quality,92L);
                _bitmap.Save(path,codec,parameters);
            }
        }
        public void Dispose() { _bitmap.Dispose(); }
    }
    internal static class VideoOcrCompat
    {
        public static double Clamp(double value,double minimum,double maximum) => Math.Max(minimum,Math.Min(maximum,value));
        public static int Clamp(int value,int minimum,int maximum) => Math.Max(minimum,Math.Min(maximum,value));
    }
}
