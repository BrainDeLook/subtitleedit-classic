using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Ocr;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic.Ocr;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassic53Ui
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    [STAThread] static void Main(string[] args)
    {
        var folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,folder+Path.DirectorySeparatorChar);
        Configuration.Settings.General.Language="ru-RU";
        Nikse.SubtitleEdit.Logic.LanguageSettings.Current=Nikse.SubtitleEdit.Logic.Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
        using(var ocr=new ClassicVideoOcr(null))
        {
            Show(ocr);var rows=(DataGridView)ocr.GetType().GetField("_lines",Flags).GetValue(ocr);var row=rows.Rows.Add("00:00:01,000","00:00:03,000","2","Hello test");rows.Rows[row].Tag=new Paragraph("Hello test",1000,3000);
            Modal(ocr,"ShowOcrFindReplace",folder,"ocr-find-replace.png");ocr.Close();
        }
        var subtitle=new Subtitle();subtitle.Paragraphs.Add(new Paragraph("Test",0,1000));
        using(var speech=new TextToSpeech(subtitle,new SubRip(),null,null))
        {
            Show(speech);Modal(speech,"EditDubbingSettings",folder,"dubbing-settings.png");
            var combo=speech.GetType().GetField("nikseComboBoxEngine",Flags).GetValue(speech);combo.GetType().GetProperty("Text").SetValue(combo,"OpenAI-compatible (API)");Application.DoEvents();Modal(speech,"EditCompatibleSettings",folder,"api-settings.png");speech.Close();
        }
        var db=new NOcrDb(args[1]);var before=db.OcrCharacters.Sum(c=>c.LinesForeground.Count+c.LinesBackground.Count);
        using(var editor=new VobSubNOcrEdit(db,null,args[1]))
        {
            Show(editor);Save(editor,Path.Combine(folder,"nocr-editor.png"));var button=editor.Controls.OfType<Button>().First(c=>c.Name=="buttonOptimizeNOcr");
            if(button.Bounds.IntersectsWith(editor.Controls.OfType<Button>().First(c=>c.Name=="buttonOK").Bounds))throw new Exception("Optimizer footer overlap");
            var task=(Task)editor.GetType().GetMethod("OptimizeDatabase",Flags).Invoke(editor,null);var cancel=(CancellationTokenSource)editor.GetType().GetField("_optimizerCancellation",Flags).GetValue(editor);cancel.Cancel();
            var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted && watch.Elapsed.TotalSeconds<60){Application.DoEvents();Thread.Sleep(20);}if(!task.IsCompleted)throw new Exception("Optimizer cancel timeout");task.GetAwaiter().GetResult();
            if(db.OcrCharacters.Sum(c=>c.LinesForeground.Count+c.LinesBackground.Count)!=before || editor.GetType().GetField("_optimizerCancellation",Flags).GetValue(editor)!=null)throw new Exception("UI cancellation changed database/state");editor.Close();
        }
        Console.WriteLine("PASS: classic OCR/API/dubbing dialogs, nOCR footer and cancellation without modifying the database");
    }
    static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();}
    static void Save(Form form,string path){using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(path);}}
    static void Modal(Form owner,string method,string folder,string image)
    {
        bool found=false;using(var timer=new System.Windows.Forms.Timer{Interval=250})
        {
            timer.Tick+=(s,e)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=owner);if(dialog==null)return;found=true;Save(dialog,Path.Combine(folder,image));dialog.DialogResult=DialogResult.Cancel;dialog.Close();timer.Stop();};timer.Start();owner.GetType().GetMethod(method,Flags).Invoke(owner,null);
        }
        if(!found)throw new Exception("Dialog did not open: "+method);
    }
}
