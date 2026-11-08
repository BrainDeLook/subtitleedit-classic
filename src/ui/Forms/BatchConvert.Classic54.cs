using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Plugins;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms
{
 public sealed partial class BatchConvert
 {
    private List<ListViewItem> _classicBatchActionItems;
    private ClassicBatchPlugin[] _classicBatchPlugins=new ClassicBatchPlugin[0];
    private IEnumerable<ListViewItem> ClassicBatchActionItems => _classicBatchActionItems ?? listViewConvertOptions.Items.Cast<ListViewItem>();
    private void InitializeClassicBatch54()
    {
        var panes = new Panel { Name = "classicBatchPanes", Bounds = Rectangle.Union(groupBoxInput.Bounds, groupBoxOutput.Bounds), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        var divider = new Splitter { Name = "classicBatchFilesDivider", Dock = DockStyle.Top, Height = 6, MinSize = 170, MinExtra = 260, BackColor = SystemColors.ControlDark };
        groupBoxInput.Dock = DockStyle.Top;
        groupBoxOutput.Dock = DockStyle.Fill;
        panes.Controls.Add(groupBoxOutput);
        panes.Controls.Add(divider);
        panes.Controls.Add(groupBoxInput);
        Controls.Add(panes);
        panes.BringToFront();
        _classicBatchActionItems=listViewConvertOptions.Items.Cast<ListViewItem>().ToList();
        var search=new TextBox{Name="classicBatchFunctionSearch",Left=listViewConvertOptions.Left,Top=listViewConvertOptions.Top,Width=Math.Max(80,listViewConvertOptions.Width-105),Anchor=AnchorStyles.Top|AnchorStyles.Left};
        var plugins=new Button{Name="classicBatchPlugins",Text=ClassicAssistedText.L("Плагины…","Plugins…"),Left=search.Right+5,Top=search.Top,Width=100,Height=24};
        listViewConvertOptions.Top+=28;listViewConvertOptions.Height-=28;
        groupBoxConvertOptions.Controls.Add(search);groupBoxConvertOptions.Controls.Add(plugins);
        var hint=new ToolTip(components);hint.SetToolTip(search,ClassicAssistedText.L("Поиск функций; скрытые отмеченные функции остаются включёнными.","Search functions; hidden checked functions remain enabled."));
        search.TextChanged+=(sender,e)=>
        {
            var selected=listViewConvertOptions.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
            listViewConvertOptions.BeginUpdate();listViewConvertOptions.Items.Clear();
            foreach(var item in _classicBatchActionItems.Where(x=>((FixActionItem)x.Tag).Text.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0))listViewConvertOptions.Items.Add(item);
            if(selected!=null && listViewConvertOptions.Items.Contains(selected))selected.Selected=true;
            listViewConvertOptions.EndUpdate();
        };
        plugins.Click+=(sender,e)=>
        {
            var installed=ClassicBatchPlugin.Installed();
            using(var form=new Form{Text=ClassicAssistedText.L("Плагины пакетной обработки","Batch plugins"),Size=new Size(640,430),StartPosition=FormStartPosition.CenterParent,MinimumSize=new Size(450,300)})
            {
                var list=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};foreach(var plugin in installed)list.Items.Add(plugin,(_batch53.PluginManifests??new string[0]).Contains(Path.Combine(plugin.Folder,"plugin.json")));
                var note=new Label{Dock=DockStyle.Top,Height=52,Text=ClassicAssistedText.L("Только установленные плагины с batch API. В пакетном режиме применяются сохранённые настройки без окон для каждого файла. Порядок — сверху вниз.","Installed plugins with the batch API only. Saved settings are applied without per-file dialogs, in the order shown.")};
                var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=38};var configure=new Button{Text=ClassicAssistedText.L("Настройки…","Configure…"),AutoSize=true};var ok=new Button{Text=LanguageSettings.Current.General.Ok,DialogResult=DialogResult.OK,AutoSize=true};var cancel=new Button{Text=LanguageSettings.Current.General.Cancel,DialogResult=DialogResult.Cancel,AutoSize=true};footer.Controls.AddRange(new Control[]{configure,ok,cancel});
                configure.Click+=async(o,args)=>{if(!(list.SelectedItem is ClassicBatchPlugin plugin))return;configure.Enabled=false;try{await plugin.Run(new Subtitle(),new SubRip(),"",PluginConstants.RequestTypeConfigure,CancellationToken.None);}catch(Exception ex)when(ex is IOException || ex is Newtonsoft.Json.JsonException || ex is System.Net.Http.HttpRequestException || ex is InvalidOperationException || ex is OperationCanceledException || ex is System.ComponentModel.Win32Exception || ex is UnauthorizedAccessException){MessageBox.Show(form,ex.Message,form.Text);}finally{configure.Enabled=true;}};
                form.Controls.Add(list);form.Controls.Add(note);form.Controls.Add(footer);form.AcceptButton=ok;form.CancelButton=cancel;
                if(form.ShowDialog(this)==DialogResult.OK)_batch53.PluginManifests=list.CheckedItems.Cast<ClassicBatchPlugin>().Select(x=>Path.Combine(x.Folder,"plugin.json")).ToArray();
            }
        };
    }
 }
}
