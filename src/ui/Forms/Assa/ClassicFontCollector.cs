using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms.Assa
{
    public sealed class ClassicFontCollector : Form
    {
        private readonly Subtitle _subtitle;
        private readonly CancellationTokenSource _cancel=new CancellationTokenSource();
        private readonly ListView _list=new ListView { Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true, HideSelection=false, MultiSelect=true };
        private readonly Label _status=new Label { Dock=DockStyle.Fill, AutoEllipsis=true };
        private readonly CheckBox _trim=new CheckBox { AutoSize=true };
        private readonly FlowLayoutPanel _actions=new FlowLayoutPanel { Dock=DockStyle.Fill, AutoSize=true, WrapContents=true };
        private List<ClassicFontFile> _files=new List<ClassicFontFile>();
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public string UpdatedFooter { get; private set; }
        public ClassicFontCollector(Subtitle subtitle)
        {
            _subtitle=new Subtitle(subtitle,false); UpdatedFooter=subtitle.Footer;
            Text=L("Сбор шрифтов ASS/SSA","ASS/SSA font collector");Size=new Size(900,530);MinimumSize=new Size(670,400);StartPosition=FormStartPosition.CenterParent;
            _list.Columns.Add(L("Шрифт","Font"),210);_list.Columns.Add(L("Состояние","Status"),120);_list.Columns.Add(L("Файлы","Files"),480);
            var root=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(10), RowCount=5, ColumnCount=1 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            root.Controls.Add(_list,0,0);
            _trim.Text=L("Сократить встраиваемые / копируемые шрифты до символов этого файла","Trim embedded / copied fonts to this subtitle's glyphs");root.Controls.Add(_trim,0,1);
            AddAction(L("В выбранную папку…","Copy to folder…"),()=>CopyTo(false));AddAction(L("В коллекцию Subtitle Edit","Copy to Subtitle Edit Fonts"),()=>CopyTo(true));AddAction(L("Встроить в субтитры","Embed in subtitle"),Embed);
            AddAction(L("Добавить шрифты…","Add font files…"),Import);root.Controls.Add(_actions,0,2);root.Controls.Add(_status,0,3);
            var footer=new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.RightToLeft };
            var close=new Button {Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};
            var apply=new Button {Text=L("Применить вложения","Apply attachments"),DialogResult=DialogResult.OK,AutoSize=true};footer.Controls.Add(close);footer.Controls.Add(apply);root.Controls.Add(footer,0,4);Controls.Add(root);CancelButton=close;
            _trim.CheckedChanged+=(s,e)=>{if(_trim.Checked)MessageBox.Show(this,L("Сокращённый шрифт содержит символы текущего текста. После добавления нового текста может потребоваться встроить полный шрифт снова.","A trimmed font contains glyphs for the current text. Adding text later may require embedding the full font again."),Text);};
            Shown+=async(s,e)=>await Scan();FormClosed+=(s,e)=>_cancel.Cancel();UiUtil.FixFonts(this);
        }
        private void AddAction(string text,Action action)
        {var button=new Button { Text=text, AutoSize=true, Enabled=false };button.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}};_actions.Controls.Add(button);}
        private async Task Scan()
        {
            foreach(Control c in _actions.Controls)c.Enabled=false;_status.Text=L("Поиск шрифтов…","Scanning fonts…");
            try
            {
                var files=await Task.Run(()=>ClassicFontCollection.Scan(_subtitle,_cancel.Token));if(IsDisposed || _cancel.IsCancellationRequested)return;_files=files;_list.Items.Clear();
                foreach(var name in AssaFontEmbedder.GetUsedFontNames(_subtitle))
                {
                    var matches=files.Where(f=>f.Family.Equals(name,StringComparison.OrdinalIgnoreCase)).ToList();
                    var row=new ListViewItem(name) {Tag=matches};row.SubItems.Add(matches.Any(f=>f.Embedded)?L("Вложен","Embedded"):matches.Count>0?L("Найден","Found"):L("Не найден","Missing"));row.SubItems.Add(string.Join("; ",matches.Select(f=>f.Path).Distinct()));
                    if(matches.Count==0)row.ForeColor=Color.Firebrick;_list.Items.Add(row);
                }
                foreach(Control c in _actions.Controls)c.Enabled=true;_status.Text=L("Выделите шрифты для операции. Без выделения используются все найденные.","Select fonts for an action. With no selection, all found fonts are used.");
            }
            catch(OperationCanceledException){}
            catch(Exception ex){if(!IsDisposed)_status.Text=ex.Message;}
        }
        private List<ClassicFontFile> Chosen()
        {
            var rows=_list.SelectedItems.Count>0?_list.SelectedItems.Cast<ListViewItem>():_list.Items.Cast<ListViewItem>();
            return rows.SelectMany(r=>(List<ClassicFontFile>)r.Tag).GroupBy(f=>f.Path,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
        }
        private void CopyTo(bool collection)
        {
            var folder=ClassicFontCollection.Folder;
            if(!collection)using(var dialog=new FolderBrowserDialog()){if(dialog.ShowDialog(this)!=DialogResult.OK)return;folder=dialog.SelectedPath;}
            var files=Chosen();foreach(var file in files)ClassicFontCollection.Copy(file,folder,_trim.Checked,_subtitle);if(collection)ClassicFontCollection.LoadCollected();_status.Text=files.Count+" → "+folder;
        }
        private void Embed()
        {
            var footer=UpdatedFooter;var files=Chosen();var lines=AssaFontEmbedder.GetUsedTextLines(_subtitle);int added=0;
            foreach(var file in files)
            {
                var bytes=file.Read();if(_trim.Checked)bytes=FontTrimmer.Trim(bytes,lines).Bytes;
                var next=file.Embedded && _trim.Checked?AssaFontEmbedder.ReplaceFont(footer,file.Path,bytes):AssaFontEmbedder.AddFontToFooter(footer,file.Path,bytes);if(next!=footer)added++;footer=next;
            }
            UpdatedFooter=footer;_status.Text=L("Добавлено вложений: ","Attachments added: ")+added;
        }
        private async void Import()
        {
            try { using(var dialog=new OpenFileDialog { Filter="Fonts|*.ttf;*.otf;*.ttc",Multiselect=true })
                if(dialog.ShowDialog(this)==DialogResult.OK)
                {foreach(var path in dialog.FileNames)ClassicFontCollection.Copy(new ClassicFontFile {Path=path},ClassicFontCollection.Folder,false,_subtitle);ClassicFontCollection.LoadCollected();await Scan();} } catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,ex.Message,Text);}
        }
    }
}
