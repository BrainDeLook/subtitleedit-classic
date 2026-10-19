using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace Nikse.SubtitleEdit.Core.BluRaySup
{public enum BluRayContentAlignment {TopLeft,TopCenter,TopRight,MiddleLeft,MiddleCenter,MiddleRight,BottomLeft,BottomCenter,BottomRight}}
namespace Nikse.SubtitleEdit.Logic.ClassicImageExport
{
    internal static class ExportMath
    {
        public const float PI=(float)Math.PI;
        public static float Sqrt(float value)=>(float)Math.Sqrt(value);
        public static float Sin(float value)=>(float)Math.Sin(value);
        public static float Cos(float value)=>(float)Math.Cos(value);
        public static T Clamp<T>(T v,T min,T max) where T:IComparable<T> => v.CompareTo(min)<0?min:v.CompareTo(max)>0?max:v;
    }
    internal readonly struct ClassicRune
    {
        public int Value {get;}
        public int Utf16SequenceLength => Value > 0xffff ? 2 : 1;
        public ClassicRune(int value){Value=value;}
        public override string ToString()=>char.ConvertFromUtf32(Value);
        public static UnicodeCategory GetUnicodeCategory(ClassicRune rune)=>CharUnicodeInfo.GetUnicodeCategory(rune.ToString(),0);
        public static bool IsWhiteSpace(ClassicRune rune)=>char.IsWhiteSpace(rune.ToString(),0);
        public static bool IsLetter(ClassicRune rune)=>char.IsLetter(rune.ToString(),0);
        public static bool IsLetterOrDigit(ClassicRune rune)=>char.IsLetterOrDigit(rune.ToString(),0);
        public static bool IsControl(ClassicRune rune)=>char.IsControl(rune.ToString(),0);
    }
    internal static class ClassicImagePlatform
    {
        public static IEnumerable<ClassicRune> EnumerateClassicRunes(this string text)
        {for(int i=0;i<text.Length;i++){if(char.IsHighSurrogate(text[i]) && i+1<text.Length && char.IsLowSurrogate(text[i+1])){yield return new ClassicRune(char.ConvertToUtf32(text,i));i++;}else yield return new ClassicRune(char.IsSurrogate(text[i])?0xfffd:text[i]);}}
        public static SKBitmap CropTo(this SKBitmap bitmap,int left,int top,int right,int bottom)
        {var result=new SKBitmap(Math.Max(1,right-left+1),Math.Max(1,bottom-top+1),bitmap.ColorType,bitmap.AlphaType);using(var canvas=new SKCanvas(result)){canvas.Clear(SKColors.Transparent);canvas.DrawBitmap(bitmap,-left,-top);}return result;}
        public static byte[] ToPngArray(this SKBitmap bitmap){using(var image=SKImage.FromBitmap(bitmap))using(var data=image.Encode(SKEncodedImageFormat.Png,100))return data.ToArray();}
        public static byte[] ToPng8BitArray(this SKBitmap bitmap)=>Png8BitEncoder.Encode(bitmap);
        public static bool StartsWith(this string text,char value)=>text.Length>0 && text[0]==value;
        public static bool EndsWith(this string text,char value)=>text.Length>0 && text[text.Length-1]==value;
        public static bool Contains(this string text,string value,StringComparison comparison)=>text.IndexOf(value,comparison)>=0;
    }
}

