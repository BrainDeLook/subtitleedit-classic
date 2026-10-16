using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestWaveformReference
{
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Pump(Task task){var until=DateTime.UtcNow.AddSeconds(40);while(!task.IsCompleted){if(DateTime.UtcNow>until)throw new TimeoutException();Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
 static T Find<T>(Form f,string name) where T:Control => (T)f.Controls.Find(name,true).Single();
 [STAThread] static int Main(string[] args){try{
  var data=Path.GetFullPath(args[0]);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,data+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[1];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
  var a=Path.Combine(data,"first.wav");var b=Path.Combine(data,"second.wav");WriteWave(a,false);WriteWave(b,true);var media=Path.Combine(data,"multi.mkv");
  Pump(Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(args[1],"-nostdin -y -i \""+a+"\" -i \""+b+"\" -map 0:a -map 1:a -c:a pcm_s16le \""+media+"\""),CancellationToken.None,null));
  var cue=new Paragraph(@"{\an8}<i>Exact reference</i>"+Environment.NewLine+"words",1125,3625){Actor="Actor sample"};
  using(var form=new ClassicQwenReferenceImport(media,cue,1)){
   form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
   Check(Find<NumericUpDown>(form,"referenceStart").Value==1.125m && Find<NumericUpDown>(form,"referenceDuration").Value==2.5m,"waveform import keeps millisecond range");
   Check(Find<TextBox>(form,"textTranscript").Text=="Exact reference words" && Find<TextBox>(form,"textVoiceName").Text=="Actor sample","cue text strips ASS/HTML tags and suggests actor name");
   Check(!Find<CheckBox>(form,"referenceConsent").Checked && form.Reference==null,"opening waveform import never grants permission or imports");
   Check(Find<Label>(form,"labelReferenceSource").Text.EndsWith("2"),"import identifies selected second audio track");
   foreach(var size in new[]{form.Size,new Size(850,650)}){form.Size=size;Application.DoEvents();var label=Find<Label>(form,"labelReferenceSource");Check(label.Bottom<=label.Parent.ClientSize.Height && label.Right<=label.Parent.ClientSize.Width,"source track and ASR toolbar fit classic import");}
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(data,"waveform-reference-import.png"));}
   Find<CheckBox>(form,"referenceConsent").Checked=true;
   var button=form.Controls.Cast<Control>().SelectMany(Descendants).OfType<Button>().First(c=>c.Text=="Импорт");button.PerformClick();var until=DateTime.UtcNow.AddSeconds(40);while(form.Reference==null){if(DateTime.UtcNow>until)throw new TimeoutException();Application.DoEvents();Thread.Sleep(5);}
   Check(cue.StartTime.TotalMilliseconds==1125 && cue.EndTime.TotalMilliseconds==3625 && cue.Text.Contains("<i>"),"voice import leaves original cue timing and formatting untouched");
   var output=Path.Combine(ClassicQwenReference.Folder,form.Reference.Id+".wav");Check(Math.Abs(Nikse.SubtitleEdit.Logic.ClassicCast.ClassicVoiceAssets.WaveSeconds(output)-2.5)<.01 && Math.Abs(Frequency(output)-880)<3,"actual import extracts selected interval from second track");
  }
  var job=Path.Combine(data,"asr-range");var extract=ClassicReferenceAudio.ExtractAsync(media,1.125,2.5,job,CancellationToken.None,1);Pump(extract);Check(Math.Abs(Frequency(extract.Result)-880)<3,"reference ASR extraction also uses selected audio track");
  var main=(Nikse.SubtitleEdit.Forms.Main)FormatterServices.GetUninitializedObject(typeof(Nikse.SubtitleEdit.Forms.Main));
  using(var menu=new ContextMenuStrip()){
   typeof(Nikse.SubtitleEdit.Forms.Main).GetField("contextMenuStripWaveform",Flags).SetValue(main,menu);typeof(Nikse.SubtitleEdit.Forms.Main).GetField("_videoFileName",Flags).SetValue(main,media);
   var init=typeof(Nikse.SubtitleEdit.Forms.Main).GetMethod("InitializeClassicVoiceReferenceMenu",Flags);init.Invoke(main,null);init.Invoke(main,null);Check(menu.Items.OfType<ToolStripMenuItem>().Count()==1,"language refresh does not duplicate waveform command");var command=menu.Items.OfType<ToolStripMenuItem>().Single();var range=typeof(Nikse.SubtitleEdit.Forms.Main).GetMethod("SetClassicVoiceReferenceRange",Flags);
   range.Invoke(main,new object[]{cue,false});Check(command.Enabled,"valid cue enables voice reference command");range.Invoke(main,new object[]{cue,true});var captured=(Paragraph)typeof(Nikse.SubtitleEdit.Forms.Main).GetField("_classicVoiceReferenceRange",Flags).GetValue(main);Check(captured.Text=="" && cue.Text!="","free selection does not reuse an unrelated cue transcript");
   range.Invoke(main,new object[]{new Paragraph("Long",0,31000),false});Check(!command.Enabled,"overlong selection disables voice import instead of truncating it");range.Invoke(main,new object[]{null,false});Check(!command.Enabled,"empty waveform click clears stale voice reference selection");
  }
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static System.Collections.Generic.IEnumerable<Control> Descendants(Control c){yield return c;foreach(Control child in c.Controls)foreach(var descendant in Descendants(child))yield return descendant;}
 static double Frequency(string file){using(var stream=File.OpenRead(file)){var header=new WaveHeader(stream);stream.Position=header.DataStartPosition;using(var r=new BinaryReader(stream)){int crossings=0;short prev=0;int samples=0;while(stream.Position+1<stream.Length){var value=r.ReadInt16();if(prev<=0 && value>0)crossings++;prev=value;samples++;}return crossings*header.SampleRate/(double)samples;}}}
 static void WriteWave(string file,bool second){using(var w=new BinaryWriter(File.Create(file))){var n=24000*5;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+n*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(24000);w.Write(48000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n*2);for(int i=0;i<n;i++)w.Write((short)(Math.Sin(2*Math.PI*(second?(i<24000?660:880):440)*i/24000)*12000));}}
}
