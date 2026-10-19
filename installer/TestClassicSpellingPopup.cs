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
class TestClassicSpellingPopup
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
        typeof(Configuration).GetField("DictionariesDirectory").SetValue(null, Path.Combine(root,"Dictionaries")+Path.DirectorySeparatorChar);Directory.CreateDirectory(Configuration.DictionariesDirectory);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles(); var exit = 0;
        using (var anchor = new Form { ShowInTaskbar = false, Location = new Point(-30000, -30000), StartPosition = FormStartPosition.Manual })
        {
            anchor.Shown += async (s, e) => { try { await Run(root,Path.GetFullPath(args[1])); } catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; } finally { anchor.Close(); } };
            Application.Run(anchor);
        }
        Console.WriteLine("FEATURE CHECKS: " + checks); return exit;
    }
    static async Task Run(string root,string sources)
    {
        LanguageSettings.Current=new Language();Configuration.Settings.WordLists.UseOnlineNames=false;
        var folder=Configuration.DictionariesDirectory;
        foreach(var name in new[]{"ru_RU.aff","ru_RU.dic","en_US.aff","en_US.dic"})File.Copy(Path.Combine(sources,name),Path.Combine(folder,name));
        using(var checker=new ClassicGridSpelling())
        {
            checker.Load(Path.Combine(folder,"ru_RU"));
            Check(checker.Check("Привет мир. Это правильное русское предложение.").Count==0,"Bundled real Russian dictionary accepts a normal sentence");
            Check(checker.DoSpell("русские") && checker.DoSpell("словами"),"Real Russian affix rules accept inflected words");
            var wrong=checker.Check("превет мир");Check(wrong.Count==1 && wrong[0].Text=="превет","Russian typo is detected with the bundled full dictionary");
            Check(checker.Suggest("превет").Contains("привет"),"Native Russian suggestions include привет");
            checker.Load(Path.Combine(folder,"en_US"));Check(checker.Check("Hello world").Count==0,"English checking remains available after a Russian dictionary");
        }
        foreach(var language in new[]{"ru-RU","en-US"})
        {
        Configuration.Settings.General.Language=language;
        using(var main=new Nikse.SubtitleEdit.Forms.Main())
        {
            Show(main);await Task.Delay(130);
            var grid=(SubtitleListView)Field(main,"SubtitleListview1");var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("<i>Это превет и русский текст.</i>",1000,4000));sub.Renumber();Set(main,"_subtitle",sub);Call(grid,"Fill",sub,null);grid.SelectIndexAndEnsureVisible(0);
            Call(main,"SaveGridSpelling",true,null);Check(grid.GridSpelling!=null && grid.GridSpelling.Dictionary.EndsWith("ru_RU"),"Automatic mode selects bundled Russian for Russian subtitles: "+language);
            using(var image=new Bitmap(grid.Width,grid.Height))grid.DrawToBitmap(image,new Rectangle(Point.Empty,grid.Size));
            var hit=grid.FirstSpellingHit(0);Check(hit!=null && hit.Word=="превет","Real owner-drawn grid marks the Russian typo: "+language);
            Call(main,"BuildGridSpellMenu",hit,0,false);var block=(ToolStripMenuItem)Field(main,"_gridSpellContext");var popup=block.DropDown;
            Check(popup.Items[0] is ToolStripLabel && popup.Items[0].Text.Contains("превет") && popup.Items[0].Font.Bold,"Popup has a bold word-specific heading: "+language);
            var suggestions=popup.Items.OfType<ToolStripMenuItem>().Where(i=>i.Tag as string=="suggestion").ToList();
            Check(suggestions.Count>0 && suggestions.Any(i=>i.Text=="привет") && suggestions[0].Font.Bold,"Corrections are top-level and the first suggestion is emphasized: "+language);
            Check(popup.Items[popup.Items.Count-1].Name=="classicStandardGridMenu","Standard subtitle menu button is the final popup action: "+language);
            var standard=(ContextMenuStrip)Field(main,"contextMenuStripListView");Check(!standard.Items.Contains(block),"Standard menu does not contain a nested spelling block: "+language);
            popup.Size=popup.GetPreferredSize(Size.Empty);popup.PerformLayout();
            using(var image=new Bitmap(Math.Max(1,popup.Width),Math.Max(1,popup.Height))){popup.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"spelling-popup-"+language+".png"));}
            // Hide this test window before invoking asynchronous popup routing; no native menus appear on the user's desktop.
            main.Hide();Call(grid,"OnKeyDown",new KeyEventArgs(Keys.Apps));var opening=new System.ComponentModel.CancelEventArgs();Call(standard,"OnOpening",opening);
            Check(opening.Cancel,"Right-click/keyboard on an error routes away from the standard menu: "+language);
            Set(main,"_gridSpellBypass",true);var ordinary=new System.ComponentModel.CancelEventArgs();Call(standard,"OnOpening",ordinary);
            Check(!ordinary.Cancel,"Fallback bypass permits the standard menu without a popup loop: "+language);Set(main,"_gridSpellBypass",false);
            Call(main,"BuildGridSpellMenu",hit,0,false);suggestions=block.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Tag as string=="suggestion").ToList();suggestions.Single(i=>i.Text=="привет").PerformClick();
            Check(sub.Paragraphs[0].Text=="<i>Это привет и русский текст.</i>","Top-level Russian suggestion corrects the cue and retains tags: "+language);
            Call(main,"UndoToIndex",true);var restored=(Subtitle)Field(main,"_subtitle");Check(restored.Paragraphs[0].Text==hit.Source,"Russian popup correction is undone in one step: "+language);
            Call(main,"UndoToIndex",false);restored=(Subtitle)Field(main,"_subtitle");Check(restored.Paragraphs[0].Text.Contains("привет"),"Russian correction supports Redo: "+language);
            Call(main,"BuildGridSpellMenu",hit,0,true);Check(block.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Enabled).All(i=>i.Name=="classicStandardGridMenu"),"Reference popup offers only the standard-menu fallback: "+language);
            Call(main,"SaveGridSpelling",true,"en_US");Check(grid.GridSpelling.Dictionary.EndsWith("en_US"),"Manual English selection is preserved: "+language);
            Call(main,"SaveGridSpelling",true,"ru_RU");Check(grid.GridSpelling.Dictionary.EndsWith("ru_RU"),"Manual Russian selection works: "+language);
            using(var dialog=new ClassicGridSpellingSettings(true,"ru_RU")){Check(dialog.DictionaryResult=="ru_RU","Settings lists the bundled Russian dictionary: "+language);}
        }
        }
    }
}
