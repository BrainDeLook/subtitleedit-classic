using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Forms.BeautifyTimeCodes;
using Profile = Nikse.SubtitleEdit.Core.Settings.BeautifyTimeCodesSettings.BeautifyTimeCodesProfile;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicTimingProfiles
{
 static BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] args){try{return o.GetType().GetMethod(n,F).Invoke(o,args);}catch(TargetInvocationException ex){throw ex.InnerException;}}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static string Json(object o)=>JsonConvert.SerializeObject(o);
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 [STAThread]static int Main(string[] args)
 {
  try{
   var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Console.WriteLine("TEST DATA: "+root);Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int result=0;
   using(var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{await Unit(root);}catch(Exception ex){Console.Error.WriteLine(ex);result=1;}finally{anchor.Close();}};Application.Run(anchor);}return result;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
 static async Task Unit(string root)
 {
  var path=Path.Combine(root,"profiles.json");var lib=new ClassicBeautifyProfiles(path);var profile=new Profile(Profile.Preset.Netflix);profile.ChainingInCueOnShotShotChangeBehavior=Profile.ChainingShotChangeBehaviorEnum.DontChain;
  Check(lib.Read().Count==0,"Missing library starts empty");lib.Save("  Мой профиль  ",profile);var loaded=lib.Read()["МОЙ ПРОФИЛЬ"];Check(Json(loaded)==Json(profile),"All timing values and hidden behaviors survive JSON round-trip; names trim and ignore case");
  loaded.Gap=9;Check(lib.Read()["Мой профиль"].Gap==2,"Loaded profiles are independent copies");
  var before=File.ReadAllBytes(path);try{lib.Save("мой профиль",profile);throw new Exception("Overwrite accepted");}catch(IOException){}Check(before.SequenceEqual(File.ReadAllBytes(path)),"Duplicate name requires explicit overwrite");
  profile.Gap=5;lib.Save("Мой профиль",profile,true);Check(lib.Read().Count==1 && lib.Read()["Мой профиль"].Gap==5 && before.SequenceEqual(File.ReadAllBytes(path+".bak")),"Overwrite saves atomically and retains a backup");
  foreach(var name in new[]{"","\n",new string('a',101)}){try{lib.Save(name,profile);throw new Exception("Invalid name accepted");}catch(ArgumentException){Console.WriteLine("PASS: Invalid profile name rejected");}}
  before=File.ReadAllBytes(path);File.WriteAllText(path,"broken json");try{lib.Save("New",profile);throw new Exception("Corrupt library replaced");}catch(JsonException){}Check(File.ReadAllText(path)=="broken json","Corrupt library is not silently overwritten");File.WriteAllBytes(path,before);
  lib.Delete("мой профиль");Check(lib.Read().Count==0,"Delete removes the selected custom profile only");
  foreach(var language in new[]{"ru-RU","en-US"})
  {
   Configuration.Settings.General.Language=language;LanguageSettings.Current=language=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",language+".xml"));
   Configuration.Settings.BeautifyTimeCodes.Profile=new Profile(Profile.Preset.Default);var original=Json(Configuration.Settings.BeautifyTimeCodes.Profile);
   using(var form=new BeautifyTimeCodesProfile(25))
   {
    Show(form);await Task.Delay(80);
    ((NikseUpDown)Field(form,"numericUpDownGap")).Value=4;
    ((NikseUpDown)Field(form,"numericUpDownOutCuesGap")).Value=2;
    Call(form,"SaveNamedProfile","Test "+language,false);
    var library=(ClassicBeautifyProfiles)Field(form,"_profileLibrary");var saved=library.Read()["Test "+language];
    Check(saved.Gap==4 && saved.OutCuesGap==2 && Json(Configuration.Settings.BeautifyTimeCodes.Profile)==original,"Save captures edited values without applying the working profile: "+language);
    ((NikseUpDown)Field(form,"numericUpDownGap")).Value=8;
    Call(form,"LoadNamedProfile","Test "+language);
    Check(((NikseUpDown)Field(form,"numericUpDownGap")).Value==4 && ((NikseUpDown)Field(form,"numericUpDownOutCuesGap")).Value==2,"Load restores independently configured gaps without general-gap propagation");
    Check(Json(Call(form,"CaptureCurrentProfile"))==Json(saved),"Every loaded editor field round-trips exactly");
    var wide=new Profile(Profile.Preset.Netflix);wide.InCuesLeftRedZone=20;wide.InCuesLeftGreenZone=25;wide.ChainingInCueOnShotShotChangeBehavior=Profile.ChainingShotChangeBehaviorEnum.DontChain;
    library.Save("Wide "+language,wide);Call(form,"LoadNamedProfile","Wide "+language);Check(Json(Call(form,"CaptureCurrentProfile"))==Json(wide),"Loading preserves hidden per-section shot behaviors");
    Call(form,"LoadNamedProfile","Test "+language);Check(Json(Call(form,"CaptureCurrentProfile"))==Json(saved),"Switching from wide to narrow zones does not clamp or overwrite saved values");
    var presetMenu=(ToolStripMenuItem)Field(form,"toolStripMenuItemLoadPreset");Check(presetMenu.DropDownItems.Count>=5,"Built-in presets and custom load entries coexist");
    var menu=(MenuStrip)Field(form,"menuStrip");Check(menu.Items.Cast<ToolStripItem>().All(i=>i.Available && i.Bounds.Right<=menu.ClientSize.Width),"Profile menu actions fit in the classic window");
    using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,form.Width,form.Height));bmp.Save(Path.Combine(root,"timing-profiles-"+language+".png"));}
    form.DialogResult=DialogResult.Cancel;form.Close();Check(Json(Configuration.Settings.BeautifyTimeCodes.Profile)==original,"Cancel leaves active timing settings unchanged");Check(library.Read().ContainsKey("Test "+language),"Saved library entries survive cancelling the editor, matching stable 5.3");
   }
   using(var form=new BeautifyTimeCodesProfile(25))
   {
    Show(form);Call(form,"LoadNamedProfile","Test "+language);Call(form,"buttonOK_Click",form,EventArgs.Empty);
    Check(Configuration.Settings.BeautifyTimeCodes.Profile.Gap==4 && Configuration.Settings.BeautifyTimeCodes.Profile.OutCuesGap==2,"OK applies the selected profile to the engine");
   }
   var snapshot=Json(Configuration.Settings.BeautifyTimeCodes.Profile);
   using(var form=new BeautifyTimeCodesProfileSimple(25,ClassicBeautifyProfiles.Clone(Configuration.Settings.BeautifyTimeCodes.Profile)))
   {
    Show(form);Call(form,"buttonLoadNetflixRules_Click",form,EventArgs.Empty);Call(form,"buttonOK_Click",form,EventArgs.Empty);
    Check(form.ResultProfile.Gap==2 && Json(Configuration.Settings.BeautifyTimeCodes.Profile)==snapshot,"Simple-mode edits remain local until parent OK");
   }
  }
  var src=new Subtitle();src.Paragraphs.Add(new Paragraph("Hello",1015,1985));src.Paragraphs.Add(new Paragraph("World",2090,3020));
  var sourceJson=Json(src);var actual=new Subtitle(src,false);
  Configuration.Settings.BeautifyTimeCodes.AlignTimeCodes=true;Configuration.Settings.BeautifyTimeCodes.SnapToShotChanges=false;
  new TimeCodesBeautifier(actual,25,new List<double>(),new List<double>()).Beautify();
  Check(actual.Paragraphs.Any(p=>p.StartTime.TotalMilliseconds!=src.Paragraphs[actual.Paragraphs.IndexOf(p)].StartTime.TotalMilliseconds || p.EndTime.TotalMilliseconds!=src.Paragraphs[actual.Paragraphs.IndexOf(p)].EndTime.TotalMilliseconds),"Applied profile drives real timing changes on a synthetic subtitle");
  Check(Json(src)==sourceJson && actual.Paragraphs.Select(p=>p.Text).SequenceEqual(src.Paragraphs.Select(p=>p.Text)),"Timing processing preserves input and subtitle text");
 }
}

