using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.VideoOcr;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassic53Ocr
{
    static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
        if(args[0]=="unit")
        {
            var pattern=ClassicOcrSearchReplace.Pattern("мир",false,false,true);var text=ClassicOcrSearchReplace.Replace("Мир, мирный и мир.",pattern,"$5",false,out var count);Check(count==2 && text=="$5, мирный и $5.","Unicode whole-word/literal-dollar replacement");
            text=ClassicOcrSearchReplace.Replace("123 abc",ClassicOcrSearchReplace.Pattern("(\\d+)",true,true,false),"[$1]",true,out count);Check(count==1 && text=="[123] abc","Regex replacement groups");
            Check(ClassicCrispVideoOcr.PinnedVersion("cpu")=="0.17.12" && ClassicCrispVideoOcr.PinnedVersion("cuda")=="0.17.11","Stable OCR builds");
            typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,folder+Path.DirectorySeparatorChar);
            foreach(var language in new[]{"rus","eng","hrv","ron"})using(var rules=new ClassicVideoOcrRules(language))Check(rules.ReplaceList!=null,"Missing OCR dictionary "+language);
            Console.WriteLine("PASS: Unicode/whole-word/regex replacement, stable OCR versions and all bundled rule files");return;
        }
        typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Subtitle Edit")+Path.DirectorySeparatorChar);
        var options=new ClassicVideoOcrOptions{Engine="CrispEmbed",CrispVariant=args.Length>3 ? args[3] : "cpu",CrispBackend=args.Length>4 ? args[4] : "PP-OCRv6",CrispModel=args.Length>4 && args[4]=="GLM-OCR" ? "glm-ocr-q8_0.gguf" : "PP-OCRv6_medium_rec-f16.gguf"};
        ClassicCrispVideoOcr.EnsureInstalledAsync(options,CancellationToken.None,null).GetAwaiter().GetResult();
        var groups=new[]{new VideoOcrFrameGroup{RepresentativeFileName=Path.GetFullPath(args[2])}};
        ClassicCrispVideoOcr.RecognizeAsync(groups,folder,options,CancellationToken.None,null).GetAwaiter().GetResult();
        Check(!string.IsNullOrWhiteSpace(groups[0].Text),"Native OCR produced no text");File.WriteAllText(Path.Combine(folder,"recognized.txt"),groups[0].Text);
        if(args.Length>5)Check(groups[0].Text.IndexOf(args[5],StringComparison.OrdinalIgnoreCase)>=0,"Native OCR did not match the known subtitle: "+groups[0].Text);
        Console.WriteLine("PASS native CrispEmbed "+ClassicCrispVideoOcr.PinnedVersion(options.CrispVariant)+": "+groups[0].Text);
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
