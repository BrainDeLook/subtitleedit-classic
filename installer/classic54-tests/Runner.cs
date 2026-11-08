// Adapted stable Subtitle Edit v5.3.0 nOCR regression cases; GDI replaces Skia only in bitmap fixtures.
using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Ocr;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class FactAttribute:Attribute{}
class TheoryAttribute:Attribute{}
[AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]class MemberDataAttribute:Attribute{public string Name;public MemberDataAttribute(string name){Name=name;}}
[AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]class InlineDataAttribute:Attribute{public object[] Data;public InlineDataAttribute(params object[] data){Data=data;}}
static class FixtureHex {
 public static byte[] From(string value)=>Enumerable.Range(0,value.Length/2).Select(i=>Convert.ToByte(value.Substring(i*2,2),16)).ToArray();
 public static string To(byte[] value)=>BitConverter.ToString(value).Replace("-", "");
}
static class Assert {
 public static void NotEqual<T>(T expected,T actual)=>False(object.Equals(expected,actual),"expected different values");
 public static void DoesNotContain<T>(T item,IEnumerable<T> items)=>False(items.Contains(item),"unexpected item");
 public static void NotEmpty(IEnumerable value)=>True(value.Cast<object>().Any(),"expected non-empty");
 public static void DoesNotContain<T>(IEnumerable<T> items,Func<T,bool> predicate)=>False(items.Any(predicate),"unexpected matching item");
 public static void DoesNotContain(string value,string actual)=>False(actual.Contains(value),"unexpected "+value);
 public static T Throws<T>(Action action) where T:Exception {try{action();}catch(T ex){return ex;}throw new Exception("Expected "+typeof(T).Name);}

 public static void True(bool? value,string message="")=>True(value==true,message);
 public static void True(bool value,string message=""){if(!value)throw new Exception("Expected true "+message);}
 public static void False(bool value,string message="")=>True(!value,message);
 public static void Null(object value)=>True(value==null,"expected null");
 public static void NotNull(object value)=>True(value!=null,"expected non-null");
 public static void Empty(IEnumerable value)=>True(!value.Cast<object>().Any(),"expected empty");
 public static void Equal(double expected,double actual,double tolerance){True(Math.Abs(expected-actual)<=tolerance,"expected "+expected+" actual "+actual+" tolerance "+tolerance);}
 public static void Same(object expected,object actual)=>True(ReferenceEquals(expected,actual),"same reference");
 public static void Equal<T>(T expected,T actual){if(expected is IEnumerable a && actual is IEnumerable b && !(expected is string)){True(a.Cast<object>().SequenceEqual(b.Cast<object>()),"sequence equality");return;}True(object.Equals(expected,actual),"expected ["+expected+"] actual ["+actual+"]");}
 public static void Equal<T>(IEnumerable<T> expected,IEnumerable<T> actual)=>True(expected.SequenceEqual(actual),"sequence equality");
 public static void InRange<T>(T value,T min,T max)where T:IComparable<T> =>True(value.CompareTo(min)>=0 && value.CompareTo(max)<=0,"range value="+value+" min="+min+" max="+max);
 public static void Contains<T>(T expected,IEnumerable<T> actual)=>True(actual.Contains(expected),"contains item");
 public static void Contains(string text,string actual)=>True(actual.Contains(text),"contains "+text);
 public static void Contains<T>(IEnumerable<T> items,Func<T,bool> predicate)=>True(items.Any(predicate),"contains predicate");
 public static void All<T>(IEnumerable<T> items,Action<T> action){foreach(var i in items)action(i);}
 public static void Single<T>(IEnumerable<T> items,Func<T,bool> predicate)=>True(items.Count(predicate)==1,"single predicate");
 public static object Single(IEnumerable items)=>Single(items.Cast<object>());
 public static T Single<T>(IEnumerable<T> items){True(items.Count()==1,"single");return items.Single();}
 public static void EndsWith(string suffix,string actual)=>True(actual.EndsWith(suffix),"suffix");
 public static void StartsWith(string prefix,string actual)=>True(actual.StartsWith(prefix),"prefix");
}

class Classic54Runner {
 [STAThread] static int Main(string[] args) {
  var testRoot=AppDomain.CurrentDomain.BaseDirectory;
  foreach(var field in new[]{"DataDirectory","SettingsFileName","DictionariesDirectory","WaveformsDirectory","AutoBackupDirectory"})
   typeof(Configuration).GetField(field).SetValue(null,field=="SettingsFileName"?Path.Combine(testRoot,"Settings.xml"):field=="DictionariesDirectory"?Path.Combine(testRoot,"Dictionaries")+Path.DirectorySeparatorChar:testRoot);
  Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
  Configuration.Settings.General.Language="en";Configuration.Settings.General.CheckForUpdates=false;
  Configuration.Settings.General.FFmpegLocation=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","upstream54","ffprobe","ffmpeg.exe"));
  Nikse.SubtitleEdit.Logic.ClassicFormats.ClassicFormatRegistry.Register();
  int passed=0,failed=0;
  foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.Namespace=="Classic54Tests" && (args.Length==0 || args.Contains(t.Name))))
  foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttributes(typeof(FactAttribute),true).Any() || m.GetCustomAttributes(typeof(TheoryAttribute),true).Any())) {
   var rows=method.GetCustomAttributes(typeof(InlineDataAttribute),true).Cast<InlineDataAttribute>().Select(x=>x.Data).ToList();
   foreach(var member in method.GetCustomAttributes(typeof(MemberDataAttribute),true).Cast<MemberDataAttribute>())
   {
    var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    var property=type.GetProperty(member.Name,flags);var factory=type.GetMethod(member.Name,flags);
    var data=property!=null?property.GetValue(null):factory.Invoke(null,null);
    rows.AddRange(((IEnumerable)data).Cast<object[]>());
   }
   if(rows.Count==0)rows.Add(new object[0]);
   foreach(var values in rows) {
    var row=values ?? new object[]{null};
    var parameters=method.GetParameters();
    if(parameters.Length>0 && parameters[parameters.Length-1].IsDefined(typeof(ParamArrayAttribute),false))
    {
      var fixedCount=parameters.Length-1;var rest=Array.CreateInstance(parameters[fixedCount].ParameterType.GetElementType(),Math.Max(0,row.Length-fixedCount));
      for(var i=fixedCount;i<row.Length;i++)rest.SetValue(row[i],i-fixedCount);
      row=row.Take(fixedCount).Concat(new object[]{rest}).ToArray();
    }
    for(var i=0;i<row.Length && i<parameters.Length;i++)
      if(row[i]!=null && parameters[i].ParameterType.IsPrimitive && row[i].GetType()!=parameters[i].ParameterType)row[i]=Convert.ChangeType(row[i],parameters[i].ParameterType,System.Globalization.CultureInfo.InvariantCulture);
    object instance = null;
    try { instance = Activator.CreateInstance(type); System.Threading.SynchronizationContext.SetSynchronizationContext(null); var returned=method.Invoke(instance,row);if(returned is System.Threading.Tasks.Task pending)pending.GetAwaiter().GetResult(); passed++; Console.WriteLine("PASS "+type.Name+"."+method.Name); }
    catch(Exception ex) { failed++;Console.WriteLine("FAIL "+type.Name+"."+method.Name+": "+(ex.InnerException??ex)); }
    finally { (instance as IDisposable)?.Dispose(); }
   }
  }
  Console.WriteLine("RESULT "+passed+" passed "+failed+" failed"); return failed==0?0:1;
 }
}

namespace System.Runtime.CompilerServices {internal static class IsExternalInit {}}
