using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.ClassicDraw
{
    public readonly struct DrawColor
    {
        public byte A {get;} public byte R {get;} public byte G {get;} public byte B {get;}
        private DrawColor(byte a,byte r,byte g,byte b){A=a;R=r;G=g;B=b;}
        public static DrawColor FromArgb(byte a,byte r,byte g,byte b)=>new DrawColor(a,r,g,b);
        public static bool TryParse(string text,out DrawColor color)
        {
            try
            {
                if(text.StartsWith("#") && text.Length==9){var bits=uint.Parse(text.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);color=new DrawColor((byte)(bits>>24),(byte)(bits>>16),(byte)(bits>>8),(byte)bits);return true;}
                var value=System.Drawing.ColorTranslator.FromHtml(text);color=new DrawColor(value.A,value.R,value.G,value.B);return true;
            }
            catch{color=default;return false;}
        }
        public override string ToString()=>$"#{A:X2}{R:X2}{G:X2}{B:X2}";
        public static bool operator ==(DrawColor a,DrawColor b)=>a.A==b.A && a.R==b.R && a.G==b.G && a.B==b.B;
        public static bool operator !=(DrawColor a,DrawColor b)=>!(a==b);
        public override bool Equals(object other)=>other is DrawColor color && this==color;
        public override int GetHashCode()=>((int)A<<24)|(R<<16)|(G<<8)|B;
        public static implicit operator System.Drawing.Color(DrawColor value)=>System.Drawing.Color.FromArgb(value.A,value.R,value.G,value.B);
        public static implicit operator DrawColor(System.Drawing.Color value)=>new DrawColor(value.A,value.R,value.G,value.B);
    }
    public static class DrawColors
    {public static DrawColor White=>System.Drawing.Color.White;public static DrawColor Black=>System.Drawing.Color.Black;}
    internal static class DrawCompatibility
    {
        public static bool StartsWith(this string text,char c)=>text.Length>0 && text[0]==c;
        public static bool EndsWith(this string text,char c)=>text.Length>0 && text[text.Length-1]==c;
        public static bool Contains(this string text,string value,StringComparison comparison)=>text.IndexOf(value,comparison)>=0;
        public static bool TryAdd<TKey,TValue>(this Dictionary<TKey,TValue> values,TKey key,TValue value){if(values.ContainsKey(key))return false;values.Add(key,value);return true;}
        public static TValue GetValueOrDefault<TKey,TValue>(this Dictionary<TKey,TValue> values,TKey key,TValue fallback=default(TValue))=>values.TryGetValue(key,out var value)?value:fallback;
    }
    internal static class DrawMath
    {
        public const float PI=(float)Math.PI;
        public static float Round(float v,int n)=>(float)Math.Round(v,n);
        public static float Sqrt(float v)=>(float)Math.Sqrt(v);
        public static float Sin(float v)=>(float)Math.Sin(v);
        public static float Cos(float v)=>(float)Math.Cos(v);
        public static float Tan(float v)=>(float)Math.Tan(v);
        public static float Atan2(float y,float x)=>(float)Math.Atan2(y,x);
        public static T Clamp<T>(T v,T min,T max) where T:IComparable<T> => v.CompareTo(min)<0?min:v.CompareTo(max)>0?max:v;
    }
    public static class ClassicDrawStore
    {
        private static List<SeAssaDrawShape> _shapes;
        private static string Path=>System.IO.Path.Combine(Configuration.DataDirectory,"ClassicDrawShapes.json");
        public static List<SeAssaDrawShape> Shapes
        {get{if(_shapes==null){try{_shapes=JsonConvert.DeserializeObject<List<SeAssaDrawShape>>(File.ReadAllText(Path));}catch{_shapes=new List<SeAssaDrawShape>();}}return _shapes;}}
        public static void Save(){var path=Path;Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));var staged=path+".tmp";File.WriteAllText(staged,JsonConvert.SerializeObject(Shapes,Formatting.Indented));if(File.Exists(path))File.Replace(staged,path,null);else File.Move(staged,path);}
    }
}
namespace Nikse.SubtitleEdit.Logic.ClassicDraw
{
    internal static class DrawSettings
    {
        public static DrawColor PointColor=>System.Drawing.Color.IndianRed;
        public static DrawColor PointHelperColor=>System.Drawing.Color.LightGreen;
    }
}
