using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using C = Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicFormatColumns
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static object Field(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethods(F).Single(m => m.Name == n && m.GetParameters().Length == args.Length && m.GetParameters().Select((p,i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(v => v)).Invoke(o, args);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
    static void Show(Form f) { f.Location = new Point(-20000, -20000); f.StartPosition = FormStartPosition.Manual; f.ShowInTaskbar = false; f.Show(); }
    static string Cell(SubtitleListView g, C c, int row = 0) => g.Items[row].SubItems[g.GetColumnIndex(c)].Text;
    [STAThread] static int Main(string[] args)
    {
        var root = Path.Combine(Path.GetFullPath(args[0]), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        typeof(Configuration).GetField("DataDirectory").SetValue(null, root + Path.DirectorySeparatorChar);
        typeof(Configuration).GetField("SettingsFileName").SetValue(null, Path.Combine(root, "Settings.xml"));
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles(); var exit = 0;
        using (var anchor = new Form { ShowInTaskbar = false, Location = new Point(-30000, -30000), StartPosition = FormStartPosition.Manual })
        {
            anchor.Shown += async (s, e) => { try { await Run(root); } catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; } finally { anchor.Close(); } };
            Application.Run(anchor);
        }
        Console.WriteLine("FEATURE CHECKS: " + checks); return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current = new Language();
        Check(ClassicFormatColumnValues.WebVttClasses("<c.red.loud>A</c><c.red>B</c>") == "red, loud", "WebVTT class order and duplicate removal match stable 5.3");
        Check(ClassicFormatColumnValues.WebVttClasses("<c.a-b_2.#fff>A</c>") == "a-b_2, #fff", "Hyphens underscores digits and color names are supported");
        Check(ClassicFormatColumnValues.WebVttClasses(null) == "" && ClassicFormatColumnValues.WebVttClasses("<c.invalid space>") == "", "Absent or malformed class markup is safe");
        Check(ClassicFormatColumnValues.WebVttVoice("<v  Alice Smith >Hi</v><v Bob>Bye") == "Alice Smith", "Voice displays the first speaker and trims tag whitespace");
        Check(ClassicFormatColumnValues.WebVttVoice("<v broken") == "" && ClassicFormatColumnValues.WebVttVoice(null) == "", "Missing or incomplete voice markup is safe");
        foreach (var align in new[]{1,2,3,4,5,6,7,8,9})
        {
            var p = new Paragraph(@"{\an"+align+"}Cue",1000,3000){MarginV="23"};
            var expected = (align==1||align==4||align==7)?"L":(align==3||align==6||align==9)?"R":"C";
            Check(ClassicFormatColumnValues.Teletext(p)=="23 "+expected,"Teletext alignment mapping "+align);
        }
        foreach(var margin in new[]{"0","24","-1","invalid",null})
            Check(ClassicFormatColumnValues.Teletext(new Paragraph("Cue",1000,3000){MarginV=margin})=="","Invalid or missing row is not invented: "+margin);
        Check(ClassicFormatColumnValues.Teletext(new Paragraph("Cue",1000,3000){MarginV="1"})=="1 C","Valid row without an alignment uses center");
        Check(ClassicFormatColumnValues.Teletext(new Paragraph(@"{\an3}Cue",1000,3000))=="R","Explicit alignment survives without a row");
        using(var grid=new SubtitleListView())
        {
            grid.UseWebVttMetadata=true;grid.ShowDetailColumn(C.Extra,"Style (WebVTT)");grid.ShowDetailColumn(C.Actor,"Voice (WebVTT)");grid.ShowDetailColumn(C.Teletext,"Teletext");
            var sub=new Subtitle();var cue=new Paragraph("<v Alice><c.red>One</c>",1000,3000){Extra="untouched",Actor="metadata",MarginV="20"};sub.Paragraphs.Add(cue);sub.Renumber();Call(grid,"Fill",sub,null);
            Check(Cell(grid,C.Extra)=="red" && Cell(grid,C.Actor)=="Alice" && Cell(grid,C.Teletext)=="20 C","Grid derives format values without replacing stored metadata");
            cue.Text="<v Bob><c.blue.loud>Two</c>";grid.SetText(0,cue.Text);
            Check(Cell(grid,C.Extra)=="blue, loud" && Cell(grid,C.Actor)=="Bob","Existing text-edit path updates class and voice cells immediately");
            cue.Text=@"{\an1}Three";cue.MarginV="21";grid.SetTimeAndText(0,cue,null);
            Check(Cell(grid,C.Teletext)=="21 L" && Cell(grid,C.Actor)=="" && Cell(grid,C.Extra)=="","Teletext edits and removed WebVTT tags clear prior values");
            Check(cue.Actor=="metadata" && cue.Extra=="untouched" && cue.StartTime.TotalMilliseconds==1000,"Display preserves source metadata and timing");
        }
        foreach(var language in new[]{"en-US","ru-RU"})
        {
            Configuration.Settings.General.Language=language;
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main);await Task.Delay(120);var grid=(SubtitleListView)Field(main,"SubtitleListview1");
                var sub=new Subtitle();var cue=new Paragraph("<v Alice><c.red.loud>Sample</c>",1000,3000){MarginV="20"};sub.Paragraphs.Add(cue);sub.Renumber();Set(main,"_subtitle",sub);
                var preferences=ClassicColumnPreference.Normalize(null,c=>c==C.Start||c==C.End||c==C.Extra||c==C.Actor||c==C.Teletext);
                Call(main,"SetCurrentFormat",new WebVTT().FriendlyName);Call(main,"ApplyClassicColumns",preferences,true);
                Check(grid.UseWebVttMetadata && Cell(grid,C.Extra)=="red, loud" && Cell(grid,C.Actor)=="Alice","WebVTT maps existing Style/Actor preferences to cue classes/voice: "+language);
                Check(grid.ColumnIndexTeletext<0 && grid.Items[0].SubItems.Count==grid.Columns.Count,"Non-EBU hides Teletext without misaligning data: "+language);
                grid.SelectIndexAndEnsureVisible(0);Call(main,"WebVTTSetVoice","Bob");
                Check(Cell(grid,C.Actor)=="Bob" && cue.Text.Contains("<v Bob>"),"Existing voice editor updates derived grid metadata: "+language);
                var format=new WebVTT();var serialized=format.ToText(sub,"Fixture");var loaded=new Subtitle();format.LoadSubtitle(loaded,serialized.SplitToLines(),"fixture.vtt");
                Check(loaded.Paragraphs.Count==1 && ClassicFormatColumnValues.WebVttVoice(loaded.Paragraphs[0].Text)=="Bob" && ClassicFormatColumnValues.WebVttClasses(loaded.Paragraphs[0].Text)=="red, loud","Real WebVTT round trip preserves displayed voice and classes: "+language);
                File.WriteAllText(Path.Combine(root,"format-columns-sample.vtt"),serialized);
                Call(main,"SetCurrentFormat",new AdvancedSubStationAlpha().FriendlyName);sub=(Subtitle)Field(main,"_subtitle");cue=sub.Paragraphs[0];cue.Extra="Default";cue.Actor="ASS actor";Call(grid,"Fill",sub,null);
                Check(!grid.UseWebVttMetadata && Cell(grid,C.Extra)=="Default" && Cell(grid,C.Actor)=="ASS actor","Leaving WebVTT restores ordinary ASS style and actor data: "+language);
                Call(main,"SetCurrentFormat",new WebVTT().FriendlyName);
                Check(grid.UseWebVttMetadata && Cell(grid,C.Actor)==ClassicFormatColumnValues.WebVttVoice(((Paragraph)grid.Items[0].Tag).Text) && grid.Items[0].SubItems.Count==grid.Columns.Count,"Returning to WebVTT restores aliases and aligns subitems: "+language);
                var hidden=preferences.Select(p=>p.Copy()).ToList();hidden.Single(p=>p.Key=="Actor").Visible=false;hidden.Single(p=>p.Key=="Extra").Visible=false;Call(main,"ApplyClassicColumns",hidden,false);
                Check(grid.ColumnIndexActor<0 && grid.ColumnIndexExtra<0,"Explicitly hidden WebVTT metadata stays hidden: "+language);
                Call(main,"SetCurrentFormat",new Ebu().FriendlyName);sub=(Subtitle)Field(main,"_subtitle");cue=sub.Paragraphs[0];cue.Text=@"{\an3}Teletext cue";cue.MarginV="22";Call(main,"ApplyClassicColumns",preferences,true);
                Check(grid.ColumnIndexTeletext>=0 && Cell(grid,C.Teletext)=="22 R","EBU STL shows row and alignment from the real paragraph: "+language);
                cue.MarginV="19";cue.Text=@"{\an1}Updated";grid.SetTimeAndText(0,cue,null);Check(Cell(grid,C.Teletext)=="19 L","EBU positioning edits refresh the table: "+language);
                var sample=new Subtitle();sample.Paragraphs.Add(new Paragraph(@"{\an1}Teletext left",1000,3000));sample.Paragraphs.Add(new Paragraph(@"{\an3}Teletext right",4000,6000));sample.Renumber();
                var ebuPath=Path.Combine(root,"format-columns-sample.stl");var ebu=new Ebu();Check(ebu.Save(ebuPath,sample,true),"Real EBU STL fixture exports without a dialog: "+language);
                var decoded=new Subtitle();ebu.LoadSubtitle(decoded,File.ReadAllBytes(ebuPath));Check(decoded.Paragraphs.Count==2 && ClassicFormatColumnValues.Teletext(decoded.Paragraphs[0]).EndsWith(" L") && ClassicFormatColumnValues.Teletext(decoded.Paragraphs[1]).EndsWith(" R"),"Teletext display reads actual binary STL row and alignment: "+language);
                using(var dialog=new ClassicGridColumns(preferences,c=>(string)Call(main,"ColumnLabel",c),c=>(string)Call(main,"ColumnNote",c),()=>preferences))
                {Show(dialog);dialog.Size=dialog.MinimumSize;var table=(DataGridView)Field(dialog,"_grid");table.FirstDisplayedScrollingRowIndex=preferences.FindIndex(p=>p.Key=="Teletext");using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(root,language+".png"));}Check(table.Rows.Count==Enum.GetValues(typeof(C)).Length,"Dialog lists Teletext with all grid columns: "+language);}
                Call(main,"SetCurrentFormat",new SubRip().FriendlyName);Check(grid.ColumnIndexTeletext<0,"SRT hides Teletext: "+language);
                Call(main,"SetCurrentFormat",new Ebu().FriendlyName);Check(grid.ColumnIndexTeletext>=0,"Returning to EBU restores enabled Teletext: "+language);
            }
            using(var reopened=new Nikse.SubtitleEdit.Forms.Main())
            {var prefs=(List<ClassicColumnPreference>)Field(reopened,"_classicColumns");Check(prefs.Single(p=>p.Key=="Teletext").Visible,"Restart retains Teletext preference: "+language);}
        }
    }
}
