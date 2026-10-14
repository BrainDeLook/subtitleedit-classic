using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoOcr;

[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

class TestClassicVideoOcr
{
    static void Check(bool test,string message){if(!test)throw new Exception(message);}
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        Configuration.Settings.General.Language="ru-RU";
        Configuration.Settings.General.FFmpegLocation=Environment.GetEnvironmentVariable("CLASSIC_OCR_TEST_FFMPEG") ?? Configuration.Settings.General.FFmpegLocation;
        LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
        Directory.CreateDirectory(args[1]); Application.EnableVisualStyles();
        if(args[0]=="prepare")
        {
            Directory.CreateDirectory(Path.Combine(ClassicPaddleOcr.Root,"Downloads"));
            foreach(var file in Directory.GetFiles(args[1],"*.7z")) File.Copy(file,Path.Combine(ClassicPaddleOcr.Root,"Downloads",Path.GetFileName(file)),true);
            ClassicPaddleOcr.EnsureInstalledAsync("cpu",CancellationToken.None,null).GetAwaiter().GetResult();
            ClassicPaddleOcr.EnsureInstalledAsync("cuda12",CancellationToken.None,null).GetAwaiter().GetResult();
            Console.WriteLine("PASS: pinned Paddle CPU/GPU archives and model bundle extracted and verified"); return;
        }
        if(args[0]=="ui") {Ui(args[1]);return;}
        if(args[0]=="ui-run") {UiRun(args[1],false);return;}
        if(args[0]=="ui-cancel") {UiRun(args[1],true);return;}
        if(args[0]=="ui-cancel-crisp") {UiRun(args[1],true,true);return;}
        if(args[0]=="prepare-crisp")
        {
            Directory.CreateDirectory(Path.Combine(ClassicPaddleOcr.Root,"Downloads")); Directory.CreateDirectory(ClassicCrispVideoOcr.ModelFolder);
            foreach(var file in Directory.GetFiles(args[1],"*.zip"))File.Copy(file,Path.Combine(ClassicPaddleOcr.Root,"Downloads",Path.GetFileName(file)),true);
            foreach(var file in Directory.GetFiles(args[1],"*.gguf"))File.Copy(file,Path.Combine(ClassicCrispVideoOcr.ModelFolder,Path.GetFileName(file)),true);
            foreach(var variant in new[]{"cpu","vulkan","cuda"})ClassicCrispVideoOcr.EnsureInstalledAsync(new ClassicVideoOcrOptions{Engine="CrispEmbed",CrispVariant=variant,CrispBackend="PP-OCRv6",CrispModel="PP-OCRv6_medium_rec-f16.gguf"},CancellationToken.None,null).GetAwaiter().GetResult();
            Console.WriteLine("PASS: pinned CrispEmbed runtime extraction and GGUF models");return;
        }
        if(args[0]=="unit") {Unit(args[1]);return;}
        if(args[0]=="scan" || args[0]=="source-video")
        {
            var video=args[2]; var ui=typeof(ClassicVideoOcr).Assembly.GetType("Nikse.SubtitleEdit.Logic.UiUtil");
            var info=(VideoInfo)ui.GetMethod("GetVideoInfo",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(string)},null).Invoke(null,new object[]{video});
            var options=new ClassicVideoOcrOptions{Language=args[3],Variant=args[4]};
            if(args.Length>5 && args[5]=="CrispEmbed") {options.Engine="CrispEmbed";options.CrispVariant=args[4];options.CrispBackend=args[6];if(args.Length>7)options.CrispModel=args[7];}
            var result=ClassicVideoOcrPipeline.ScanAsync(video,info,options,CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(args[1],"result.srt"),new Nikse.SubtitleEdit.Core.SubtitleFormats.SubRip().ToText(result,null));
            foreach(var p in result.Paragraphs) Console.WriteLine(p.StartTime.ToDisplayString()+" --> "+p.EndTime.ToDisplayString()+" "+p.Text.Replace(Environment.NewLine," | "));
            if(args[0]=="source-video"){Check(result.Paragraphs.Count>0,"No subtitles found in the actual source sample");Console.WriteLine("PASS: actual source video, "+result.Paragraphs.Count+" OCR cues");return;}
            Check(result.Paragraphs.Count==3,"Expected three known subtitle cues");
            var expected=new[]{(1.37,2.93),(4.12,6.68),(8.22,9.84)};
            for(int i=0;i<3;i++){Check(Math.Abs(result.Paragraphs[i].StartTime.TotalSeconds-expected[i].Item1)<0.09,"Start refinement failed");Check(Math.Abs(result.Paragraphs[i].EndTime.TotalSeconds-expected[i].Item2)<0.09,"End refinement failed");}
            Console.WriteLine("PASS: real "+options.Language+" "+options.Variant+" OCR, grouping and timings within 90ms");return;
        }
    }
    static void Ui(string folder)
    {
        using(var form=new ClassicVideoOcr(""))
        {
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();
            var engine=(ComboBox)typeof(ClassicVideoOcr).GetField("_engine",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
            foreach(var selection in new[]{"Paddle OCR","CrispEmbed"})
            foreach(var size in new[]{new Size(1080,760),new Size(960,700)})
            {
                engine.SelectedItem=selection;
                form.Size=size; Application.DoEvents();
                foreach(var name in new[]{"_start","_ok","_cancel","_status","_progress"})
                {
                    var c=(Control)typeof(ClassicVideoOcr).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                    Check(c.Visible && form.RectangleToClient(c.RectangleToScreen(c.ClientRectangle)).Bottom<=form.ClientSize.Height,"Clipped control "+name);
                }
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"ocr-"+selection.Replace(" ","_")+"-"+size.Width+".png"));}
                var table=(TableLayoutPanel)typeof(ClassicVideoOcr).GetField("_settingsTable",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var viewport=(Panel)table.Parent;
                var last=(Control)typeof(ClassicVideoOcr).GetField("_positionTag",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                viewport.ScrollControlIntoView(last);Application.DoEvents();
                var bounds=viewport.RectangleToClient(last.RectangleToScreen(last.ClientRectangle));
                Check(bounds.Top>=0 && bounds.Bottom<=viewport.ClientSize.Height,"Final settings row cannot be fully scrolled into view");
                foreach(var container in table.Controls.OfType<Control>().Where(c=>c.Visible && (c is FlowLayoutPanel || c is TableLayoutPanel)))
                    foreach(Control child in container.Controls)
                        Check(container.ClientRectangle.Contains(child.Bounds),"Crop control clipped by its parent: "+child.Text);
                foreach(Control label in table.Controls.OfType<Label>().Where(c=>c.Visible && table.GetColumnSpan(c)==1))
                    Check(label.Height>=label.GetPreferredSize(new Size(label.Width,0)).Height,"Wrapped settings label is clipped: "+label.Text);
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"scrolled-"+selection.Replace(" ","_")+"-"+size.Width+".png"));}
            }
            Console.WriteLine("PASS: classic Video OCR layout at normal/minimum widths");
        }
    }
    static void UiRun(string folder,bool cancel,bool crisp=false)
    {
        using(var form=new ClassicVideoOcr(Path.GetFullPath(Path.Combine(folder,"..","samples","en.mp4"))))
        using(var timer=new System.Windows.Forms.Timer{Interval=100})
        {
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(typeof(ClassicVideoOcr).GetField("_info",flags).GetValue(form)==null && watch.Elapsed.TotalSeconds<20){Application.DoEvents();Thread.Sleep(10);}
            Check(typeof(ClassicVideoOcr).GetField("_info",flags).GetValue(form)!=null,"Video preview did not initialize");
            ((ComboBox)typeof(ClassicVideoOcr).GetField("_engine",flags).GetValue(form)).SelectedItem="Paddle OCR";
            ((ComboBox)typeof(ClassicVideoOcr).GetField("_variant",flags).GetValue(form)).SelectedItem="cpu";
            if(crisp)
            {
                ((ComboBox)typeof(ClassicVideoOcr).GetField("_engine",flags).GetValue(form)).SelectedItem="CrispEmbed";
                ((ComboBox)typeof(ClassicVideoOcr).GetField("_variant",flags).GetValue(form)).SelectedItem="vulkan";
                var backends=(ComboBox)typeof(ClassicVideoOcr).GetField("_crispBackend",flags).GetValue(form);
                backends.SelectedItem=backends.Items.Cast<CrispEmbedBackend>().First(b=>b.Name=="DeepSeek-OCR-2");
            }
            var languages=(ComboBox)typeof(ClassicVideoOcr).GetField("_language",flags).GetValue(form);
            languages.SelectedItem=languages.Items.Cast<Nikse.SubtitleEdit.Logic.Ocr.OcrLanguage2>().First(l=>l.Code=="en");
            watch.Restart();bool cancelled=false;
            timer.Tick+=(s,e)=>
            {
                var owned=crisp ? OwnedProcesses("crispembed-server") : OwnedProcesses("paddleocr");
                if(cancel && !cancelled && owned.Count>0){((CancellationTokenSource)typeof(ClassicVideoOcr).GetField("_operation",flags).GetValue(form))?.Cancel();cancelled=true;}
                // Close only dialogs belonging to this isolated test application.
                foreach(Form dialog in Application.OpenForms.Cast<Form>().ToArray()) if(dialog!=form){dialog.DialogResult=DialogResult.OK;dialog.Close();}
            };
            timer.Start();var task=(Task)typeof(ClassicVideoOcr).GetMethod("Run",flags).Invoke(form,new object[]{false,false});
            int pumps=0;while(!task.IsCompleted && watch.Elapsed.TotalSeconds<90){Application.DoEvents();Thread.Sleep(10);pumps++;}
            timer.Stop();Check(task.IsCompleted,"Classic OCR operation hung");task.GetAwaiter().GetResult();
            Check(pumps>0 && typeof(ClassicVideoOcr).GetField("_operation",flags).GetValue(form)==null,"Operation did not release UI state");
            Check(((Button)typeof(ClassicVideoOcr).GetField("_start",flags).GetValue(form)).Enabled,"Start button was not restored");
            if(cancel) {Check(cancelled && form.SubtitleFromOcr.Paragraphs.Count==0,"Cancel returned incomplete results");Check(OwnedProcesses(crisp ? "crispembed-server" : "paddleocr").Count==0,"OCR cancellation left a native process running");}
            else Check(form.SubtitleFromOcr.Paragraphs.Count==3,"Classic UI did not return expected subtitles with OCR fixes enabled");
            using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,cancel ? "cancelled.png" : "completed.png"));}
            Console.WriteLine(cancel ? "PASS: real Video OCR cancellation through classic form and restored controls" : "PASS: real Video OCR through classic form, preview and OCR corrections; three editable cues returned");
        }
    }
    static List<int> OwnedProcesses(string name)
    {
        var result=new List<int>();
        foreach(var p in System.Diagnostics.Process.GetProcessesByName(name))using(p)
        {try{if(!p.HasExited && p.MainModule.FileName.StartsWith(ClassicPaddleOcr.Root,StringComparison.OrdinalIgnoreCase))result.Add(p.Id);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}
        return result;
    }
    static void Unit(string folder)
    {
        // Stable upstream VideoOcrTests: confidence weighting, short junk gap,
        // text cleaning and native-frame transition selection.
        var groups=new[]{new VideoOcrFrameGroup{StartFrame=0,EndFrame=5,Text="Hello",Confidence=.2},new VideoOcrFrameGroup{StartFrame=6,EndFrame=9,Text="Hello!",Confidence=1}};
        Check(VideoOcrLineBuilder.Build(groups,5,80,250,250).Single().Text=="Hello!","Confidence weighted majority changed");
        Check(VideoOcrLineBuilder.CleanOcrResult("```\n**Hello**\nHello\n```\n!!!")=="Hello","Vision output cleaner changed");
        var method=typeof(VideoOcrTimingRefiner).GetMethod("PickTransition",BindingFlags.NonPublic|BindingFlags.Static);
        var list=new List<(double,bool)>{(1000,false),(1040,true),(1080,true),(1120,false)};
        Check((double?)method.Invoke(null,new object[]{list,true,40d})==1040,"Start transition changed");
        Check((double?)method.Invoke(null,new object[]{list,false,40d})==1120,"End transition changed");
        Check(PaddleOcrModels.GetRecName("ru","mobile").Contains("eslav"),"Wrong Russian recognizer");
        Check(PaddleOcrModels.GetRecName("en","mobile")=="PP-OCRv6_small_rec","Wrong English recognizer");
        var regions=ClassicPaddleOcr.ReadRegions("{\"rec_texts\":[\"lower\",\"upper\"],\"rec_scores\":[0.95,0.95],\"rec_polys\":[[[0,50],[100,50],[100,70],[0,70]],[[0,10],[100,10],[100,30],[0,30]]]}");
        Check(PaddleOcrTextLayout.BuildText(regions,75,false,out var confidence)=="upper"+Environment.NewLine+"lower","Paddle line reading order changed");
        var files=new List<string>();
        for(int n=0;n<4;n++)
        {
            var file=Path.Combine(folder,"img"+n.ToString("D6")+".jpg");
            using(var b=new Bitmap(720,120)) using(var g=Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(60,70,60));
                if(n<3) foreach(var y in new[]{40,70}) for(int x=150;x<570;x+=14){g.FillRectangle(Brushes.Black,x-1,y-1,12,4);g.FillRectangle(Brushes.White,x,y,10,2);}
                b.Save(file,System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            files.Add(file);
        }
        var mask=VideoOcrFrameGrouper.Group(files,190,92,null,CancellationToken.None);
        Check(mask.Count==2 && !mask[0].IsBlank && mask[1].IsBlank,"Thin subtitle strokes disappeared in the GDI mask adapter");
        Check(ClassicVideoOcrRules.NormalizeApostrophes("didn’t")=="didn't" && ClassicVideoOcrRules.NormalizeApostrophes("‘word’")=="‘word’","Upstream apostrophe rule changed");
        using(var rules=new ClassicVideoOcrRules("rus"))Check(rules.ReplaceList.WordReplaceList.ContainsKey("Чї0"),"Pinned Cyrillic OCR correction table was not loaded");
        Console.WriteLine("PASS: upstream grouping, confidence voting, text layout, transitions, language/model mapping and thin-stroke mask");
    }
}
