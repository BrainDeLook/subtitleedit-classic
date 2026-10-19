using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private readonly ClassicActorOrder _classicActorOrder=new ClassicActorOrder();
        private ToolStripMenuItem _cuePickerRoot,_actorPickerMenu,_stylePickerMenu;
        private ToolStripMenuItem[] _styleSlotMenus;
        private string CuePickerKeysPath=>Path.Combine(Configuration.DataDirectory,"ClassicCuePickerKeys.json");
        // Availability must not call the format getter: during construction the format combo is still empty.
        private bool ClassicPickersSupported=>_currentSubtitleFormat is AdvancedSubStationAlpha || _currentSubtitleFormat is SubStationAlpha;
        private static string PickerText(string ru,string en)=>ClassicAssistedText.L(ru,en);
        private void InitializeClassicCuePickers()
        {
            if(_cuePickerRoot==null)
            {
                _cuePickerRoot=new ToolStripMenuItem{Name="classicCuePickers"};_actorPickerMenu=new ToolStripMenuItem{Name="classicActorPicker"};_stylePickerMenu=new ToolStripMenuItem{Name="classicStylePicker"};
                _cuePickerRoot.DropDownItems.AddRange(new ToolStripItem[]{_actorPickerMenu,_stylePickerMenu,new ToolStripSeparator()});_styleSlotMenus=new ToolStripMenuItem[10];
                for(var i=0;i<10;i++){var index=i;var menu=new ToolStripMenuItem{Name="classicSetStyle"+(i+1)};menu.Click+=(s,e)=>SetClassicStyleSlot(index);_styleSlotMenus[i]=menu;_cuePickerRoot.DropDownItems.Add(menu);}
                var configure=new ToolStripMenuItem{Name="classicCuePickerKeys"};configure.Click+=(s,e)=>ConfigureClassicCuePickerKeys();_cuePickerRoot.DropDownItems.Add(new ToolStripSeparator());_cuePickerRoot.DropDownItems.Add(configure);
                toolsToolStripMenuItem.DropDownItems.Add(_cuePickerRoot);_actorPickerMenu.Click+=(s,e)=>ShowClassicCuePicker(false);_stylePickerMenu.Click+=(s,e)=>ShowClassicCuePicker(true);
                contextMenuStripListView.Opening+=(s,e)=>{if(!e.Cancel)PopulateClassicPickerContext();};
                toolsToolStripMenuItem.DropDownOpening+=(s,e)=>RefreshClassicCuePickerAvailability();
                SubtitleListview1.SelectedIndexChanged+=(s,e)=>RefreshClassicCuePickerAvailability();
                comboBoxSubtitleFormats.SelectedIndexChanged+=(s,e)=>RefreshClassicCuePickerAvailability();
            }
            _cuePickerRoot.Text=PickerText("Актёры и стили","Actors and styles");_actorPickerMenu.Text=PickerText("Выбрать актёра…","Set actor…");_stylePickerMenu.Text=PickerText("Выбрать стиль…","Set style…");
            _cuePickerRoot.DropDownItems[ _cuePickerRoot.DropDownItems.Count-1].Text=PickerText("Горячие клавиши…","Shortcuts…");
            RefreshClassicCuePickerKeys();RefreshClassicCuePickerAvailability();
        }
        private void RefreshClassicCuePickerAvailability()
        {
            if(_cuePickerRoot==null)return;
            var enabled=ClassicPickersSupported && SubtitleListview1.SelectedIndices.Count>0;_actorPickerMenu.Enabled=_stylePickerMenu.Enabled=enabled;
            var styles=ClassicPickersSupported?AdvancedSubStationAlpha.GetStylesFromHeader(_subtitle.Header):new List<string>();
            if(ClassicPickersSupported && styles.Count==0)styles.AddRange(AdvancedSubStationAlpha.GetStylesFromHeader(GetCurrentSubtitleFormat() is SubStationAlpha?SubStationAlpha.DefaultHeader:AdvancedSubStationAlpha.DefaultHeader));
            for(var i=0;i<10;i++){_styleSlotMenus[i].Text=PickerText("Стиль ","Style ")+(i+1)+(i<styles.Count?": "+styles[i]:"");_styleSlotMenus[i].Enabled=enabled && i<styles.Count;}
        }
        private string ActorPickerShortcut(int position)
        {
            if(position>=10)return "";var property=Configuration.Settings.Shortcuts.GetType().GetProperty("MainListViewSetActor"+(position+1));return property?.GetValue(Configuration.Settings.Shortcuts) as string??"";
        }
        private List<ClassicCueChoice> ClassicPickerChoices(bool styles)
        {
            var selected=SubtitleListview1.SelectedIndices.Cast<int>().Select(i=>_subtitle.GetParagraphOrDefault(i)).Where(p=>p!=null).ToList();
            if(styles)return ClassicCuePickerData.Styles(_subtitle,selected.Select(p=>p.Extra),i=>i<10?_styleSlotMenus[i].ShortcutKeyDisplayString:"",GetCurrentSubtitleFormat() is SubStationAlpha);
            return ClassicCuePickerData.Actors(_subtitle,_classicActorOrder.Get(_subtitle),selected.Select(p=>p.Actor).Distinct().Count()==1?selected[0].Actor:null,ActorPickerShortcut);
        }
        private void PopulateClassicPickerContext()
        {
            if(!ClassicPickersSupported || SubtitleListview1.SelectedIndices.Count==0)return;
            var styles=setStylesForSelectedLinesToolStripMenuItem;styles.Visible=true;
            styles.DropDownItems.Insert(0,new ToolStripSeparator());var style=new ToolStripMenuItem(_stylePickerMenu.Text,null,(s,e)=>ShowClassicCuePicker(true)){Name="classicStylePickerContext",ShortcutKeyDisplayString=_stylePickerMenu.ShortcutKeyDisplayString};styles.DropDownItems.Insert(0,style);
            for(var i=2;i<styles.DropDownItems.Count && i-2<10;i++)if(styles.DropDownItems[i] is ToolStripMenuItem item)item.ShortcutKeyDisplayString=_styleSlotMenus[i-2].ShortcutKeyDisplayString;
            var actors=setActorForSelectedLinesToolStripMenuItem;actors.DropDownItems.Insert(0,new ToolStripSeparator());actors.DropDownItems.Insert(0,new ToolStripMenuItem(_actorPickerMenu.Text,null,(s,e)=>ShowClassicCuePicker(false)){Name="classicActorPickerContext",ShortcutKeyDisplayString=_actorPickerMenu.ShortcutKeyDisplayString});
        }
        private void ShowClassicCuePicker(bool styles)
        {
            ReloadFromSourceView();if(!ClassicPickersSupported || SubtitleListview1.SelectedIndices.Count==0)return;
            var document=_subtitle;var snapshot=AssistedContent(document);var header=document.Header;var indices=SubtitleListview1.SelectedIndices.Cast<int>().ToArray();
            using(var form=new ClassicCuePicker(ClassicPickerChoices(styles),styles,indices.Length,ConfigureClassicCuePickerKeys))
            {
                var result=form.ShowDialog(this);
                if(form.OpenManager){ToolStripMenuItemAssStylesClick(this,EventArgs.Empty);return;}
                if(result!=DialogResult.OK || form.ResultName==null)return;
                if(!ApplyClassicPickerChoice(document,snapshot,header,indices,styles,form.ResultName,form.ResultIsNew))return;
                if(!styles)_classicActorOrder.Set(document,form.NamesInOrder);
            }
        }
        private bool ApplyClassicPickerChoice(Subtitle document,string snapshot,string header,int[] indices,bool styles,string name,bool create)
        {
            if(!ClassicPickersSupported || !ReferenceEquals(document,_subtitle) || AssistedContent(document)!=snapshot || document.Header!=header || indices.Length==0 || indices.Any(i=>i<0 || i>=document.Paragraphs.Count))return false;
            if((styles || name.Length>0) && !ClassicCuePickerData.ValidName(name))return false;
            var nextHeader=header;
            if(styles)
            {
                var existing=ClassicCuePickerData.Styles(document,Array.Empty<string>(),i=>"",GetCurrentSubtitleFormat() is SubStationAlpha).FirstOrDefault(i=>i.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
                if(existing!=null)name=existing.Name;
                else if(create){try{nextHeader=ClassicCuePickerData.AddStyle(header,name,GetCurrentSubtitleFormat() is SubStationAlpha);}catch(ArgumentException ex){ShowStatus(ex.Message);return false;}}
                else return false;
            }
            if(nextHeader==header && indices.All(i=>(styles?document.Paragraphs[i].Extra:document.Paragraphs[i].Actor)==name))return true;
            if(!styles)_classicActorOrder.Get(document);
            MakeHistoryForUndo(styles?"Set style: "+name:"Set actor: "+name);document.Header=nextHeader;
            foreach(var index in indices){if(styles)document.Paragraphs[index].Extra=name;else document.Paragraphs[index].Actor=name;SubtitleListview1.SetTimeAndText(index,document.Paragraphs[index],document.GetParagraphOrDefault(index+1));}
            if(!styles)_classicActorOrder.Get(document);
            RefreshSelectedParagraph();audioVisualizer.Invalidate();UpdateSourceView();SetTitle();RefreshClassicCuePickerAvailability();return true;
        }
        private void SetClassicStyleSlot(int slot)
        {
            ReloadFromSourceView();if(!ClassicPickersSupported || SubtitleListview1.SelectedIndices.Count==0)return;
            var choices=ClassicPickerChoices(true);if(slot<0 || slot>=choices.Count)return;
            ApplyClassicPickerChoice(_subtitle,AssistedContent(_subtitle),_subtitle.Header,SubtitleListview1.SelectedIndices.Cast<int>().ToArray(),true,choices[slot].Name,false);
        }
        private Dictionary<string,int> LoadClassicCuePickerKeys()
        {
            try{return JsonConvert.DeserializeObject<Dictionary<string,int>>(File.ReadAllText(CuePickerKeysPath))??new Dictionary<string,int>();}
            catch(Exception ex)when(ex is IOException || ex is UnauthorizedAccessException || ex is JsonException){return new Dictionary<string,int>();}
        }
        private IEnumerable<ToolStripMenuItem> ClassicPickerCommands=>new[]{_actorPickerMenu,_stylePickerMenu}.Concat(_styleSlotMenus);
        private bool CuePickerKeyConflict(Keys keys,ToolStripMenuItem owner)
        {
            if(keys==Keys.None)return false;if(ClassicPaletteKeyConflict(keys) || LoadClassicPaletteShortcut()==keys)return true;
            var assisted=LoadClassicAssistedKeys();if(assisted.Split==(int)keys || assisted.Move==(int)keys)return true;
            return OtherClassicPickerKeyConflict(menuStrip1.Items,keys) || ClassicPickerCommands.Any(m=>m!=owner && m.ShortcutKeys==keys);
        }
        private bool OtherClassicPickerKeyConflict(ToolStripItemCollection items,Keys keys)
        {
            foreach(var menu in items.OfType<ToolStripMenuItem>())
                if((!ClassicPickerCommands.Contains(menu) && menu.ShortcutKeys==keys) || OtherClassicPickerKeyConflict(menu.DropDownItems,keys))return true;
            return false;
        }
        private void RefreshClassicCuePickerKeys()
        {
            if(_cuePickerRoot==null)return;var settings=LoadClassicCuePickerKeys();foreach(var menu in ClassicPickerCommands)menu.ShortcutKeys=Keys.None;
            foreach(var menu in ClassicPickerCommands)
            {var key=settings.TryGetValue(menu.Name,out var stored)?(Keys)stored:Keys.None;menu.ShortcutKeys=ValidClassicPaletteKey(key) && !CuePickerKeyConflict(key,menu)?key:Keys.None;menu.ShortcutKeyDisplayString=menu.ShortcutKeys==Keys.None?"":new KeysConverter().ConvertToString(menu.ShortcutKeys);}
        }
        private void SaveClassicCuePickerKeys(Dictionary<string,int> settings)
        {
            var used=new HashSet<int>();foreach(var menu in ClassicPickerCommands)
            {
                var key=settings.TryGetValue(menu.Name,out var stored)?(Keys)stored:Keys.None;
                if(!ValidClassicPaletteKey(key) || (key!=Keys.None && (!used.Add((int)key) || ClassicPaletteKeyConflict(key) || OtherClassicPickerKeyConflict(menuStrip1.Items,key) || LoadClassicPaletteShortcut()==key || LoadClassicAssistedKeys().Split==(int)key || LoadClassicAssistedKeys().Move==(int)key)))throw new ArgumentException(PickerText("Сочетание занято или недопустимо.","Shortcut is occupied or invalid."));
            }
            Directory.CreateDirectory(Configuration.DataDirectory);var temp=CuePickerKeysPath+".tmp";try{File.WriteAllText(temp,JsonConvert.SerializeObject(settings));if(File.Exists(CuePickerKeysPath))File.Replace(temp,CuePickerKeysPath,null);else File.Move(temp,CuePickerKeysPath);}finally{if(File.Exists(temp))File.Delete(temp);}RefreshClassicCuePickerKeys();
        }
        private void ConfigureClassicCuePickerKeys()
        {
            using(var form=new Form{Text=PickerText("Клавиши актёров и стилей","Actor and style shortcuts"),ClientSize=new Size(660,520),MinimumSize=new Size(600,400),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false})
            {
                var values=LoadClassicCuePickerKeys();var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(10)};root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
                foreach(var menu in ClassicPickerCommands)
                {
                    var box=new TextBox{ReadOnly=true,Dock=DockStyle.Fill,Text=menu.ShortcutKeyDisplayString,AccessibleName=menu.Text};var id=menu.Name;
                    box.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape || e.KeyCode==Keys.Enter)return;var key=e.KeyCode==Keys.Back || e.KeyCode==Keys.Delete?Keys.None:e.KeyData;values[id]=(int)key;box.Text=new KeysConverter().ConvertToString(key);e.SuppressKeyPress=true;};
                    var row=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.Absolute,34));table.Controls.Add(new Label{Text=menu.Text,AutoEllipsis=true,Dock=DockStyle.Fill},0,row);table.Controls.Add(box,1,row);
                }
                scroll.Controls.Add(table);root.Controls.Add(scroll,0,0);var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=LanguageSettings.Current.General.Cancel,DialogResult=DialogResult.Cancel,AutoSize=true};var ok=new Button{Text=LanguageSettings.Current.General.Ok,AutoSize=true};
                ok.Click+=(s,e)=>{try{SaveClassicCuePickerKeys(values);form.DialogResult=DialogResult.OK;}catch(Exception ex)when(ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException){MessageBox.Show(form,ex.Message,form.Text);}};
                footer.Controls.Add(cancel);footer.Controls.Add(ok);root.Controls.Add(footer,0,1);form.Controls.Add(root);form.CancelButton=cancel;form.AcceptButton=ok;UiUtil.FixFonts(form);form.ShowDialog(Form.ActiveForm??this);
            }
        }
    }
}
