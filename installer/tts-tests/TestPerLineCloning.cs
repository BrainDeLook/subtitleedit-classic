using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestPerLineCloning
{
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object value)=>o.GetType().GetField(n,F).SetValue(o,value);
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Pump(Task task){var until=DateTime.UtcNow.AddMinutes(4);while(!task.IsCompleted){if(DateTime.UtcNow>until)throw new TimeoutException();Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();}
 [STAThread]static int Main(string[] args){try{
  var data=Path.GetFullPath(args[1]);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,data+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
  var native=args[0]=="native";var wave=Path.Combine(data,"source.wav");var sub=new Subtitle();
  if(native){Pump(ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(args[2],"-nostdin -y -i "+ClassicSpeechRunner.Quote(args[3])+" -i "+ClassicSpeechRunner.Quote(args[4])+" -filter_complex [0:a][1:a]concat=n=2:v=0:a=1[out] -map [out] -ar 24000 -ac 1 "+ClassicSpeechRunner.Quote(wave)),CancellationToken.None,null));var seconds=ClassicVoiceAssets.WaveSeconds(args[3]);sub.Paragraphs.Add(new Paragraph("English translated test",0,seconds*1000));sub.Paragraphs.Add(new Paragraph("Русский перевод для проверки",seconds*1000,ClassicVoiceAssets.WaveSeconds(wave)*1000));}
  else{WriteWave(wave);sub.Paragraphs.Add(new Paragraph("Translated A",0,300));sub.Paragraphs.Add(new Paragraph("Translated B",300,600));}
  var range=ClassicPerLineReference.Range(sub.Paragraphs[0],sub.Paragraphs);Check(range.Item1==0 && (native || range.Item2<=.301),"reference expansion never crosses the next subtitle");
  var single=ClassicPerLineReference.Range(new Paragraph("Short",2000,2200),new Paragraph[0]);Check(single.Item2>=2.99 && single.Item1>=0,"isolated short cue expands towards three seconds");
  Check(ClassicLocalTts.Catalog.Where(d=>d.SupportsAudioOnlyPerLineClone).Select(d=>d.Id).OrderBy(s=>s).SequenceEqual(new[]{"breeze","fish","higgs","index25"}),"audio-only mode is limited to supported per-request engines");
  Check(ClassicLocalTts.Catalog.First(d=>d.Id=="firered").RequiresReferenceTranscript,"FireRed still requires the source transcript");
  var settings=ClassicLocalTtsSettings.Load("higgs");settings.Variant="cuda";settings.Model=ClassicLocalTts.Catalog.First(d=>d.Id=="higgs").Models[0].Key;settings.Language="en";settings.LicenseAccepted=ClassicAudioCpp.License("higgs")[0];settings.Save("higgs");
  using(var form=new TextToSpeech(sub,new SubRip(),wave,new VideoInfo{TotalSeconds=ClassicVoiceAssets.WaveSeconds(wave)},0)){
   form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var combo=(Control)Field(form,"nikseComboBoxEngine");combo.Text=ClassicLocalTts.Catalog.First(d=>d.Id=="higgs").Name;
   bool activated=false;using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=(s,e)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Controls.Find("cloneEachLine",true).Length>0);if(dialog==null)return;timer.Stop();((CheckBox)dialog.Controls.Find("cloneEachLine",true).Single()).Checked=true;((CheckBox)dialog.Controls.Find("cloneConsent",true).Single()).Checked=true;((Button)dialog.Controls.Find("applyVideoVoices",true).Single()).PerformClick();activated=Field(form,"_perLineOriginal")!=null;dialog.Size=dialog.MinimumSize;Application.DoEvents();var top=dialog.Controls.Find("videoVoiceOptions",true).Single();Check(top.Controls.Cast<Control>().All(c=>c.Bottom<=top.ClientSize.Height && c.Right<=top.ClientSize.Width),"classic clone options fit minimum window");using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(data,"per-line-options.png"));}dialog.Close();};timer.Start();form.GetType().GetMethod("EditVideoVoices",F).Invoke(form,null);}
   Check(activated && ClassicCastSettings.Load().PerLineAudioOnly,"audio-only cloning activates without an original subtitle file");
   var timeline=(Subtitle)Field(form,"_subtitle");Set(form,"_waveFolder",Path.Combine(data,"render"));Directory.CreateDirectory(Path.Combine(data,"render"));
   var ids=new System.Collections.Generic.List<string>();
   foreach(var cue in timeline.Paragraphs){var task=(Task<string>)form.GetType().GetMethod("PerLineReferenceAsync",F).Invoke(form,new object[]{cue,CancellationToken.None,"higgs"});Pump(task);ids.Add(task.Result);var path=Path.Combine(ClassicQwenReference.Folder,task.Result+".wav");Check(ClassicVoiceAssets.WaveSeconds(path)>=.99,"short references are padded to at least one second");Check(File.ReadAllText(Path.ChangeExtension(path,".txt"))=="","translated subtitle is never substituted for source transcript");}
   Check(ids.Distinct().Count()==2 && timeline.Paragraphs[0].Text==sub.Paragraphs[0].Text,"each line receives its own reference without changing target text");
   if(native){using(var engine=new ClassicLocalTts("higgs",settings)){Process server=null;for(int i=0;i<2;i++){settings.Language=i==0?"en":"ru";var text=i==0?"Today we are checking the voice for this sentence.":"Сегодня мы проверяем голос для этой фразы.";var file=Path.Combine(data,i==0?"english.wav":"russian.wav");Pump(engine.SpeakAsync(text,ids[i],file,CancellationToken.None));Check(ClassicVoiceAssets.WaveSeconds(file)>1,"real audio-only per-line synthesis "+settings.Language);var current=(Process)Field(engine,"_server");if(server!=null)Check(ReferenceEquals(server,current),"changing reference does not reload the model");server=current;}}}
   form.GetType().GetMethod("CleanupPerLineReferences",F).Invoke(form,null);Check(ids.All(id=>!File.Exists(Path.Combine(ClassicQwenReference.Folder,id+".wav"))),"temporary per-line voices are cleaned after generation");Check(Directory.GetFiles(Path.Combine(data,"render","session-refs"),"*.wav").Length==2,"session keeps independent per-line reference copies");form.Close();
  }
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void WriteWave(string file){using(var w=new BinaryWriter(File.Create(file))){var n=24000*5;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+n*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(24000);w.Write(48000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n*2);for(int i=0;i<n;i++)w.Write((short)(Math.Sin(2*Math.PI*440*i/24000)*10000));}}
}
