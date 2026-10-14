using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicTiming;
using Nikse.SubtitleEdit.Forms;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicTiming
{
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
        if(args[0]=="unit"){Unit(folder);return;}
        Configuration.Settings.General.Language="ru-RU";
        Nikse.SubtitleEdit.Logic.LanguageSettings.Current=Nikse.SubtitleEdit.Logic.Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
        if(args[0]=="ui")
        {
            var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Test",1000,3000));
            using(var form=new ClassicImproveTimeCodes(sub,null,10,24))
            {form.Location=new Point(-20000,-20000);form.StartPosition=FormStartPosition.Manual;form.Show();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"timing-ui.png"));}form.Close();}
            Console.WriteLine("PASS: classic timing review form");return;
        }
        typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Subtitle Edit")+Path.DirectorySeparatorChar);
        Configuration.Settings.General.FFmpegLocation=args[2];
        var source=args[3];var language=args[4];
        var util=typeof(ClassicRetimingJob).Assembly.GetType("Nikse.SubtitleEdit.Logic.UiUtil");
        var info=(VideoInfo)util.GetMethod("GetVideoInfo",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(string)},null).Invoke(null,new object[]{source});
        var subtitle=new Subtitle();subtitle.Paragraphs.Add(new Paragraph(args[5],1800,(info.TotalSeconds-0.15)*1000));
        var result=new ClassicRetimingJob().RunAsync(subtitle,source,info.TotalSeconds,"cpu",false,true,language,new SubtitleRetimer.Options{MaxShiftSeconds=1,MinDurationSeconds=0.2},CancellationToken.None,null).GetAwaiter().GetResult();
        if(result.Length!=1 || result[0].Status==SubtitleRetimer.LineStatus.Failed || result[0].StartSeconds<1.0 || result[0].StartSeconds>1.55 || !result[0].HeardRatio.HasValue || result[0].HeardRatio<0.8)throw new Exception("Real alignment/check failed: "+result[0]);
        Console.WriteLine("PASS native "+language+": "+result[0]);
    }
    static void Unit(string folder)
    {
        var lines=Enumerable.Range(0,12).Select(i=>new SubtitleRetimer.Line("Line number "+i,10+i*3,12+i*3)).ToList();
        var audio=new Audio(folder);var runner=new Oracle(audio);
        foreach(var l in lines)runner.Truth[l.Text]=(l.StartSeconds+0.3,l.EndSeconds-0.2);
        var results=new SubtitleRetimer(runner,audio,new SubtitleRetimer.Options{BatchLines=8}).RetimeAsync(lines).GetAwaiter().GetResult();
        Check(runner.Fed.Count==2 && runner.Fed[0].Length==9 && runner.Fed[1].Length==5,"Sentinel batching");
        for(int i=0;i<12;i++)Check(results[i].Status==SubtitleRetimer.LineStatus.Retimed && Math.Abs(results[i].StartSeconds-lines[i].StartSeconds-0.3)<0.01 && Math.Abs(results[i].EndSeconds-lines[i].EndSeconds-0.3)<0.01,"Timing translation/tails");
        var noise=new[]{new SubtitleRetimer.Line("[music]",10,12),new SubtitleRetimer.Line("{\\pos(100,100)}Sign",13,14),new SubtitleRetimer.Line("♪ A song ♪",15,18)};
        var skipped=new SubtitleRetimer(runner,audio).RetimeAsync(noise).GetAwaiter().GetResult();Check(skipped.All(r=>r.Status==SubtitleRetimer.LineStatus.NoSpeech),"Sign/music protection");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{new SubtitleRetimer(runner,audio).RetimeAsync(lines,null,cancellation.Token).GetAwaiter().GetResult();throw new Exception("Ignored cancel");}catch(OperationCanceledException){}}
        var heard=ClassicRetimingJob.ParseHeardWords("1\n00:00:01,000 --> 00:00:04,000\nOne two three\n");Check(heard.Count==3 && Math.Abs(heard[0].StartSeconds-1)<0.001 && Math.Abs(heard[2].EndSeconds-4)<0.001,"Independent word evidence parsing");
        Console.WriteLine("PASS: 5.3 retimer sentinels, offset translation, quiet tails, signs/music, cancellation and word evidence");
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    sealed class Audio : ClassicAlignerContracts.IAudioSource
    {
        public double Start;readonly string Folder;public Audio(string folder){Folder=folder;}public double TotalSeconds=>600;
        public Task<string> ExtractWindowAsync(double start,double length,CancellationToken cancel){Start=start;return Task.FromResult(Path.Combine(Folder,Guid.NewGuid()+".wav"));}
    }
    sealed class Oracle : ClassicAlignerContracts.IRunner
    {
        readonly Audio Audio;public Oracle(Audio audio){Audio=audio;}
        public readonly Dictionary<string,(double Start,double End)> Truth=new Dictionary<string,(double,double)>();public readonly List<string[]> Fed=new List<string[]>();
        public Task<string> AlignAsync(string file,string text,CancellationToken cancel)
        {
            var lines=File.ReadAllLines(text);Fed.Add(lines);var s=new StringBuilder();
            for(int i=0;i<lines.Length;i++){var t=Truth[lines[i]];s.AppendLine((i+1).ToString());s.AppendLine(Time(t.Start-Audio.Start)+" --> "+Time(t.End-Audio.Start));s.AppendLine(lines[i]);s.AppendLine();}return Task.FromResult(s.ToString());
        }
        static string Time(double at)=>TimeSpan.FromSeconds(Math.Max(0,at)).ToString(@"hh\:mm\:ss\,fff",CultureInfo.InvariantCulture);
    }
}
