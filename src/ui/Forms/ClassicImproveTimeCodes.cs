using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicTiming;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicImproveTimeCodes : Form
    {
        private readonly Subtitle _source;
        private readonly int[] _indices;
        private readonly string _video;
        private readonly double _seconds,_fps;
        private readonly int _audioTrack;
        private readonly DataGridView _grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false};
        private readonly ComboBox _compute=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=100};
        private readonly ComboBox _model=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=210};
        private readonly ComboBox _language=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=105};
        private readonly NumericUpDown _shift=new NumericUpDown{Minimum=0.1m,Maximum=10,DecimalPlaces=2,Value=0.5m,Increment=0.1m,Width=65};
        private readonly CheckBox _check=new CheckBox{AutoSize=true,Checked=true},_starts=new CheckBox{AutoSize=true,Checked=true},_ends=new CheckBox{AutoSize=true,Checked=true},_snap=new CheckBox{AutoSize=true};
        private readonly Label _status=new Label{Dock=DockStyle.Fill,AutoEllipsis=true};
        private readonly ProgressBar _progress=new ProgressBar{Dock=DockStyle.Fill};
        private readonly Button _run=new Button{AutoSize=true},_apply=new Button{AutoSize=true,Enabled=false},_cancel=new Button{AutoSize=true};
        private readonly Action<double> _preview;
        private CancellationTokenSource _operation;
        private SubtitleRetimer.LineResult[] _results;
        public Subtitle EditedSubtitle { get; private set; }
        private static string L(string ru,string en)=>Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase) ? ru : en;
        public ClassicImproveTimeCodes(Subtitle source,string video,double seconds,double fps,Action<double> preview=null,int audioTrack=0)
        {
            UiUtil.PreInitialize(this);_source=new Subtitle(source);_video=video;_seconds=seconds;_fps=fps;_preview=preview;_audioTrack=audioTrack;
            _indices=Enumerable.Range(0,source.Paragraphs.Count).OrderBy(i=>source.Paragraphs[i].StartTime.TotalMilliseconds).ToArray();
            Text=L("Улучшить тайминги — выравнивание по речи","Improve time codes — forced alignment");ClientSize=new Size(1080,650);MinimumSize=new Size(940,480);StartPosition=FormStartPosition.CenterParent;
            _compute.Items.AddRange(new object[]{"cpu","cpu-legacy","vulkan","cuda","cuda13"});_compute.SelectedIndex=0;
            _model.Items.AddRange(new object[]{"Canary CTC (25 languages)","Qwen3 (10 languages)"});_model.SelectedIndex=0;
            _language.Items.AddRange(new object[]{"auto","ru","en","de","fr","es","it","pt","uk"});_language.SelectedIndex=0;
            _check.Text=L("Проверять распознаванием речи","Check with speech-to-text");_starts.Text=L("Начало","Start times");_ends.Text=L("Конец","End times");_snap.Text=L("Привязать к кадрам","Snap to frames");
            var options=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Padding=new Padding(5)};
            foreach(var c in new Control[]{Caption(L("Движок","Compute")),_compute,Caption(L("Модель","Model")),_model,Caption(L("Язык","Language")),_language,Caption(L("Макс. сдвиг, с","Max shift, s")),_shift,_starts,_ends,_snap,_check})options.Controls.Add(c);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(8)};
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,8));root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(options,0,0);root.Controls.Add(_grid,0,1);root.Controls.Add(_progress,0,2);root.Controls.Add(_status,0,3);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft};_run.Text=L("Рассчитать","Analyze");_apply.Text=L("Применить выбранные","Apply selected");_cancel.Text=L("Отмена","Cancel");buttons.Controls.AddRange(new Control[]{_cancel,_apply,_run});root.Controls.Add(buttons,0,4);Controls.Add(root);
            _grid.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText=L("Применить","Apply"),Width=82});
            foreach(var name in new[]{L("Старое начало","Old start"),L("Старый конец","Old end"),L("Новое начало","New start"),L("Новый конец","New end"),L("Результат","Result"),L("Текст","Text")})_grid.Columns.Add(name,name);
            for(int i=1;i<_grid.Columns.Count;i++){_grid.Columns[i].ReadOnly=i!=3 && i!=4;_grid.Columns[i].Width=i==5 ? 160 : 100;}
            _grid.Columns[6].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
            foreach(var index in _indices){var p=_source.Paragraphs[index];_grid.Rows.Add(false,p.StartTime.ToString(),p.EndTime.ToString(),p.StartTime.ToString(),p.EndTime.ToString(),"",p.Text);}
            _run.Click+=async(s,e)=>await Analyze();_apply.Click+=(s,e)=>Apply();_cancel.Click+=(s,e)=>{if(_operation!=null)_operation.Cancel();else Close();};
            _grid.CellDoubleClick+=(s,e)=>{var column=e.ColumnIndex>=1 && e.ColumnIndex<=4 ? e.ColumnIndex : 3;if(_operation==null && e.RowIndex>=0 && _preview!=null && TrySeconds(Convert.ToString(_grid.Rows[e.RowIndex].Cells[column].Value),out var at))_preview(column==2 || column==4 ? Math.Max(0,at-1) : at);};
            _grid.CellEndEdit+=(s,e)=>{if(_results!=null && e.RowIndex>=0 && (e.ColumnIndex==3 || e.ColumnIndex==4)){_results[e.RowIndex]=_results[e.RowIndex] with {Status=SubtitleRetimer.LineStatus.AdjustedByHand};_grid.Rows[e.RowIndex].Cells[0].Value=true;_grid.Rows[e.RowIndex].Cells[5].Value=L("Изменено вручную","Adjusted by hand");}};
            FormClosing+=(s,e)=>{if(_operation!=null){_operation.Cancel();e.Cancel=true;}};
            _status.Text=L("Дважды нажми время, чтобы прослушать 5 секунд в основном плеере. Спорные изменения будут сняты с выбора.","Double-click a time to play 5 seconds in the main player. Disputed changes are initially unchecked.");UiUtil.FixFonts(this);
        }
        private static Label Caption(string text)=>new Label{Text=text,AutoSize=true,Margin=new Padding(5,6,3,3)};
        private async Task Analyze()
        {
            if(_operation!=null)return;_operation=new CancellationTokenSource();_run.Enabled=_apply.Enabled=false;_grid.Enabled=false;
            try
            {
                var source=new Subtitle(_source);source.Paragraphs.Clear();source.Paragraphs.AddRange(_indices.Select(i=>new Paragraph(_source.Paragraphs[i],false)));
                var general=Configuration.Settings.General;
                var options=new SubtitleRetimer.Options{MaxShiftSeconds=(double)_shift.Value,AdjustStart=_starts.Checked,AdjustEnd=_ends.Checked,MinDurationSeconds=general.SubtitleMinimumDisplayMilliseconds/1000.0,MaxDurationSeconds=general.SubtitleMaximumDisplayMilliseconds/1000.0,ReadingCharsPerSecond=(double)general.SubtitleOptimalCharactersPerSeconds,MaxCharsPerSecond=(double)general.SubtitleMaximumCharactersPerSeconds,MinGapSeconds=general.MinimumMillisecondsBetweenLines/1000.0};
                _results=await new ClassicRetimingJob().RunAsync(source,_video,_seconds,_compute.Text,_model.SelectedIndex==1,_check.Checked,_language.Text,options,_operation.Token,new Progress<SpeechProgress>(p=>{_status.Text=p.Message;if(p.Percent>=0)_progress.Value=Math.Max(0,Math.Min(100,p.Percent));}),_audioTrack);
                for(int i=0;i<_results.Length;i++){var r=_results[i];var row=_grid.Rows[i];row.Cells[0].Value=SubtitleRetimer.IsMove(r.Status) && !SubtitleRetimer.IsUnconfirmed(r.Status);row.Cells[3].Value=Format(r.StartSeconds);row.Cells[4].Value=Format(r.EndSeconds);row.Cells[5].Value=StatusText(r.Status)+(r.HeardRatio.HasValue ? " · "+r.HeardRatio.Value.ToString("P0") : "");}
                _apply.Enabled=true;_status.Text=L("Проверь изменения и выбери реплики для применения. Исходные тайминги пока не изменены.","Review and select the proposed changes. Original time codes have not changed.");
                var words=0;var heard=0.0;
                for(int i=0;i<_results.Length;i++)if(_results[i].HeardRatio.HasValue){var count=SubtitleRetimer.GetSpokenText(source.Paragraphs[i].Text).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Length;words+=count;heard+=count*_results[i].HeardRatio.Value;}
                if(words>0 && heard/words<0.3)_status.Text=L("В аудио найдено мало слов из субтитров. Проверь, что текст соответствует речи и выбрана нужная аудиодорожка.","Few subtitle words were heard. Check that the text matches the speech and the correct audio track is selected.");
            }
            catch(OperationCanceledException){_status.Text=L("Отменено","Cancelled");}
            catch(Exception ex){_status.Text=ex.Message;MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
            finally{_operation.Dispose();_operation=null;_run.Enabled=_grid.Enabled=true;}
        }
        private void Apply()
        {
            if(_results==null)return;_grid.EndEdit();
            var original=_indices.Select(i=>new SubtitleRetimer.Line(_source.Paragraphs[i].Text,_source.Paragraphs[i].StartTime.TotalSeconds,_source.Paragraphs[i].EndTime.TotalSeconds)).ToArray();
            var placed=new SubtitleRetimer.Placement[original.Length];
            for(int i=0;i<placed.Length;i++)
            {
                var selected=(bool?)_grid.Rows[i].Cells[0].Value==true;
                if(selected)
                {
                    if(!TrySeconds(Convert.ToString(_grid.Rows[i].Cells[3].Value),out var start) || !TrySeconds(Convert.ToString(_grid.Rows[i].Cells[4].Value),out var end) || start<0 || end<=start || end>_seconds+0.1)
                    {MessageBox.Show(this,L("Проверь время в строке ","Check time codes in row ")+(i+1));return;}
                    placed[i]=new SubtitleRetimer.Placement(start,end,_results[i].Status!=SubtitleRetimer.LineStatus.AdjustedByHand);
                }
                else placed[i]=new SubtitleRetimer.Placement(original[i].StartSeconds,original[i].EndSeconds,false);
            }
            SubtitleRetimer.Settle(original,placed,Configuration.Settings.General.MinimumMillisecondsBetweenLines/1000.0);
            if(_snap.Checked)SubtitleRetimer.SnapToFrames(original,placed,_fps,Configuration.Settings.General.MinimumMillisecondsBetweenLines/1000.0);
            EditedSubtitle=new Subtitle(_source);
            for(int i=0;i<placed.Length;i++){var p=EditedSubtitle.Paragraphs[_indices[i]];p.StartTime.TotalMilliseconds=placed[i].StartSeconds*1000;p.EndTime.TotalMilliseconds=placed[i].EndSeconds*1000;}
            DialogResult=DialogResult.OK;
        }
        private static string Format(double seconds)=>TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\,fff",CultureInfo.InvariantCulture);
        private static string StatusText(SubtitleRetimer.LineStatus status)
        {
            switch(status)
            {
                case SubtitleRetimer.LineStatus.Retimed:return L("Выровнено","Retimed");
                case SubtitleRetimer.LineStatus.MovedWithNeighbours:return L("Сдвинуто с соседними","Moved with neighbours");
                case SubtitleRetimer.LineStatus.LargeMoveUnconfirmed:return L("Проверь большой сдвиг","Check large shift");
                case SubtitleRetimer.LineStatus.NoSpeech:return L("Неречевая строка","No speech");
                case SubtitleRetimer.LineStatus.ShiftTooLarge:return L("Слишком большой сдвиг","Shift too large");
                case SubtitleRetimer.LineStatus.Failed:return L("Ошибка выравнивания","Alignment failed");
                case SubtitleRetimer.LineStatus.ConfirmedBySpeech:return L("Подтверждено речью","Confirmed by speech");
                case SubtitleRetimer.LineStatus.DisputedBySpeech:return L("Спорный результат","Disputed by speech");
                case SubtitleRetimer.LineStatus.MovedWithSync:return L("Синхронизировано","Synced first");
                case SubtitleRetimer.LineStatus.AdjustedByHand:return L("Изменено вручную","Adjusted by hand");
                default:return L("Без изменений","Unchanged");
            }
        }
        private static bool TrySeconds(string text,out double seconds){seconds=0;if(!TimeSpan.TryParseExact(text,@"hh\:mm\:ss\,fff",CultureInfo.InvariantCulture,out var span))return false;seconds=span.TotalSeconds;return true;}
    }
}
