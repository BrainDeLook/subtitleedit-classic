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
using Nikse.SubtitleEdit.Logic.ClassicCaptions;
using Nikse.SubtitleEdit.Logic.ClassicBroadcast;
namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicCaptionImportForm : Form
    {
        private readonly TextBox _path=new TextBox{Dock=DockStyle.Fill};private readonly ListBox _tracks=new ListBox{Dock=DockStyle.Fill};private readonly TextBox _preview=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};private readonly Label _status=new Label{Dock=DockStyle.Fill};private readonly Button _scan=new Button{AutoSize=true},_ok=new Button{AutoSize=true};private CancellationTokenSource _operation;private bool _closing;private List<BroadcastTrack> _results=new List<BroadcastTrack>();
        public Subtitle Result {get;private set;}
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public ClassicCaptionImportForm(string source)
        {
            Text=L("Импорт CEA / MXF VANC / ARIB: выбор канала","Import CEA / MXF VANC / ARIB: channel selection");Size=new Size(850,570);MinimumSize=new Size(670,420);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),RowCount=5};root.RowStyles.Add(new RowStyle(SizeType.Absolute,35));root.RowStyles.Add(new RowStyle(SizeType.Percent,40));root.RowStyles.Add(new RowStyle(SizeType.Percent,60));root.RowStyles.Add(new RowStyle(SizeType.Absolute,30));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));var top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));var open=new Button{Text=L("Открыть…","Open…"),Dock=DockStyle.Fill};_scan.Text=L("Найти каналы","Find channels");top.Controls.Add(_path,0,0);top.Controls.Add(open,1,0);top.Controls.Add(_scan,2,0);root.Controls.Add(top,0,0);root.Controls.Add(_tracks,0,1);root.Controls.Add(_preview,0,2);root.Controls.Add(_status,0,3);var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=L("Отмена","Cancel"),AutoSize=true};cancel.Click+=(s,e)=>{if(_operation!=null){_closing=true;_operation.Cancel();}else Close();};_ok.Text=L("Импортировать канал","Import channel");_ok.Enabled=false;_ok.Click+=(s,e)=>{if(_operation!=null || _tracks.SelectedIndex<0)return;Result=new Subtitle();Result.Paragraphs.AddRange(_results[_tracks.SelectedIndex].Paragraphs.Select(p=>new Paragraph(p,false)));Result.Renumber();DialogResult=DialogResult.OK;};footer.Controls.Add(cancel);footer.Controls.Add(_ok);root.Controls.Add(footer,0,4);Controls.Add(root);
            _path.Text=source??string.Empty;open.Click+=(s,e)=>{using(var dialog=new OpenFileDialog{Filter="Video|*.1hd;*.2hd;*.1sd;*.2sd;*.mts;*.ts;*.m2ts;*.mkv;*.mp4;*.mov;*.mxf;*.vob;*.mpg;*.mpeg;*.m2v|All files|*.*"})if(dialog.ShowDialog(this)==DialogResult.OK)_path.Text=dialog.FileName;};_scan.Click+=async(s,e)=>await Scan();_tracks.SelectedIndexChanged+=(s,e)=>{_ok.Enabled=_tracks.SelectedIndex>=0;_preview.Text=_tracks.SelectedIndex<0?string.Empty:string.Join(Environment.NewLine,_results[_tracks.SelectedIndex].Paragraphs.Take(25).Select(p=>p.StartTime+" "+p.Text));};FormClosing+=(s,e)=>{if(_operation!=null){_closing=true;_operation.Cancel();e.Cancel=true;}};UiUtil.FixFonts(this);
        }
        private async Task Scan()
        {
            if(_operation!=null || !File.Exists(_path.Text))return;_operation=new CancellationTokenSource();_scan.Enabled=false;_ok.Enabled=false;_path.Parent.Enabled=false;_tracks.Items.Clear();_results.Clear();_preview.Clear();_status.Text=L("Чтение закрытых титров…","Reading closed captions…");
            try{_results=await ClassicBroadcastImport.Extract(_path.Text,_operation.Token);_tracks.Items.Clear();foreach(var pair in _results)_tracks.Items.Add(pair.Name+" — "+pair.Paragraphs.Count);if(_tracks.Items.Count>0)_tracks.SelectedIndex=0;_status.Text=L("Найдено каналов: ","Channels found: ")+_tracks.Items.Count;}
            catch(OperationCanceledException){_status.Text=L("Отменено","Cancelled");}catch(Exception ex){_status.Text=ex.Message;}
            finally{_operation.Dispose();_operation=null;_scan.Enabled=true;_path.Parent.Enabled=true;if(_closing)Close();}
        }
        public void SetTracks(List<BroadcastTrack> tracks)
        {
            _results=tracks;_tracks.Items.Clear();
            foreach(var track in tracks)_tracks.Items.Add(track.Name+" — "+track.Paragraphs.Count);
            if(_tracks.Items.Count>0)_tracks.SelectedIndex=0;
        }
        public void ScanOnOpen(){Shown+=async(s,e)=>await Scan();}
    }
}
