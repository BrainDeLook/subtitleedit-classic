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
class TestClassicGridSpelling
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
            anchor.Shown += async (s, e) => { try { await Run(root); } catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; } finally { anchor.Close(); } };
            Application.Run(anchor);
        }
        Console.WriteLine("FEATURE CHECKS: " + checks); return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current=new Language();Configuration.Settings.WordLists.UseOnlineNames=false;Configuration.Settings.General.Language="en-US";
        var folder=Configuration.DictionariesDirectory;
        var installedDictionary=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Subtitle Edit","Dictionaries","en_US");
        if(File.Exists(installedDictionary+".aff") && File.Exists(installedDictionary+".dic"))
            foreach(var ext in new[]{".dic",".aff"})File.Copy(installedDictionary+ext,Path.Combine(folder,"en_US"+ext));
        else
        {
            File.WriteAllText(Path.Combine(folder,"en_US.aff"),"SET UTF-8\nTRY esiarntolcdugmphbyfvkwzxjq\n",new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder,"en_US.dic"),"5\nhello\nworld\nword\nspelling\ncorrect\n",new System.Text.UTF8Encoding(false));
        }
        File.WriteAllText(Path.Combine(folder,"ru_RU.aff"),"SET UTF-8\nTRY оеаинтсрвлкмдпуяыьгзбчйхжюшцщэфъё\n",new System.Text.UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder,"ru_RU.dic"),"4\nпривет\nмир\nпроверка\nслово\n",new System.Text.UTF8Encoding(false));
        using(var spell=new ClassicGridSpelling())
        {
            spell.Load(Path.Combine(folder,"en_US"));Check(spell.Ready && spell.Check("hello world").Count==0,"Real Hunspell accepts correct English");
            var wrong=spell.Check("hello speling world");Check(wrong.Count==1 && wrong[0].Text=="speling" && wrong[0].Index==6,"Misspelled word positions are exact");
            Check(spell.Suggest("speling").Contains("spelling"),"Native suggestions include a useful correction");
            Check(spell.Check("hello https://mispeling.qxz user@mispeling.qxz #mispeling 1234").Count==0,"URLs mail hashtags and numbers are skipped");
            Check(spell.Check(@"{\fnMispeling\pos(20,30)}<font face='Mispeling'>hello</font>").Count==0,"HTML and ASS tag contents are not checked");
            spell.Ignore("speling");Check(spell.Check("speling").Count==0,"Ignore all clears cached mistakes");spell.Load(Path.Combine(folder,"en_US"));Check(spell.Check("speling").Count==0,"Reapplying the same dictionary retains session ignores");
            spell.Load(Path.Combine(folder,"ru_RU"));Check(spell.Check("привет мир").Count==0 && spell.Check("превет мир").Count==1,"UTF-8 Cyrillic dictionary changes language correctly");
            spell.Load(Path.Combine(folder,"en_US"));Check(spell.Check("speling").Count==1,"Changing dictionary clears session ignores");
            spell.AddWord("subtitlewidgetzz",false);Check(spell.Check("subtitlewidgetzz").Count==0,"Added user word is immediately accepted");
            spell.AddWord("Braindelookzz",true);Check(spell.Check("Braindelookzz").Count==0,"Added proper name is immediately accepted");
            using(var reopened=new ClassicGridSpelling()){reopened.Load(Path.Combine(folder,"en_US"));Check(reopened.Check("subtitlewidgetzz Braindelookzz").Count==0,"User words and names persist through a new checker");}
            var watch=Stopwatch.StartNew();for(var i=0;i<10000;i++)spell.Check("hello speling world");watch.Stop();Check(watch.ElapsedMilliseconds<1000,"Cached checking of 10,000 rows remains under one second ("+watch.ElapsedMilliseconds+" ms)");
            var painter=new ClassicGridPainter();
            foreach(ClassicGridFormattingMode mode in Enum.GetValues(typeof(ClassicGridFormattingMode)))
            foreach(var rtl in new[]{false,true})
            {
                using(var image=new Bitmap(600,70))using(var g=Graphics.FromImage(image))using(var font=new Font("Arial",14))
                {
                    g.Clear(Color.White);var hits=new List<ClassicGridSpellHit>();var source="<i>speling</i> world";
                    painter.Draw(g,new Rectangle(5,5,580,55),source,font,Color.Black,Color.White,false,rtl,mode," || ",spell.Check,h=>hits.Add(h));
                    Check(hits.Count>0 && hits.All(h=>h.Word=="speling"),"Correct word hit geometry in "+mode+" RTL="+rtl);
                    Check(hits.All(h=>h.Bounds.Left>=5 && h.Bounds.Right<=585),"Spelling underline stays inside cell in "+mode+" RTL="+rtl);
                    Check(hits[0].Replace("spelling")=="<i>spelling</i> world","Corrections retain tags in "+mode+" RTL="+rtl);
                    image.Save(Path.Combine(root,"spelling-"+mode+"-"+rtl+".png"));
                }
            }
            foreach(var source in new[]{"<i>spe</i><b>ling</b>","sp&#101;ling"})
            {
                using(var image=new Bitmap(400,60))using(var g=Graphics.FromImage(image))using(var font=new Font("Arial",14))
                {
                    var hits=new List<ClassicGridSpellHit>();painter.Draw(g,new Rectangle(0,0,390,55),source,font,Color.White,Color.DarkBlue,true,false,ClassicGridFormattingMode.ShowFormatting," || ",spell.Check,h=>hits.Add(h));
                    Check(hits.Count>0 && hits.All(h=>h.Word=="speling"),"A word split by styling or entities is checked as one: "+source);
                    var corrected=hits[0].Replace("spelling");var visible=string.Concat(ClassicGridMarkup.Parse(corrected,ClassicGridFormattingMode.HideTags).Select(r=>r.Text));
                    Check(visible=="spelling","Correction maps rendered text back into exact source ranges: "+source);
                    Check(!source.Contains("<i>") || (corrected.Contains("<i>") && corrected.Contains("</i>") && corrected.Contains("<b>") && corrected.Contains("</b>")),"Spelling correction preserves nested tag structure");
                }
            }
        }
        foreach(var language in new[]{"en-US","ru-RU"})
        {
            Configuration.Settings.General.Language=language;Configuration.Settings.Tools.LiveSpellCheck=false;Configuration.Settings.General.SubtitleTextBoxSyntaxColor=false;
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main);await Task.Delay(130);var grid=(SubtitleListView)Field(main,"SubtitleListview1");var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("<i>speling</i> world",1000,3000){Actor="Alice",Extra="Default",Layer=2});sub.Renumber();Set(main,"_subtitle",sub);Call(grid,"Fill",sub,null);grid.SelectIndexAndEnsureVisible(0);
                Call(main,"SaveGridSpelling",true,"en_US");Check(grid.GridSpelling!=null && grid.GridSpelling.Ready,"Grid spelling works independently of editor spelling and syntax coloring: "+language);
                Call(main,"SaveGridSpelling",true,"en_US");Check(grid.GridSpelling!=null,"Repeated settings apply retains the checker: "+language);
                Call(main,"SaveGridSpelling",true,null);Check(grid.GridSpelling!=null && grid.GridSpelling.Dictionary.EndsWith("en_US"),"Automatic mode picks the installed English dictionary: "+language);
                Call(main,"SaveGridSpelling",true,"en_US");
                using(var image=new Bitmap(grid.Width,grid.Height)){grid.DrawToBitmap(image,new Rectangle(Point.Empty,grid.Size));image.Save(Path.Combine(root,"grid-"+language+".png"));}
                var hit=grid.FirstSpellingHit(0);Check(hit!=null && hit.Word=="speling","Actual owner-drawn grid records a misspelling: "+language);
                var at=grid.SpellingAt(new Point(hit.Bounds.Left+1,hit.Bounds.Top+1),out var row,out var original);Check(at!=null && row==0 && !original,"Mouse hit test identifies the exact working cue: "+language);
                Call(main,"BuildGridSpellMenu",hit,0,false);var menu=(ToolStripMenuItem)Field(main,"_gridSpellContext");Check(menu.DropDownItems.OfType<ToolStripMenuItem>().Any(i=>i.Text=="spelling"),"Context menu offers native spelling suggestions: "+language);
                Call(main,"ApplyGridSpelling",sub.Paragraphs[0],hit,"spelling");Check(sub.Paragraphs[0].Text=="<i>spelling</i> world" && sub.Paragraphs[0].Actor=="Alice" && sub.Paragraphs[0].Layer==2,"Context correction changes only word text and keeps formatting/metadata: "+language);
                Call(main,"UndoToIndex",true);var restored=(Subtitle)Field(main,"_subtitle");Check(restored.Paragraphs[0].Text=="<i>speling</i> world","One editor Undo restores the misspelled original: "+language);
                Call(main,"UndoToIndex",false);restored=(Subtitle)Field(main,"_subtitle");Check(restored.Paragraphs[0].Text=="<i>spelling</i> world","Redo restores the correction: "+language);
                Call(main,"ApplyGridSpelling",restored.Paragraphs[0],hit,"oops");Check(restored.Paragraphs[0].Text=="<i>spelling</i> world","Stale menu snapshots cannot change edited text: "+language);
                Call(main,"BuildGridSpellMenu",hit,0,true);Check(!menu.DropDownItems.OfType<ToolStripMenuItem>().Any(i=>i.Tag as string=="suggestion") && menu.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Enabled).All(i=>i.Name=="classicStandardGridMenu"),"Original-column spelling menu cannot modify the reference: "+language);
                // Both live renderers share explicit word-list actions, without reloading the native dictionary.
                var editor=(SETextBox)Field(main,"textBoxListViewText");editor.Initialize(true,false);var advanced=(AdvancedTextBox)Field(editor,"_uiTextBox");
                Call(advanced,"LoadDictionaries","en_US");advanced.IsSpellCheckerInitialized=true;
                var spellingWord=new Nikse.SubtitleEdit.Core.SpellCheck.SpellCheckWord{Text="speling",Index=0};
                Set(advanced,"_currentWord",spellingWord);Set(advanced,"_wrongWords",new List<Nikse.SubtitleEdit.Core.SpellCheck.SpellCheckWord>{spellingWord});
                advanced.DoAction(Nikse.SubtitleEdit.Core.Enums.SpellCheckAction.SkipAll);
                Check(grid.GridSpelling.Check("speling").Count==0,"Edit-box Ignore all immediately refreshes grid spelling: "+language);
                editor.ApplyExternalSpellingWord("anotherwrongzz",Nikse.SubtitleEdit.Core.Enums.SpellCheckAction.SkipAll);
                Check(((List<string>)Field(advanced,"_skipAllList")).Contains("anotherwrongzz"),"Grid word-list bridge retains edit-box session ignores: "+language);
                Call(grid,"OnKeyDown",new KeyEventArgs(Keys.Apps));Check(grid.SpellingKeyboardMenu,"Keyboard context menu targets spelling in selected row: "+language);
                restored.Paragraphs[0].Text="wrld word";grid.SetTimeAndText(0,restored.Paragraphs[0],null);grid.SelectIndexAndEnsureVisible(0);
                using(var bitmap=new Bitmap(grid.Width,grid.Height))grid.DrawToBitmap(bitmap,new Rectangle(Point.Empty,grid.Size));
                main.Hide();var context=(ContextMenuStrip)Field(main,"contextMenuStripListView");var opening=new System.ComponentModel.CancelEventArgs();Call(context,"OnOpening",opening);
                Check(opening.Cancel && menu.Available && menu.DropDownItems.Count>0,"Actual keyboard context-menu opening reaches spelling commands: "+language);
                var options=ClassicGridAppearance.Load(Path.Combine(root,"ClassicGridAppearance.json"));options.ShowForcedColumn=true;options.Save(Path.Combine(root,"ClassicGridAppearance.json"));
                Call(main,"SaveGridSpelling",true,"en_US");Check(ClassicGridAppearance.Load(Path.Combine(root,"ClassicGridAppearance.json")).ShowForcedColumn,"Spelling preference saves preserve the other grid settings: "+language);
                Call(main,"SaveGridSpelling",false,"en_US");Check(grid.GridSpelling==null,"Disabling grid spelling removes its renderer: "+language);
                Call(main,"SaveGridSpelling",true,"missing");Check(grid.GridSpelling==null,"Missing dictionary disables underlines without throwing: "+language);
                Call(main,"SaveGridSpelling",true,"en_US");Check(grid.GridSpelling!=null,"Selecting an installed dictionary recovers from missing dictionary: "+language);
                Call(main,"SaveGridSpelling",true,"en_GB");Check(grid.GridSpelling==null,"A not-yet-downloaded dictionary stays inactive: "+language);
                foreach(var ext in new[]{".aff",".dic"})File.Copy(Path.Combine(folder,"en_US"+ext),Path.Combine(folder,"en_GB"+ext));
                Call(main,"RefreshClassicSpellingAfterActivation");Check(grid.GridSpelling!=null && grid.GridSpelling.Dictionary.EndsWith("en_GB"),"Returning from dictionary download activates newly installed files: "+language);
                Call(main,"SaveGridSpelling",true,"en_US");foreach(var ext in new[]{".aff",".dic"})File.Delete(Path.Combine(folder,"en_GB"+ext));
                var wordsFile=Path.Combine(folder,"en_US_user.xml");var wordsBefore=File.ReadAllText(wordsFile);File.WriteAllText(wordsFile,"<broken");
                Call(main,"RefreshClassicSpellingWords");Check(grid.GridSpelling==null,"Malformed user dictionary disables spelling without crashing the editor: "+language);File.WriteAllText(wordsFile,wordsBefore);
                Call(main,"SaveGridSpelling",true,"en_US");Check(grid.GridSpelling!=null,"Restoring user dictionary recovers spelling: "+language);
                Call(main,"SaveGridSpelling",true,"bad\0name");Check(grid.GridSpelling==null,"Invalid saved dictionary identity is handled safely: "+language);Call(main,"SaveGridSpelling",true,"en_US");
                using(var dialog=new ClassicGridSpellingSettings(true,"en_US"))
                {Show(dialog);dialog.Size=dialog.MinimumSize;Check(dialog.DictionaryResult=="en_US" && dialog.EnabledResult,"Settings restores dictionary and independent switch: "+language);using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(root,"settings-"+language+".png"));}dialog.DialogResult=DialogResult.Cancel;}
                Check((bool)Field(main,"_gridSpellEnabled"),"Cancelling the settings draft retains active spelling: "+language);
            }
            using(var reopened=new Nikse.SubtitleEdit.Forms.Main()){Check((bool)Field(reopened,"_gridSpellEnabled") && (string)Field(reopened,"_gridSpellDictionary")=="en_US","Restart restores spelling settings: "+language);}
        }
    }
}
