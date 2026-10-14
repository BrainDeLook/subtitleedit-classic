using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Ocr;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Nikse.SubtitleEdit.Logic.VideoOcr;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageBox= Nikse.SubtitleEdit.Forms.SeMsgBox.MessageBox;

namespace Nikse.SubtitleEdit.Forms
{
    // WinForms adaptation of stable v5.2.0 VideoOcrWindow/ViewModel. The options,
    // crop selection, frame probe and result editing follow that feature.
    public sealed class ClassicVideoOcr : Form
    {
        private readonly ClassicVideoOcrOptions _options=ClassicVideoOcrOptions.Load();
        private readonly TextBox _file=new TextBox{Dock=DockStyle.Fill,ReadOnly=true};
        private readonly PictureBox _preview=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Black};
        private readonly TrackBar _position=new TrackBar{Dock=DockStyle.Fill,Minimum=0,Maximum=10000,TickStyle=TickStyle.None};
        private readonly NumericUpDown _seconds=new NumericUpDown{DecimalPlaces=3,Maximum=86400,Width=110};
        private readonly ComboBox _language=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly ComboBox _engine=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly ComboBox _crispBackend=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly ComboBox _crispModel=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,DropDownWidth=460};
        private TableLayoutPanel _settingsTable;
        private readonly ComboBox _variant=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly ComboBox _mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly ComboBox _dictionary=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly CheckBox _fix=new CheckBox{AutoSize=true};
        private readonly CheckBox _positionTag=new CheckBox{AutoSize=true};
        private readonly NumericUpDown _fps=Number(1,25),_brightness=Number(0,255),_imageSimilarity=Number(0,100),_textSimilarity=Number(0,100),_gap=Number(0,5000),_duration=Number(0,10000);
        private readonly NumericUpDown[] _crop={Number(0,100),Number(0,100),Number(0.1m,100),Number(0.1m,100)};
        private readonly DataGridView _lines=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=true,BackgroundColor=SystemColors.Window,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells};
        private readonly Label _status=new Label{Dock=DockStyle.Fill,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
        private readonly ProgressBar _progress=new ProgressBar{Dock=DockStyle.Fill};
        private readonly Button _start=new Button(),_ok=new Button(),_cancel=new Button(),_test=new Button(),_download=new Button();
        private readonly GroupBox _settings=new GroupBox{Dock=DockStyle.Fill};
        private readonly Button _open=new Button();
        private CancellationTokenSource _operation;
        private readonly CancellationTokenSource _lifetime=new CancellationTokenSource();
        private CancellationTokenSource _previewRequest;
        private Task _previewTask=Task.CompletedTask;
        private readonly List<Task> _previewTasks=new List<Task>();
        private readonly System.Windows.Forms.Timer _elapsedTimer=new System.Windows.Forms.Timer{Interval=500};
        private Stopwatch _watch;
        private string _phase="";
        private VideoInfo _info;
        private bool _updating,_closeRequested;
        private PointF? _dragStart;
        private OcrFixEngine _fixEngine;
        private ClassicVideoOcrRules _rules;
        private readonly string _previewFolder=Path.Combine(ClassicPaddleOcr.Root,"Preview",Guid.NewGuid().ToString("N"));
        public Subtitle SubtitleFromOcr { get; private set; }=new Subtitle();
        private static string L(string ru,string en) => Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase) ? ru : en;
        private static NumericUpDown Number(decimal min,decimal max) => new NumericUpDown{Minimum=min,Maximum=max,Dock=DockStyle.Fill};

        public ClassicVideoOcr(string video)
        {
            UiUtil.PreInitialize(this);
            Text=L("OCR вшитых субтитров","OCR burned-in subtitles"); StartPosition=FormStartPosition.CenterParent;
            ClientSize=new Size(1080,760); MinimumSize=new Size(960,700);
            _file.Text=video ?? ""; _open.Text=L("Открыть видео…","Open video…"); _open.Width=140;
            _start.Text=L("Начать OCR","Start OCR"); _ok.Text="OK"; _cancel.Text=L("Отмена","Cancel");
            _test.Text=L("Проверить кадр","Test current frame"); _test.Width=180;
            _download.Text=L("Скачать движок и модели","Download engine and models"); _download.Dock=DockStyle.Fill;
            _fix.Text=L("Исправлять ошибки OCR","Fix OCR errors"); _positionTag.Text=L("Добавлять позицию ASS","Add ASS position tag");
            _settings.Text=L("Параметры","Settings");
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(10)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); root.RowStyles.Add(new RowStyle(SizeType.Percent,62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,38)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,28)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            var fileBar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2}; fileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); fileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
            fileBar.Controls.Add(_file,0,0); fileBar.Controls.Add(_open,1,0); root.Controls.Add(fileBar,0,0);
            var top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2}; top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,365));
            var previewLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3}; previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            previewLayout.Controls.Add(_preview,0,0); previewLayout.Controls.Add(_position,0,1);
            var probeBar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false}; probeBar.Controls.Add(_seconds); probeBar.Controls.Add(_test);
            probeBar.Controls.Add(new Label{Text=L("Выдели область мышью","Drag to select the scan area"),AutoSize=true,Padding=new Padding(0,6,0,0)});
            previewLayout.Controls.Add(probeBar,0,2); top.Controls.Add(previewLayout,0,0); top.Controls.Add(_settings,1,0); root.Controls.Add(top,0,1);
            // Let a dedicated viewport scroll the full preferred height. A dock-filled
            // scrolling table can clip its final rows as engine rows are hidden/shown.
            var settingsViewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(0,0,0,12)};
            var settings=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=2,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(8,8,8,16)}; _settingsTable=settings;
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
            AddSetting(settings,L("Движок","Engine"),_engine);
            AddSetting(settings,L("Язык","Language"),_language); AddSetting(settings,L("Вычисления","Compute"),_variant); AddSetting(settings,L("Размер модели","Model size"),_mode);
            AddSetting(settings,L("Модуль CrispEmbed","CrispEmbed backend"),_crispBackend); AddSetting(settings,L("Модель CrispEmbed","CrispEmbed model"),_crispModel);
            settings.RowCount++; settings.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); settings.Controls.Add(_download,0,settings.RowCount-1); settings.SetColumnSpan(_download,2);
            AddSetting(settings,L("Кадров в секунду","Frames per second"),_fps); AddSetting(settings,L("Минимальная яркость","Brightness minimum"),_brightness);
            AddSetting(settings,L("Сходство кадров (%)","Image similarity (%)"),_imageSimilarity); AddSetting(settings,L("Сходство текста (%)","Text similarity (%)"),_textSimilarity);
            AddSetting(settings,L("Максимальный разрыв (мс)","Maximum gap (ms)"),_gap); AddSetting(settings,L("Минимальная длительность (мс)","Minimum duration (ms)"),_duration);
            var cropBar=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=4,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink}; for(int i=0;i<4;i++) { cropBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25)); _crop[i].DecimalPlaces=1; cropBar.Controls.Add(_crop[i],i,0); }
            settings.RowCount++; settings.RowStyles.Add(new RowStyle(SizeType.AutoSize)); var cropLabel=new Label{Text=L("Область: X, Y, ширина, высота (%)","Area: X, Y, width, height (%)"),AutoSize=true}; settings.Controls.Add(cropLabel,0,settings.RowCount-1); settings.SetColumnSpan(cropLabel,2);
            settings.RowCount++; settings.RowStyles.Add(new RowStyle(SizeType.AutoSize)); settings.Controls.Add(cropBar,0,settings.RowCount-1); settings.SetColumnSpan(cropBar,2);
            var presets=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=3,RowCount=1,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};
            for(int i=0;i<3;i++) presets.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
            foreach(var preset in new[]{L("Низ","Bottom"),L("Верх","Top"),L("Весь кадр","Full frame")})
            {
                var button=new Button{Text=preset,Dock=DockStyle.Top,Height=27}; button.Click+=(s,e)=> { _updating=true; _crop[0].Value=0; _crop[2].Value=100; _crop[1].Value=preset==L("Низ","Bottom") ? 66.7m : 0; _crop[3].Value=preset==L("Весь кадр","Full frame") ? 100 : 33.3m; _updating=false; _preview.Invalidate(); }; presets.Controls.Add(button);
            }
            settings.RowCount++; settings.RowStyles.Add(new RowStyle(SizeType.AutoSize)); settings.Controls.Add(presets,0,settings.RowCount-1); settings.SetColumnSpan(presets,2);
            AddSetting(settings,L("Словарь","Dictionary"),_dictionary);
            settings.RowCount++; settings.RowStyles.Add(new RowStyle(SizeType.Absolute,25)); settings.Controls.Add(_fix,0,settings.RowCount-1); settings.SetColumnSpan(_fix,2);
            settings.RowCount++; settings.RowStyles.Add(new RowStyle(SizeType.Absolute,25)); settings.Controls.Add(_positionTag,0,settings.RowCount-1); settings.SetColumnSpan(_positionTag,2);
            settingsViewport.Controls.Add(settings);
            _settings.Controls.Add(settingsViewport);
            _lines.Columns.Add("start",L("Начало","Start")); _lines.Columns[0].ReadOnly=true; _lines.Columns[0].Width=110;
            _lines.Columns.Add("end",L("Конец","End")); _lines.Columns[1].ReadOnly=true; _lines.Columns[1].Width=110;
            _lines.Columns.Add("duration",L("Длительность","Duration")); _lines.Columns[2].ReadOnly=true; _lines.Columns[2].Width=95;
            _lines.Columns.Add("text",L("Текст","Text")); _lines.Columns[3].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
            _lines.Columns[3].DefaultCellStyle.WrapMode=DataGridViewTriState.True;
            root.Controls.Add(_lines,0,2);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2}; footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30)); footer.Controls.Add(_status,0,0); footer.Controls.Add(_progress,1,0); root.Controls.Add(footer,0,3);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false}; foreach(var b in new[]{_cancel,_ok,_start}){b.Width=140;b.Height=27;buttons.Controls.Add(b);} root.Controls.Add(buttons,0,4); Controls.Add(root);
            foreach(var lang in ClassicVideoOcrLanguages.GetLanguages()) _language.Items.Add(lang);
            _language.SelectedItem=_language.Items.Cast<OcrLanguage2>().FirstOrDefault(l=>l.Code==_options.Language) ?? _language.Items.Cast<OcrLanguage2>().First(l=>l.Code=="en");
            _engine.Items.AddRange(new object[]{"Paddle OCR","CrispEmbed"}); _engine.SelectedItem=_options.Engine; if(_engine.SelectedIndex<0)_engine.SelectedIndex=0;
            foreach(var backend in ClassicCrispOcrCatalog.VideoBackends()) _crispBackend.Items.Add(backend);
            _crispBackend.SelectedItem=_crispBackend.Items.Cast<CrispEmbedBackend>().FirstOrDefault(b=>b.Name==_options.CrispBackend) ?? _crispBackend.Items[0];
            RefreshEngineOptions(); RefreshCrispModels();
            _engine.SelectedIndexChanged+=(s,e)=>RefreshEngineOptions(); _crispBackend.SelectedIndexChanged+=(s,e)=>RefreshCrispModels();
            _mode.Items.AddRange(new object[]{"mobile","server"}); _mode.SelectedItem=_options.ModelMode; if(_mode.SelectedIndex<0) _mode.SelectedIndex=0;
            Set(_fps,_options.FramesPerSecond); Set(_brightness,_options.BrightnessMinimum); Set(_imageSimilarity,_options.ImageSimilarityPercent); Set(_textSimilarity,_options.TextSimilarityPercent); Set(_gap,_options.MaxGapMs); Set(_duration,_options.MinDurationMs);
            Set(_crop[0],(decimal)_options.CropXPercent); Set(_crop[1],(decimal)_options.CropYPercent); Set(_crop[2],(decimal)_options.CropWidthPercent); Set(_crop[3],(decimal)_options.CropHeightPercent);
            _dictionary.Items.Add(L("Не использовать","None")); foreach(var path in Directory.Exists(Utilities.DictionaryFolder) ? Directory.GetFiles(Utilities.DictionaryFolder,"*.dic") : new string[0]) _dictionary.Items.Add(Path.GetFileName(path));
            _dictionary.SelectedItem=_options.DictionaryFileName; if(_dictionary.SelectedIndex<0) _dictionary.SelectedIndex=0;
            _fix.Checked=_options.FixOcrErrors; _positionTag.Checked=_options.AddAssaPositionTag;
            _open.Click+=async(s,e)=> { using(var open=new OpenFileDialog{Filter=L("Видео|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.ts|Все файлы|*.*","Video|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.ts|All files|*.*")}) if(open.ShowDialog(this)==DialogResult.OK){_file.Text=open.FileName;await LoadVideo();} };
            Shown+=async(s,e)=> { if(File.Exists(_file.Text)) await LoadVideo(); };
            _position.MouseUp+=async(s,e)=> { if(_info==null) return; _updating=true; _seconds.Value=(decimal)(_info.TotalSeconds*_position.Value/10000.0); _updating=false; await UpdatePreview(); };
            _seconds.ValueChanged+=async(s,e)=> { if(_updating || _info==null) return; _updating=true; _position.Value=Math.Min(10000,(int)((double)_seconds.Value/_info.TotalSeconds*10000)); _updating=false; await UpdatePreview(); };
            foreach(var crop in _crop) crop.ValueChanged+=(s,e)=> {if(!_updating) _preview.Invalidate();};
            _preview.Paint+=PaintCrop; _preview.MouseDown+=(s,e)=> {if(_operation==null && e.Button==MouseButtons.Left) _dragStart=PointOnImage(e.Location);};
            _preview.MouseMove+=(s,e)=>DragCrop(e.Location); _preview.MouseUp+=(s,e)=> {_dragStart=null;};
            _test.Click+=async(s,e)=>await Run(true,false); _download.Click+=async(s,e)=>await Run(false,true); _start.Click+=async(s,e)=>await Run(false,false);
            _cancel.Click+=(s,e)=> {if(_operation!=null){_operation.Cancel();_status.Text=L("Отмена…","Cancelling…");}else Close();};
            _ok.Click+=(s,e)=> { if(_operation!=null) return; CollectRows(); if(SubtitleFromOcr.Paragraphs.Count>0) DialogResult=DialogResult.OK; };
            _lines.CellEndEdit+=(s,e)=> {if(e.RowIndex>=0) MarkSpelling(_lines.Rows[e.RowIndex]);};
            _lines.CellPainting+=PaintSpelling;
            _lines.CellDoubleClick+=async(s,e)=> {if(e.RowIndex>=0 && _operation==null){ var p=_lines.Rows[e.RowIndex].Tag as Paragraph; if(p!=null) _seconds.Value=(decimal)Math.Min(_info.TotalSeconds,p.StartTime.TotalSeconds+0.05); await UpdatePreview(); }};
            var menu=new ContextMenuStrip(); var italic=menu.Items.Add(L("Курсив","Italic")); italic.Click+=(s,e)=> {foreach(DataGridViewRow row in _lines.SelectedRows) row.Cells[3].Value="<i>"+row.Cells[3].Value+"</i>";};
            var delete=menu.Items.Add(L("Удалить","Delete")); delete.Click+=(s,e)=> {foreach(DataGridViewRow row in _lines.SelectedRows.Cast<DataGridViewRow>().ToArray()) _lines.Rows.Remove(row);}; _lines.ContextMenuStrip=menu;
            _lines.KeyDown+=(s,e)=> {if(e.KeyCode==Keys.Delete && !_lines.IsCurrentCellInEditMode){foreach(DataGridViewRow row in _lines.SelectedRows.Cast<DataGridViewRow>().ToArray()) _lines.Rows.Remove(row);e.Handled=true;}};
            FormClosing+=(s,e)=> {if(_operation!=null){_closeRequested=true;_operation.Cancel();e.Cancel=true;} else {_lifetime.Cancel();_previewRequest?.Cancel();}};
            _elapsedTimer.Tick+=(s,e)=>{if(_operation!=null) _status.Text=_phase+" · "+_watch.Elapsed.ToString(@"hh\:mm\:ss");};
            _ok.Enabled=false; UiUtil.FixFonts(this);
        }
        private static void AddSetting(TableLayoutPanel table,string text,Control control)
        {
            var row=table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label{Text=text,Dock=DockStyle.Fill,AutoSize=true,MinimumSize=new Size(0,22),TextAlign=ContentAlignment.MiddleLeft},0,row); table.Controls.Add(control,1,row);
        }
        private static void Set(NumericUpDown control,decimal value) {control.Value=Math.Max(control.Minimum,Math.Min(control.Maximum,value));}
        private void CaptureOptions()
        {
            _options.Language=((OcrLanguage2)_language.SelectedItem).Code; _options.Engine=_engine.Text;
            if(_options.Engine=="CrispEmbed")
            {
                _options.CrispVariant=(string)_variant.SelectedItem;
                _options.CrispBackend=((CrispEmbedBackend)_crispBackend.SelectedItem).Name;
                _options.CrispModel=((CrispEmbedModel)_crispModel.SelectedItem).Name;
            }
            else _options.Variant=(string)_variant.SelectedItem;
            _options.ModelMode=(string)_mode.SelectedItem;
            _options.FramesPerSecond=(int)_fps.Value; _options.BrightnessMinimum=(int)_brightness.Value; _options.ImageSimilarityPercent=(int)_imageSimilarity.Value; _options.TextSimilarityPercent=(int)_textSimilarity.Value;
            _options.MaxGapMs=(int)_gap.Value; _options.MinDurationMs=(int)_duration.Value; _options.CropXPercent=(double)_crop[0].Value; _options.CropYPercent=(double)_crop[1].Value; _options.CropWidthPercent=(double)_crop[2].Value; _options.CropHeightPercent=(double)_crop[3].Value;
            _options.FixOcrErrors=_fix.Checked; _options.DictionaryFileName=_dictionary.SelectedIndex>0 ? _dictionary.Text : ""; _options.AddAssaPositionTag=_positionTag.Checked; _options.Save();
        }
        private async Task LoadVideo()
        {
            try
            {
                var file=_file.Text;
                _info=await Task.Run(()=>UiUtil.GetVideoInfo(file));
                if(_info.Width<=0 || _info.Height<=0 || _info.TotalSeconds<=0) throw new InvalidDataException(L("Не удалось прочитать видео","Could not read the video"));
                if(_lifetime.IsCancellationRequested) return;
                _updating=true; _seconds.Maximum=(decimal)_info.TotalSeconds; _seconds.Value=(decimal)Math.Min(_info.TotalSeconds*.2,120); _position.Value=Math.Min(10000,(int)((double)_seconds.Value/_info.TotalSeconds*10000)); _updating=false;
                _status.Text=_info.Width+" × "+_info.Height+" · "+TimeSpan.FromSeconds(_info.TotalSeconds); await UpdatePreview();
            }
            catch(Exception ex) { _updating=false; _info=null; ShowError(ex); }
        }
        private Task UpdatePreview()
        {
            _previewRequest?.Cancel(); var request=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _previewRequest=request;
            _previewTask=RenderPreview(request); _previewTasks.Add(_previewTask);
            _previewTasks.RemoveAll(t=>t.IsCompleted); return _previewTask;
        }
        private async Task RenderPreview(CancellationTokenSource request)
        {
            var path=Path.Combine(_previewFolder,Guid.NewGuid().ToString("N")+".jpg");
            try
            {
                if(_info==null) return; Directory.CreateDirectory(_previewFolder);
                await VideoOcrProcess.RunFfmpegAsync("-nostdin -y -ss "+((double)_seconds.Value).ToString("0.###",CultureInfo.InvariantCulture)+" -i "+VideoOcrProcess.Quote(_file.Text)+" -frames:v 1 -q:v 2 "+VideoOcrProcess.Quote(path),_previewFolder,request.Token);
                if(request.IsCancellationRequested || IsDisposed) return;
                using(var image=Image.FromFile(path)){var old=_preview.Image;_preview.Image=new Bitmap(image);old?.Dispose();}
            }
            catch(OperationCanceledException) {}
            catch(Exception ex) { if(!IsDisposed && !_lifetime.IsCancellationRequested) _status.Text=ex.Message; }
            finally { if(File.Exists(path)) File.Delete(path); if(ReferenceEquals(_previewRequest,request)) _previewRequest=null; request.Dispose(); }
        }
        private RectangleF ImageBounds()
        {
            if(_preview.Image==null) return RectangleF.Empty;
            var scale=Math.Min(_preview.ClientSize.Width/(float)_preview.Image.Width,_preview.ClientSize.Height/(float)_preview.Image.Height);
            var width=_preview.Image.Width*scale; var height=_preview.Image.Height*scale;
            return new RectangleF((_preview.ClientSize.Width-width)/2,(_preview.ClientSize.Height-height)/2,width,height);
        }
        private PointF? PointOnImage(Point point)
        {
            var rect=ImageBounds(); if(rect.IsEmpty || !rect.Contains(point)) return null;
            return new PointF((point.X-rect.Left)/rect.Width*100,(point.Y-rect.Top)/rect.Height*100);
        }
        private void DragCrop(Point point)
        {
            if(!_dragStart.HasValue || _operation!=null) return; var end=PointOnImage(point); if(!end.HasValue) return;
            _updating=true; Set(_crop[0],(decimal)Math.Min(_dragStart.Value.X,end.Value.X)); Set(_crop[1],(decimal)Math.Min(_dragStart.Value.Y,end.Value.Y));
            Set(_crop[2],(decimal)Math.Abs(_dragStart.Value.X-end.Value.X)); Set(_crop[3],(decimal)Math.Abs(_dragStart.Value.Y-end.Value.Y)); _updating=false; _preview.Invalidate();
        }
        private void PaintCrop(object sender,PaintEventArgs e)
        {
            var rect=ImageBounds(); if(rect.IsEmpty) return;
            using(var pen=new Pen(Color.Lime,2)) e.Graphics.DrawRectangle(pen,rect.Left+rect.Width*(float)_crop[0].Value/100,rect.Top+rect.Height*(float)_crop[1].Value/100,
                rect.Width*(float)Math.Min(_crop[2].Value,100-_crop[0].Value)/100,rect.Height*(float)Math.Min(_crop[3].Value,100-_crop[1].Value)/100);
        }
        private async Task Run(bool probe,bool downloadOnly)
        {
            if(_operation!=null) return;
            if(!downloadOnly && _info==null){MessageBox.Show(this,L("Открой видео","Open a video"),Text);return;}
            CaptureOptions(); _operation=new CancellationTokenSource(); var cancel=_operation.Token;
            SetRunning(true); var watch=Stopwatch.StartNew(); _watch=watch; _elapsedTimer.Start(); long last=0;
            var progress=new Progress<SpeechProgress>(p=>
            {
                if(IsDisposed || _operation==null) return;
                if(watch.ElapsedMilliseconds-last<100 && p.Percent!=100) return; last=watch.ElapsedMilliseconds;
                _phase=TranslatePhase(p.Message ?? ""); _status.Text=_phase+" · "+watch.Elapsed.ToString(@"hh\:mm\:ss");
                if(p.Percent>=0){_progress.Style=ProgressBarStyle.Blocks;_progress.Value=Math.Max(0,Math.Min(100,p.Percent));}
                else _progress.Style=ProgressBarStyle.Marquee;
            });
            var job=Path.Combine(ClassicPaddleOcr.Root,"Jobs",Guid.NewGuid().ToString("N"));
            try
            {
                await ClassicVideoOcrPipeline.EnsureInstalledAsync(_options,cancel,progress);
                if(downloadOnly){_status.Text=L("Движок и модели установлены","Engine and models installed");return;}
                _fixEngine?.Dispose(); _rules?.Dispose();
                var language=ClassicVideoOcrRules.ThreeLetterLanguage(_options.Language);
                _rules=new ClassicVideoOcrRules(language);
                _fixEngine=new OcrFixEngine(language,_options.DictionaryFileName,this,false,_rules.ReplaceList);
                if(probe)
                {
                    Directory.CreateDirectory(job); var file=Path.Combine(job,"frame.jpg");
                    await VideoOcrProcess.RunFfmpegAsync("-nostdin -y -ss "+((double)_seconds.Value).ToString("0.###",CultureInfo.InvariantCulture)+" -i "+VideoOcrProcess.Quote(_file.Text)+" -vf "+VideoOcrProcess.Quote(_options.CropFilter(_info))+" -frames:v 1 -q:v 2 "+VideoOcrProcess.Quote(file),job,cancel);
                    var group=new VideoOcrFrameGroup{RepresentativeFileName=file};
                    await ClassicVideoOcrPipeline.RecognizeAsync(new[]{group},job,_options,cancel,progress);
                    _status.Text=FixRecognizedText(VideoOcrLineBuilder.CleanOcrResult(group.Text),new Subtitle(),0);
                    MessageBox.Show(this,string.IsNullOrWhiteSpace(_status.Text) ? L("Текст не обнаружен","No text detected") : _status.Text,L("Результат текущего кадра","Current frame result"));
                }
                else
                {
                    _lines.Rows.Clear(); SubtitleFromOcr=new Subtitle();
                    var acceptObservations=true;
                    var observations=new Progress<VideoOcrFrameGroup>(group=>
                    {
                        if(!acceptObservations || IsDisposed || _operation==null || cancel.IsCancellationRequested) return;
                        var text=VideoOcrLineBuilder.CleanOcrResult(group.Text); if(text.Length==0) return;
                        AddLine(new Paragraph(text,group.GetStartMs(_options.FramesPerSecond),group.GetEndMs(_options.FramesPerSecond)),false);
                    });
                    var context=new SortedDictionary<int,Paragraph>();
                    var result=await ClassicVideoOcrPipeline.ScanAsync(_file.Text,_info,_options,cancel,progress,(i,g)=>((IProgress<VideoOcrFrameGroup>)observations).Report(g),(i,g)=>
                    {
                        var p=new Paragraph(g.Text,g.GetStartMs(_options.FramesPerSecond),g.GetEndMs(_options.FramesPerSecond));
                        context[i]=p;var subtitle=new Subtitle();subtitle.Paragraphs.AddRange(context.Values);
                        p.Text=FixRecognizedText(p.Text,subtitle,subtitle.Paragraphs.IndexOf(p)); return p.Text;
                    });
                    acceptObservations=false;
                    cancel.ThrowIfCancellationRequested(); _lines.Rows.Clear();
                    foreach(var p in result.Paragraphs) AddLine(p,false);
                    CollectRows(); _status.Text=L("Субтитров: ","Subtitle cues: ")+SubtitleFromOcr.Paragraphs.Count+" · "+watch.Elapsed.ToString(@"hh\:mm\:ss");
                }
            }
            catch(OperationCanceledException) { _status.Text=L("OCR отменён","OCR cancelled"); if(!probe && !downloadOnly){_lines.Rows.Clear(); SubtitleFromOcr=new Subtitle();} }
            catch(Exception ex) { ShowError(ex); if(!probe && !downloadOnly){_lines.Rows.Clear(); SubtitleFromOcr=new Subtitle();} }
            finally
            {
                _elapsedTimer.Stop(); _operation.Dispose(); _operation=null; SetRunning(false);
                if(Directory.Exists(job)) Directory.Delete(job,true);
                if(_closeRequested) Close();
            }
        }
        private void SetRunning(bool running)
        {
            _settings.Enabled=_open.Enabled=_position.Enabled=_seconds.Enabled=_test.Enabled=_start.Enabled=_lines.Enabled=!running;
            _ok.Enabled=!running && _lines.Rows.Count>0; _progress.Style=running ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        }
        private void RefreshEngineOptions()
        {
            var crisp=_engine.Text=="CrispEmbed";
            _variant.Items.Clear();_variant.Items.AddRange(crisp ? new object[]{"cpu","vulkan","cuda"} : new object[]{"cpu","cuda12"});
            _variant.SelectedItem=crisp ? _options.CrispVariant : _options.Variant; if(_variant.SelectedIndex<0)_variant.SelectedIndex=0;
            SetSettingRowVisible(_mode,!crisp);SetSettingRowVisible(_crispBackend,crisp);SetSettingRowVisible(_crispModel,crisp);
        }
        private void SetSettingRowVisible(Control control,bool visible)
        {
            var row=_settingsTable.GetRow(control);
            _settingsTable.RowStyles[row].SizeType=visible ? SizeType.AutoSize : SizeType.Absolute;
            _settingsTable.RowStyles[row].Height=0;
            foreach(Control item in _settingsTable.Controls) if(_settingsTable.GetRow(item)==row)item.Visible=visible;
        }
        private void RefreshCrispModels()
        {
            _crispModel.Items.Clear();
            if(!(_crispBackend.SelectedItem is CrispEmbedBackend backend)) return;
            foreach(var model in backend.Models)_crispModel.Items.Add(model);
            _crispModel.SelectedItem=backend.Models.FirstOrDefault(m=>m.Name==_options.CrispModel) ?? backend.Models[0];
        }
        private void AddLine(Paragraph paragraph,bool fix)
        {
            if(fix && _options.FixOcrErrors && _fixEngine!=null)
            {
                var context=new Subtitle(); foreach(DataGridViewRow row in _lines.Rows) if(row.Tag is Paragraph p) context.Paragraphs.Add(p); context.Paragraphs.Add(paragraph);
                paragraph.Text=_fixEngine.FixOcrErrors(paragraph.Text,context,context.Paragraphs.Count-1,context.GetParagraphOrDefault(context.Paragraphs.Count-2)?.Text ?? "",context.GetParagraphOrDefault(context.Paragraphs.Count-3)?.Text ?? "",false,OcrFixEngine.AutoGuessLevel.None);
                if(Configuration.Settings.Tools.OcrFixUseHardcodedRules) paragraph.Text=ClassicVideoOcrRules.NormalizeApostrophes(paragraph.Text);
            }
            var index=_lines.Rows.Add(paragraph.StartTime.ToDisplayString(),paragraph.EndTime.ToDisplayString(),paragraph.Duration.TotalSeconds.ToString("0.000"),paragraph.Text);
            _lines.Rows[index].Tag=paragraph; MarkSpelling(_lines.Rows[index]);
        }
        private void MarkSpelling(DataGridViewRow row)
        {
            if(_fixEngine==null || _dictionary.SelectedIndex<=0) return;
            var text=Regex.Replace(Convert.ToString(row.Cells[3].Value),"<[^>]+>|\\{[^}]+\\}","");
            bool unknown; lock(_fixEngine) unknown=Regex.Matches(text,@"\p{L}+").Cast<Match>().Any(m=>!_fixEngine.DoSpell(m.Value));
            row.Cells[3].Style.ForeColor=unknown ? Color.Firebrick : Color.DarkGreen;
        }
        private void PaintSpelling(object sender,DataGridViewCellPaintingEventArgs e)
        {
            if(e.RowIndex<0 || e.ColumnIndex!=3 || _fixEngine==null || _dictionary.SelectedIndex<=0 || _lines.IsCurrentCellInEditMode && _lines.CurrentCell.RowIndex==e.RowIndex) return;
            e.PaintBackground(e.ClipBounds,true); e.Paint(e.ClipBounds,DataGridViewPaintParts.Border);
            var font=e.CellStyle.Font ?? _lines.Font;
            var x=e.CellBounds.Left+4; var y=e.CellBounds.Top+3; var right=e.CellBounds.Right-4;
            var flags=TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine;
            foreach(Match token in Regex.Matches(Convert.ToString(e.FormattedValue) ?? "",@"\p{L}+|\r\n|\n|[^\p{L}\r\n]+"))
            {
                if(token.Value.Contains("\n")){x=e.CellBounds.Left+4;y+=font.Height;continue;}
                var width=TextRenderer.MeasureText(e.Graphics,token.Value,font,Size.Empty,flags).Width;
                if(x+width>right && x>e.CellBounds.Left+4){x=e.CellBounds.Left+4;y+=font.Height;}
                var word=char.IsLetter(token.Value[0]);
                var color=e.CellStyle.ForeColor;
                if(word) lock(_fixEngine) color=_fixEngine.DoSpell(token.Value) ? Color.DarkGreen : Color.Firebrick;
                if(y+font.Height<=e.CellBounds.Bottom) TextRenderer.DrawText(e.Graphics,token.Value,font,new Point(x,y),color,flags);
                x+=width;
            }
            e.Handled=true;
        }
        private string FixRecognizedText(string text,Subtitle context,int index)
        {
            if(!_options.FixOcrErrors || _fixEngine==null || string.IsNullOrWhiteSpace(text)) return text;
            if(context.Paragraphs.Count==0) context.Paragraphs.Add(new Paragraph(text,0,1));
            lock(_fixEngine)
            {
                text=_fixEngine.FixOcrErrors(text,context,index,context.GetParagraphOrDefault(index-1)?.Text ?? "",context.GetParagraphOrDefault(index-2)?.Text ?? "",false,OcrFixEngine.AutoGuessLevel.None);
                return Configuration.Settings.Tools.OcrFixUseHardcodedRules ? ClassicVideoOcrRules.NormalizeApostrophes(text) : text;
            }
        }
        private static string TranslatePhase(string phase)
        {
            if(phase=="Extracting video frames") return L("Извлечение кадров","Extracting video frames");
            if(phase=="Grouping frames") return L("Группировка кадров","Grouping frames");
            if(phase=="Refining subtitle timings") return L("Уточнение таймингов","Refining subtitle timings");
            if(phase.StartsWith("Extracting ",StringComparison.Ordinal)) return L("Распаковка ","Extracting ")+phase.Substring(11);
            return phase;
        }
        private void CollectRows()
        {
            var result=new Subtitle(); foreach(DataGridViewRow row in _lines.Rows) if(row.Tag is Paragraph source){var p=new Paragraph(source);p.Text=Convert.ToString(row.Cells[3].Value) ?? "";if(!string.IsNullOrWhiteSpace(p.Text))result.Paragraphs.Add(p);} result.Renumber(); SubtitleFromOcr=result;
        }
        private void ShowError(Exception ex)
        {
            SeLogger.WhisperInfo("Video OCR: "+ex);
            MessageBox.Show(this,ex.Message.Split('\n')[0],Text,MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                _elapsedTimer.Dispose();_lifetime.Cancel();_previewRequest?.Cancel();_fixEngine?.Dispose();_rules?.Dispose();_preview.Image?.Dispose();
                Task.WhenAll(_previewTasks.ToArray()).ContinueWith(t=>{try{if(Directory.Exists(_previewFolder))Directory.Delete(_previewFolder,true);}catch(IOException){}catch(UnauthorizedAccessException){}});
            }
            base.Dispose(disposing);
        }
    }
}

