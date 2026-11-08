using System.Drawing;
using System.IO;
using SkiaSharp;
namespace Classic54Tests { static class SkiaFixture { public static SKBitmap ToSkiaFixture(this Bitmap bitmap) {using(bitmap)using(var stream=new MemoryStream()){bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);return SKBitmap.Decode(stream.ToArray());}} } }
