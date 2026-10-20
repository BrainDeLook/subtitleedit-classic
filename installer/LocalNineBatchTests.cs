using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.AutoTranslate;
using Nikse.SubtitleEdit.Core.Translate;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Translate;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.CommandLineConvert;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class LocalNineBatchTests
{
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string name)=>o.GetType().GetField(name,F).GetValue(o);
 static object Call(object o,string name,params object[] args){try{return o.GetType().GetMethod(name,F).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Set(object o,string name,object value)=>o.GetType().GetField(name,F).SetValue(o,value);
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static void Pump(Task task){var end=DateTime.UtcNow.AddMinutes(8);while(!task.IsCompleted){if(DateTime.UtcNow>end)throw new TimeoutException();Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
 sealed class Handler:HttpMessageHandler
 {
  public Func<JObject,CancellationToken,Task<HttpResponseMessage>> Run;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)=>await Run(JObject.Parse(await request.Content.ReadAsStringAsync()),cancel);
  protected override void Dispose(bool disposing){} // Reused by the per-file client reset in this test.
 }
 static HttpResponseMessage Reply(string text)=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(new JObject{["choices"]=new JArray(new JObject{["message"]=new JObject{["content"]=text}})}.ToString(),Encoding.UTF8,"application/json")};
 static Subtitle Sample(int count=3){var s=new Subtitle();for(var i=0;i<count;i++)s.Paragraphs.Add(new Paragraph("This is source sentence number "+i+".",1000+i*3000,3000+i*3000){Actor="Actor"+i,Extra="Default"});return s;}
 static IEnumerable<Control> Desc(Control root){foreach(Control c in root.Controls){yield return c;foreach(var d in Desc(c))yield return d;}}
 [STAThread] static int Main(string[] args){Form anchor=null;try{
  var root=Path.GetFullPath(args[1]);if(args[0]!="native")root=Path.Combine(root,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);foreach(var field in new[]{"SettingsFileName","WaveformsDirectory","AutoBackupDirectory","DictionariesDirectory"}) typeof(Configuration).GetField(field).SetValue(null, field=="SettingsFileName"?Path.Combine(root,"Settings.xml"):Path.Combine(root,field)+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)};SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
  if(args[0]=="native"){var local=ClassicLlamaSettings.Load();local.Variant="cuda13";local.Folder=args[2];local.Save();var s=ClassicTranslationSettings.Load("llama-advanced");s.UseLocalLlama=true;s.Save("llama-advanced");using(var form=NewForm()){var input=Sample(2);input.Paragraphs[0].Text="<i>Hello.\nHow are you today?</i>";input.Paragraphs[1].Text="I am fine. Thank you very much.";using(var engine=new ClassicNewTranslator("llama-advanced")){var output=(Subtitle)Call(form,"RunClassicBatchTranslation",input,engine,"en","ru");foreach(var p in output.Paragraphs)Console.WriteLine(p.Text);Check(output.Paragraphs.All(p=>p.Text.Any(c=>c>='А'&&c<='я')),"Native managed Qwen advanced batch translates both cues into Russian");Check(output.Paragraphs[0].Text.StartsWith("<i>") && output.Paragraphs[0].Text.EndsWith("</i>") && output.Paragraphs[0].Text.Contains("\n"),"Native Qwen preserves italic tags and the internal line break");}}ClassicLlamaServer.Stop();return 0;}
  var result=0;anchor.Shown+=async(sender,eventArgs)=>{try{await Unit();Files(root,false);Files(root,true);Ui(root);}catch(Exception error){Console.Error.WriteLine(error);result=1;}finally{anchor.Close();}};Application.Run(anchor);return result;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{ClassicLlamaServer.Stop();anchor?.Dispose();}}
 static BatchConvert NewForm(){if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA)throw new InvalidOperationException("UI fixture must remain on its STA dispatcher.");var f=new BatchConvert(SystemIcons.Application){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)};f.Show();Application.DoEvents();return f;}
 static async Task Unit()
 {
  var settings=ClassicTranslationSettings.Load("ollama-advanced");settings.BatchSize=2;settings.HistoryLines=2;settings.Context="A boxing documentary";settings.Glossary="Mike = Майк";settings.Save("ollama-advanced");
  int calls=0;var history=new List<int>();var handler=new Handler{Run=(body,cancel)=>{calls++;Check((string)body["response_format"]?["type"]=="json_schema","Advanced requests include a cue-number schema");var input=JObject.Parse((string)body["messages"][1]["content"]);history.Add(input["history"].Count());Check(((string)body["messages"][0]["content"]).Contains("Mike = Майк"),"Glossary and synopsis are included");var output=new JObject();foreach(var line in input["lines"].Reverse())output[(string)line["n"]]="Перевод строки "+(string)line["n"]+".";return Task.FromResult(Reply(output.ToString()));}};
  using(var form=NewForm())using(var engine=new ClassicNewTranslator("ollama-advanced",handler))
  {
   var input=Sample();var result=(Subtitle)Call(form,"RunClassicBatchTranslation",input,engine,"en","ru");Check(calls==2 && history.SequenceEqual(new[]{0,2}),"Real batch conversion splits requests and carries bounded history");Check(result.Paragraphs[0].Text=="Перевод строки 1." && result.Paragraphs[1].Text=="Перевод строки 2.","Out-of-order JSON keys retain cue identity");
   Check(input.Paragraphs[0].Text.StartsWith("This") && result.Paragraphs.Select(p=>p.Actor).SequenceEqual(input.Paragraphs.Select(p=>p.Actor)) && result.Paragraphs[1].StartTime.TotalMilliseconds==input.Paragraphs[1].StartTime.TotalMilliseconds,"Source text, timing and actors survive translation");
   Call(form,"RunClassicBatchTranslation",Sample(1),engine,"en","ru");Check(history.Last()==0,"Next queued file starts with empty history");
  }
  int tries=0;handler=new Handler{Run=(body,cancel)=>{tries++;return Task.FromResult(Reply(tries==1?"{\"1\":\"Потеря второй строки\"}":"{\"2\":\"Вторая.\",\"1\":\"Первая.\"}"));}};
  using(var engine=new ClassicNewTranslator("ollama-advanced",handler)){var result=await engine.TranslateBatchAsync(new[]{"First source sentence.","Second source sentence."},"en","ru",CancellationToken.None);Check(tries==2 && result[1]=="Вторая.","Incomplete JSON retries without shifting cues");}
  foreach(var bad in new[]{"{\"1\":\"Один\",\"1\":\"Другой\"}","{\"1\":\"Removed tags\"}","{\"1\":\"<i>Слитая строка</i>\"}"})
  {
   handler=new Handler{Run=(b,c)=>Task.FromResult(Reply(bad))};using(var engine=new ClassicNewTranslator("ollama-advanced",handler)){bool failed=false;try{await engine.TranslateBatchAsync(new[]{"<i>First sentence.\nSecond sentence.</i>"},"en","ru",CancellationToken.None);}catch(InvalidDataException){failed=true;}Check(failed,"Duplicate keys, lost tags or lost line breaks reject the entire response");}
  }
  tries=0;handler=new Handler{Run=(body,c)=>{tries++;if(tries<3)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest){Content=new StringContent("response_format unsupported")});return Task.FromResult(Reply("{\"1\":\"Перевод.\"}"));}};
  using(var engine=new ClassicNewTranslator("ollama-advanced",handler)){await engine.TranslateBatchAsync(new[]{"A source sentence."},"en","ru",CancellationToken.None);Check(tries==3,"Older compatible servers fall back from schema to JSON to plain output");}
  handler=new Handler{Run=(body,c)=>Task.FromResult(Reply("```json\n{\"1\":\"Перевод.\"}\n```"))};using(var engine=new ClassicNewTranslator("lmstudio-advanced",handler)){Check((await engine.TranslateBatchAsync(new[]{"A source sentence."},"en","ru",CancellationToken.None))[0]=="Перевод.","Plain fallback accepts fenced JSON with the same strict cue validation");}
  using(var form=NewForm())using(var engine=new ClassicNewTranslator("compatible",new Handler{Run=(b,c)=>Task.FromResult(Reply("Первая строка.\nВторая строка."))}))
  {var formattedSource=Sample(1);formattedSource.Paragraphs[0].Text="<i>First source sentence.\nSecond source sentence.</i>";var result=(Subtitle)Call(form,"RunClassicBatchTranslation",formattedSource,engine,"en","ru");Console.WriteLine("FORMATTED: "+result.Paragraphs[0].Text.Replace("\r","[CR]").Replace("\n","[LF]"));Check(result.Paragraphs[0].Text.Replace("\r\n","\n")=="<i>Первая строка.\nВторая строка.</i>","Ordinary modern translator retains formatting through the batch pipeline");}
  using(var form=NewForm())using(var cancel=new CancellationTokenSource())
  {
   Set(form,"_cancellationTokenSource",cancel);var input=Sample();handler=new Handler{Run=async(body,c)=>{await Task.Delay(60000,c);return Reply("{}");}};
   using(var engine=new ClassicNewTranslator("ollama-advanced",handler))using(var timer=new System.Windows.Forms.Timer{Interval=100}){timer.Tick+=(s,e)=>{timer.Stop();cancel.Cancel();};timer.Start();bool stopped=false;try{Call(form,"RunClassicBatchTranslation",input,engine,"en","ru");}catch(OperationCanceledException){stopped=true;}Check(stopped && input.Paragraphs.All(p=>p.Text.StartsWith("This")),"Cancel remains responsive and publishes no partial translated file");}
  }
  var source=Sample(3);var target=new Subtitle(source);handler=new Handler{Run=(body,c)=>Task.FromResult(Reply("{\"1\":\"Один.\",\"2\":\"Два.\"}"))};using(var engine=new ClassicNewTranslator("ollama-advanced",handler)){var count=await MergeAndSplitHelper.MergeAndTranslateIfPossible(source,target,new TranslationPair("English","en"),new TranslationPair("Russian","ru"),0,engine,false,CancellationToken.None);Check(count==2 && target.Paragraphs[2].Text==source.Paragraphs[2].Text,"Ordinary translation uses the same atomic advanced batch adapter");}
 }
 static void Ui(string root)
 {
  using(var form=NewForm())
  {
   var engines=(List<IAutoTranslator>)Field(form,"_autoTranslatorEngines");Check(engines.OfType<ClassicNewTranslator>().Count()==10,"All ten modern engines appear in Batch Convert");var chooser=(Nikse.SubtitleEdit.Controls.NikseComboBox)Field(form,"nikseComboBoxEngine");var group=(Control)Field(form,"groupBoxAutoTranslate");group.Visible=true;
   foreach(var engine in engines.OfType<ClassicNewTranslator>()){chooser.SelectedItem=engine.Name;Application.DoEvents();var button=Desc(form).OfType<Button>().Single(b=>b.Name=="classicBatchTranslateSettings");Check(button.Visible && button.Bottom<=group.ClientSize.Height && button.Width>150,"Classic batch settings fit for "+engine.Name);}
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"BatchConvert.png"));}
  }
  foreach(var id in new[]{"llama-advanced","ollama-advanced","lmstudio-advanced"})using(var form=new ClassicTranslationForm(id)){form.ShowInTaskbar=false;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();Check(Desc(form).OfType<TextBox>().Any(t=>t.Name=="batchSize"),"Advanced settings expose batch and history controls for "+id);using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,id+".png"));}form.Close();}
 }
 static void Files(string root,bool fail)
 {
  var folder=Path.Combine(root,fail?"failed-files":"translated-files");Directory.CreateDirectory(folder);var inputs=Path.Combine(folder,"input");var outputs=Path.Combine(folder,"output");Directory.CreateDirectory(inputs);Directory.CreateDirectory(outputs);
  var requests=0;var history=new List<int>();var handler=new Handler{Run=(body,c)=>{requests++;var data=JObject.Parse((string)body["messages"][1]["content"]);history.Add(data["history"].Count());if(fail)return Task.FromResult(Reply("{}"));var output=new JObject();foreach(var line in data["lines"])output[(string)line["n"]]="Проверенная русская реплика.";return Task.FromResult(Reply(output.ToString()));}};
  using(var form=NewForm())
  {
   ((ClassicBatch53Options)Field(form,"_batch53")).TargetLanguages="ru,fr"; ((ClassicBatch53Options)Field(form,"_batch53")).PreserveSourceTimestamp=true;
   var engines=(List<IAutoTranslator>)Field(form,"_autoTranslatorEngines");var at=engines.FindIndex(e=>e is ClassicNewTranslator t && t.Id=="ollama-advanced");var previous=(IDisposable)engines[at];previous.Dispose();engines[at]=new ClassicNewTranslator("ollama-advanced",handler);
   ((Nikse.SubtitleEdit.Controls.NikseComboBox)Field(form,"nikseComboBoxEngine")).SelectedItem=engines[at].Name;
   foreach(ListViewItem item in ((ListView)Field(form,"listViewConvertOptions")).Items)item.Checked=((BatchConvert.FixActionItem)item.Tag).Action==CommandLineConverter.BatchAction.AutoTranslate;
   ((RadioButton)Field(form,"radioButtonSaveInOutputFolder")).Checked=true;((TextBox)Field(form,"textBoxOutputFolder")).Text=outputs;
   AutoTranslate.SelectLanguageCode((Nikse.SubtitleEdit.Controls.NikseComboBox)Field(form,"comboBoxSource"),"en");AutoTranslate.SelectLanguageCode((Nikse.SubtitleEdit.Controls.NikseComboBox)Field(form,"comboBoxTarget"),"ru");
   for(var i=0;i<2;i++){var path=Path.Combine(inputs,"sample"+i+".srt");File.WriteAllText(path,Sample(2).ToText(new SubRip()));File.SetLastWriteTimeUtc(path,new DateTime(2001,2,3,4,5,6,DateTimeKind.Utc));Call(form,"AddInputFile",path);}
   Call(form,"buttonConvert_Click",null,EventArgs.Empty);
   var written=Directory.GetFiles(outputs,"*.srt");Check(fail?written.Length==0:written.Length==4,"Real Convert button "+(fail?"does not save failed translations":"writes all files in both target languages"));
   Check(requests==(fail?4:4) && history.All(n=>n==0),"Queued files use independent context and bounded retries");
   if(!fail)Check(written.All(p=>File.GetLastWriteTimeUtc(p)==new DateTime(2001,2,3,4,5,6,DateTimeKind.Utc)),"Each translated target preserves the source timestamp");
   if(!fail)Check(written.All(p=>File.ReadAllText(p).Contains("Проверенная русская реплика.")),"Saved files contain translated text");
  }
 }
}
