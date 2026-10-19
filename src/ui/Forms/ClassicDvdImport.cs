using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.VobSub;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.ClassicImport.Dvd;
namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicDvdImport : Form
    {
        private readonly ComboBox _titles=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};private readonly ListBox _files=new ListBox{Dock=DockStyle.Fill};
        private readonly Label _status=new Label{Dock=DockStyle.Fill};private readonly ProgressBar _progress=new ProgressBar{Dock=DockStyle.Fill};private readonly Button _rip=new Button{AutoSize=true};private readonly CheckBox _pal=new CheckBox{Text="PAL (25 fps)",Checked=true,AutoSize=true};
        private CancellationTokenSource _operation;private bool _closing;private readonly List<DvdTitle> _titleList=new List<DvdTitle>();
        public List<VobSubMergedPack> MergedVobSubPacks {get;private set;}=new List<VobSubMergedPack>();public List<Color> Palette {get;private set;}=new List<Color>();public List<string> Languages {get;private set;}=new List<string>();public string SelectedLanguage {get;private set;}="en";
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public ClassicDvdImport(string initial)
        {
            Text=L("Импорт DVD: титулы и точные тайминги ячеек","DVD import: titles and exact cell timing");Size=new Size(880,570);MinimumSize=new Size(680,440);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),RowCount=6};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));root.RowStyles.Add(new RowStyle(SizeType.Absolute,24));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};Add(tools,"IFO…",OpenIfo);Add(tools,L("Добавить VOB…","Add VOB…"),AddVob);Add(tools,"↑",()=>Move(-1));Add(tools,"↓",()=>Move(1));Add(tools,L("Удалить","Remove"),()=>{if(_files.SelectedIndex>=0)_files.Items.RemoveAt(_files.SelectedIndex);});Add(tools,L("Очистить","Clear"),()=>_files.Items.Clear());tools.Controls.Add(_pal);root.Controls.Add(tools,0,0);root.Controls.Add(_titles,0,1);root.Controls.Add(_files,0,2);root.Controls.Add(_status,0,3);root.Controls.Add(_progress,0,4);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=L("Отмена","Cancel"),AutoSize=true};cancel.Click+=(s,e)=>{if(_operation!=null){_operation.Cancel();_closing=true;}else{DialogResult=DialogResult.Cancel;Close();}};_rip.Text=L("Начать извлечение","Start ripping");_rip.Click+=async(s,e)=>await Rip();footer.Controls.Add(cancel);footer.Controls.Add(_rip);root.Controls.Add(footer,0,5);Controls.Add(root);_titles.SelectedIndexChanged+=(s,e)=>SelectTitle();
            Shown+=(s,e)=>{if(!string.IsNullOrWhiteSpace(initial))try{LoadIfo(initial);}catch(Exception ex){_status.Text=ex.Message;}};FormClosing+=(s,e)=>{if(_operation!=null){_closing=true;_operation.Cancel();e.Cancel=true;}};UiUtil.FixFonts(this);
        }
        private void Add(FlowLayoutPanel parent,string text,Action action){var button=new Button{Text=text,AutoSize=true};button.Click+=(s,e)=>{if(_operation!=null)return;try{action();}catch(Exception ex){_status.Text=ex.Message;}};parent.Controls.Add(button);}
        private void OpenIfo(){using(var d=new OpenFileDialog{Filter="DVD IFO/BUP|*.ifo;*.bup"})if(d.ShowDialog(this)==DialogResult.OK)LoadIfo(d.FileName);}
        public void LoadIfo(string path)
        {
            if(Path.GetExtension(path).Equals(".vob",StringComparison.OrdinalIgnoreCase)){_files.Items.Add(path);return;}
            _titles.Items.Clear();_titleList.Clear();_titleList.AddRange(DvdTitle.Find(path));foreach(var title in _titleList)_titles.Items.Add($"VTS {title.TitleSetNumber:00} / {title.ProgramChain.Number}: {title.ProgramChain.Duration} — {title.AvailableShare:P0}");
            var selected=DvdTitle.GetDefault(_titleList);if(selected!=null)_titles.SelectedIndex=_titleList.IndexOf(selected);else _status.Text=L("Титулы не найдены. Можно добавить VOB вручную.","No titles found. VOB files can be added manually.");
        }
        private void SelectTitle(){if(_titles.SelectedIndex<0)return;var title=_titleList[_titles.SelectedIndex];_files.Items.Clear();foreach(var path in title.VobFileNames)_files.Items.Add(path);_pal.Checked=title.Ifo.IsPal;Palette=title.ProgramChain.Palette.Select(c=>Color.FromArgb(c.Alpha,c.Red,c.Green,c.Blue)).ToList();Languages=title.Ifo.GetLanguages();_status.Text=string.Join("; ",Languages);}
        private void AddVob(){using(var d=new OpenFileDialog{Filter="DVD VOB|*.vob",Multiselect=true})if(d.ShowDialog(this)==DialogResult.OK)foreach(var file in d.FileNames)_files.Items.Add(file);}
        private void Move(int direction){var i=_files.SelectedIndex;if(i<0 || i+direction<0 || i+direction>=_files.Items.Count)return;var item=_files.Items[i];_files.Items.RemoveAt(i);_files.Items.Insert(i+direction,item);_files.SelectedIndex=i+direction;}
        private async Task Rip()
        {
            if(_operation!=null || _files.Items.Count==0)return;var files=_files.Items.Cast<string>().ToList();var title=_titles.SelectedIndex>=0?_titleList[_titles.SelectedIndex]:null;
            if(title==null){var nearby=ClassicIfoParser.GetIfoFileName(files[0]);if(File.Exists(nearby)){var ifo=new ClassicIfoParser(nearby);Palette=ifo.Palette.Select(c=>Color.FromArgb(c.Alpha,c.Red,c.Green,c.Blue)).ToList();Languages=ifo.GetLanguages();_pal.Checked=ifo.IsPal;}}
            var chain=title!=null && files.SequenceEqual(title.VobFileNames,StringComparer.OrdinalIgnoreCase)?title.ProgramChain:null;var pal=_pal.Checked;_operation=new CancellationTokenSource();var token=_operation.Token;var success=false;_rip.Enabled=false;_titles.Enabled=false;_pal.Enabled=false;
            var progress=new Progress<int>(value=>{if(!IsDisposed)_progress.Value=Math.Max(0,Math.Min(100,value));});
            try
            {
                MergedVobSubPacks=await Task.Run(()=>{Action<long,long> report=(done,total)=>((IProgress<int>)progress).Report(total>0?(int)(done*100/total):0);var packs=chain!=null?DvdSubtitleRipper.Rip(files,chain,report,token):DvdSubtitleRipper.Rip(files,report,token);token.ThrowIfCancellationRequested();if(DvdSubtitleRipper.CountEncrypted(packs)>0)throw new InvalidDataException("Encrypted DVD subtitle sectors cannot be decoded.");var parser=new VobSubParser(pal);parser.VobSubPacks.AddRange(packs);return parser.MergeVobSubPacks();},token);
                if(MergedVobSubPacks.Count==0)_status.Text=L("Субтитры не найдены.","No subtitles found.");else{SelectedLanguage=Languages.FirstOrDefault()??"en";success=true;}
            }
            catch(OperationCanceledException){_status.Text=L("Извлечение отменено.","Ripping cancelled.");}catch(Exception ex){_status.Text=ex.Message;}
            finally{_operation.Dispose();_operation=null;_rip.Enabled=true;_titles.Enabled=true;_pal.Enabled=true;if(_closing)Close();else if(success){DialogResult=DialogResult.OK;Close();}}
        }
    }
}

