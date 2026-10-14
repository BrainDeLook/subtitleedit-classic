using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassic53Tts
{
    static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
    static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        if(args[0]=="unit") {Preparation(); return;}
        typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Subtitle Edit")+Path.DirectorySeparatorChar);
        var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
        var settings=new ClassicQwenTtsSettings{Supertonic=true,Voice="M1",Language="ru",Variant="cpu"};
        var progress=new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message));
        using(var engine=new ClassicQwenTts(settings))
        {
            engine.StartAsync(CancellationToken.None,progress).GetAwaiter().GetResult();
            engine.SpeakAsync("Сегодня мы проверяем озвучку русских субтитров. Голос должен произносить слова ясно.","M1",Path.Combine(folder,"russian.wav"),CancellationToken.None).GetAwaiter().GetResult();
            settings.Language="en";
            engine.SpeakAsync("Today we are testing English subtitles. Every sentence should sound clear and natural.","F1",Path.Combine(folder,"english.wav"),CancellationToken.None).GetAwaiter().GetResult();
        }
        Console.WriteLine("PASS: native Supertonic Russian/English; changed voice and language in one server");
    }
    static void Preparation()
    {
        var s=new Subtitle();
        s.Paragraphs.Add(new Paragraph("This sentence",1000,2200));
        s.Paragraphs.Add(new Paragraph("continues here",2400,3500));
        s.Paragraphs.Add(new Paragraph("and finishes.",3600,4700));
        s.Paragraphs.Add(new Paragraph("[music]",4800,5500));
        s.Paragraphs.Add(new Paragraph("Another sentence",6000,7200){Actor="A"});
        s.Paragraphs.Add(new Paragraph("another speaker",7400,8500){Actor="B"});
        s.Paragraphs.Add(new Paragraph("- Dialogue",8600,9700));
        s.Paragraphs.Add(new Paragraph("Next words",9800,10900));
        s.Paragraphs.Add(new Paragraph("after long silence",11500,12700));
        var suggestions=ClassicTtsPreparation.Detect(s,"en");
        Check(suggestions.Where(x=>!x.Noise).Select(x=>x.Index).SequenceEqual(new[]{0,1}),"Sentence/speaker/dialogue/gap safeguards");
        Check(suggestions.Any(x=>x.Noise && x.Index==3),"Music cue not identified");
        var merged=ClassicTtsPreparation.Apply(s,new System.Collections.Generic.HashSet<int>{0,1},"en");
        Check(s.Paragraphs.Count==9 && s.Paragraphs[0].EndTime.TotalMilliseconds==2200,"Source mutated");
        Check(merged.Paragraphs.Count==7 && merged.Paragraphs[0].EndTime.TotalMilliseconds==4700,"Continuation chain timing");
        Check(merged.Paragraphs[1].Text=="[music]","Noise text must remain in subtitles");
        Check(ClassicSupertonicLanguages.Catalog.Length==31 && ClassicQwenTts.SupertonicVoices.Length==10,"Stable model catalog mismatch");
        Console.WriteLine("PASS: continuation chains, original immutability, speaker/dialogue/gap protection, noise text preservation");
    }
}
