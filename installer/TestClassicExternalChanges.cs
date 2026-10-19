using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicExternalChanges
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks,writes;
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,F).SetValue(o,value);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethods(F).Single(m=>m.Name==n && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(o,args);
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();}
    static Subtitle Current(object main)=>(Subtitle)Field(main,"_subtitle");
    static Task<bool> Reload(object main,Func<string,bool> confirm=null)=>(Task<bool>)Call(main,"CheckClassicExternalChange",confirm);
    static string Write(string file,string text,SubtitleFormat format=null)
    {
        var document=new Subtitle{Header=AdvancedSubStationAlpha.DefaultHeader};document.Paragraphs.Add(new Paragraph(text,10000,12000){Extra="Default",Actor="Alice"});document.Paragraphs.Add(new Paragraph("Second line",13000,15000){Extra="Default",Actor="Bob"});document.Renumber();
        var data=(format??new AdvancedSubStationAlpha()).ToText(document,"Test");File.WriteAllText(file,data,Encoding.UTF8);File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddSeconds(++writes));return data;
    }
    static void Open(object main,string file)=>Call(main,"OpenSubtitle",file,Encoding.UTF8);
    [STAThread]static int Main(string[] args)
    {
        var root=Path.Combine(Path.GetFullPath(args[0]),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        typeof(Configuration).GetField("DataDirectory").SetValue(null,root+Path.DirectorySeparatorChar);typeof(Configuration).GetField("SettingsFileName").SetValue(null,Path.Combine(root,"Settings.xml"));
        Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);var exit=0;
        using(var anchor=new Form{ShowInTaskbar=false,Location=new Point(-30000,-30000),StartPosition=FormStartPosition.Manual}){anchor.Shown+=async(s,e)=>{try{await Run(root,args.Length>1?Path.GetFullPath(args[1]):null,args.Length>2?Path.GetFullPath(args[2]):null);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}Console.WriteLine("FEATURE CHECKS: "+checks);return exit;
    }
    static async Task Run(string root,string video,string ffmpeg)
    {
        LanguageSettings.Current=new Language();Configuration.Settings.General.DisableVideoAutoLoading=true;Configuration.Settings.General.DisableShowingLoadErrors=true;
        foreach(var language in new[]{"ru-RU","en-US"})
        {
            Configuration.Settings.General.Language=language;
            var file=Path.Combine(root,"external-"+language+".ass");Write(file,"Initial");
            var first=ClassicSubtitleFileStamp.Read(file);Check(first!=null && first.SameFile(file.ToUpperInvariant()) && first.SameContent(ClassicSubtitleFileStamp.Read(file)),"Stamp tracks the same Windows file by timestamp and length: "+language);
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main);await Task.Delay(140);Open(main,file);main.Hide();var grid=(SubtitleListView)Field(main,"SubtitleListview1");
                Check(((ClassicSubtitleFileStamp)Field(main,"_classicExternalStamp")).SameFile(file),"Successful open immediately remembers the disk stamp: "+language);
                var asked=0;Func<string,bool> no=question=>{asked++;return false;};Func<string,bool> yes=question=>{asked++;return true;};
                Check(!await Reload(main,no) && asked==0,"Unchanged file neither reloads nor prompts: "+language);
                Write(file,"Changed externally");Check(await Reload(main,no) && Current(main).Paragraphs[0].Text=="Changed externally" && asked==0,"A clean document reloads automatically without a prompt: "+language);
                Check((int)Field(main,"_changeSubtitleHash")== (int)Call(main,"GetFastSubtitleHash") && !Current(main).CanUndo,"External reload establishes a clean baseline and fresh history: "+language);
                Current(main).Paragraphs[0].Text="Unsaved local";Call(main,"RefreshSelectedParagraph");Write(file,"New disk revision");
                Check(!await Reload(main,no) && Current(main).Paragraphs[0].Text=="Unsaved local" && asked==1,"No retains unsaved edits: "+language);
                Check(!await Reload(main,no) && asked==1,"Declining is remembered for this exact disk revision: "+language);
                Write(file,"Another disk revision");Check(await Reload(main,yes) && asked==2 && Current(main).Paragraphs[0].Text=="Another disk revision","A newer disk revision asks again and Yes loads it: "+language);
                var kept=Current(main);File.WriteAllText(file,"");Check(!await Reload(main,no) && ReferenceEquals(kept,Current(main)),"Empty in-progress writes retain the current document: "+language);
                File.Delete(file);Check(!await Reload(main,no) && ReferenceEquals(kept,Current(main)),"Missing files retain the document and previous stamp: "+language);
                Write(file,"Recovered disk file");Check(await Reload(main,no) && Current(main).Paragraphs[0].Text=="Recovered disk file","A recreated valid file can be loaded on a later check: "+language);
                var stamp=Field(main,"_classicExternalStamp");kept=Current(main);File.WriteAllText(file,"This is not a subtitle file\nThere are no time codes\n");
                Check(!await Reload(main,no) && ReferenceEquals(kept,Current(main)) && ReferenceEquals(stamp,Field(main,"_classicExternalStamp")),"Unparseable content does not clear the editor or acknowledge the stamp: "+language);
                Write(file,"Retry succeeds");Check(await Reload(main,no),"A failed parse remains retryable after the file becomes valid: "+language);
                Write(file,"Locked revision");using(var locked=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Check(!await Reload(main,no),"A locked file is skipped without replacing the editor: "+language);}Check(await Reload(main,no),"Unlocking allows the same disk revision to be retried: "+language);
                Set(main,"_converted",true);Write(file,"Converted import must not reload");kept=Current(main);Check(!await Reload(main,no) && ReferenceEquals(kept,Current(main)),"Converted imports are not watched: "+language);Set(main,"_converted",false);
                var oldFile=Field(main,"_fileName");Set(main,"_fileName",Path.Combine(root,"different.ass"));Check(!await Reload(main,no),"A stamp cannot reload a different document path: "+language);Set(main,"_fileName",oldFile);
                main.Enabled=false;Check(!await Reload(main,no),"A disabled/modal owner defers reload: "+language);main.Enabled=true;
                Check(await Reload(main,no),"Deferred clean reload proceeds when the editor becomes available: "+language);
                Current(main).Paragraphs[0].Text="Dirty before race";Call(main,"RefreshSelectedParagraph");Write(file,"Disk race revision");
                Check(!await Reload(main,q=>{Current(main).Paragraphs[0].Text="Newer local change";return true;}) && Current(main).Paragraphs[0].Text=="Newer local change","A local edit made during the reload decision rejects the stale result: "+language);
                Check(await Reload(main,yes),"Rejected stale results remain retryable: "+language);
                var original=new Subtitle(Current(main),false);Set(main,"_subtitleOriginal",original);Set(main,"_subtitleOriginalFileName",Path.Combine(root,"original.ass"));grid.ShowOriginalTextColumn("Original");Call(grid,"Fill",Current(main),original);grid.SelectIndexAndEnsureVisible(1);
                Set(main,"_videoFileName","unchanged-video.mkv");Set(main,"_videoAudioTrackNumber",2);var visual=(AudioVisualizer)Field(main,"audioVisualizer");var cuts=visual.ShotChanges;var player=Field(main,"mediaPlayer");
                Configuration.Settings.General.CurrentVideoOffsetInMs=5000;Configuration.Settings.General.CurrentVideoIsSmpte=true;Current(main).AddTimeToAllParagraphs(TimeSpan.FromMilliseconds(-5000));Set(main,"_changeSubtitleHash",Call(main,"GetFastSubtitleHash"));
                Write(file,"Offset reload");Check(await Reload(main,no),"Clean reload supports an active video offset: "+language);
                Check(Current(main).Paragraphs[0].StartTime.TotalMilliseconds==5000 && Configuration.Settings.General.CurrentVideoOffsetInMs==5000 && Configuration.Settings.General.CurrentVideoIsSmpte,"Disk time codes are adjusted once while offset and SMPTE settings remain: "+language);
                Check(ReferenceEquals(original,Field(main,"_subtitleOriginal")) && original.Paragraphs[0].Text=="Disk race revision" && grid.IsOriginalTextColumnVisible,"Reload keeps the original reference and its text: "+language);
                Check((string)Field(main,"_videoFileName")=="unchanged-video.mkv" && (int)Field(main,"_videoAudioTrackNumber")==2 && ReferenceEquals(player,Field(main,"mediaPlayer")) && ReferenceEquals(cuts,visual.ShotChanges),"Reload keeps video, selected audio, player and waveform state: "+language);
                Check(grid.SelectedIndices.Count==1 && grid.SelectedIndices[0]==1 && grid.Items.Cast<ListViewItem>().All(i=>i.SubItems.Count==grid.Columns.Count),"Selected cue and logical column alignment survive reload: "+language);
                var source=(TextBox)Field(main,"textBoxSource");source.Text="Unsaved invalid source buffer";Set(main,"_sourceViewChange",true);var before=asked;Write(file,"Disk after source draft");
                Check(!await Reload(main,no) && asked==before+1 && source.Text=="Unsaved invalid source buffer","Source-view drafts also require confirmation and survive No: "+language);
                Write(file,"Accept over source draft");Check(await Reload(main,yes) && !(bool)Field(main,"_sourceViewChange") && Current(main).Paragraphs[0].Text=="Accept over source draft","Yes discards the source draft only after a valid replacement parses: "+language);
                Configuration.Settings.General.CurrentVideoOffsetInMs=0;Configuration.Settings.General.CurrentVideoIsSmpte=false;Set(main,"_videoFileName",null);Set(main,"_videoAudioTrackNumber",-1);
                var format=Field(main,"_currentSubtitleFormat");Check((DialogResult)Call(main,"SaveSubtitle",format,false,true)==DialogResult.OK,"Own save completes through the real Classic save path: "+language);
                before=asked;Check(!await Reload(main,no) && asked==before,"Own save updates the stamp and cannot trigger an external-change prompt: "+language);
                Write(file,"Changed format",new SubRip());Check(await Reload(main,no) && Field(main,"_currentSubtitleFormat") is SubRip && Current(main).Paragraphs[0].Text=="Changed format","A valid external format change is parsed without converting its contents: "+language);
                var other=Path.Combine(root,"save-as-"+language+".srt");Set(main,"_fileName",other);Check((DialogResult)Call(main,"SaveSubtitle",new SubRip(),false,true)==DialogResult.OK && ((ClassicSubtitleFileStamp)Field(main,"_classicExternalStamp")).SameFile(other),"Saving to a different path moves the remembered stamp to that path: "+language);
                Write(other,"Activation reload",new SubRip());main.Show();main.Activate();Call(main,"OnActivated",EventArgs.Empty);await Task.Delay(350);
                Check(Current(main).Paragraphs[0].Text=="Activation reload","Actual main-window activation triggers the clean reload: "+language);
                if(language=="en-US" && video!=null)
                {
                    var settings=Configuration.Settings.General;settings.VideoPlayer="MPV";settings.MpvExtraOptions="--ao=null --vo=null";settings.VideoPlayerDefaultVolume=0;settings.WaveformAutoGenWhenOpeningVideo=false;settings.FFmpegLocation=ffmpeg;
                    File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"libmpv-2.dll"),Path.Combine(root,"libmpv-2.dll"),true);
                    Call(main,"OpenVideo",video,1);var container=(VideoPlayerContainer)Field(main,"mediaPlayer");
                    var until=DateTime.UtcNow.AddSeconds(15);while(!(container.VideoPlayer is LibMpvDynamic p && p.AudioTracks.Count==2 && p.AudioTrackNumber==1)){if(DateTime.UtcNow>until)throw new TimeoutException("Native MPV did not load the selected track");await Task.Delay(20);}
                    var native=(LibMpvDynamic)container.VideoPlayer;native.Volume=0;native.Pause();native.CurrentPosition=.2;await Task.Delay(150);var position=native.CurrentPosition;
                    Check(native.AudioTrackNumber==1 && native.IsPaused,"Real muted MPV opens the second audio track: "+language);
                    Write(other,"Native player reload",new SubRip());Check(await Reload(main,no),"External reload works while real MPV is loaded: "+language);await Task.Delay(100);
                    Check(ReferenceEquals(native,container.VideoPlayer) && native.AudioTrackNumber==1 && main.VideoAudioTrackNumber==1,"Reload retains the native MPV instance and selected sounding track: "+language);
                    Check(native.IsPaused && Math.Abs(native.CurrentPosition-position)<.1,"Reload retains the real player's pause state and playback position: "+language);
                }
            }
        }
    }
}
