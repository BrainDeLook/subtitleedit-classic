using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed class ClassicQwenCloneSettings : Form
    {
        private static bool Ru => Nikse.SubtitleEdit.Core.Common.Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase);
        private static string L(string ru,string en)=>Ru ? ru : en;
        public ClassicQwenCloneSettings(ClassicQwenTtsSettings settings)
        {
            Text=L("Модель и клонирование голоса","Model and voice cloning");Size=new Size(620,450);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;
            var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=7};
            table.RowStyles.Add(new RowStyle(SizeType.Absolute,30));table.RowStyles.Add(new RowStyle(SizeType.Absolute,32));table.RowStyles.Add(new RowStyle(SizeType.Absolute,70));table.RowStyles.Add(new RowStyle(SizeType.Absolute,30));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));table.RowStyles.Add(new RowStyle(SizeType.Absolute,40));table.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            var model=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};model.Items.AddRange(new object[]{"1.7B CustomVoice","1.7B Voice clone","1.7B VoiceDesign"});model.SelectedIndex=settings.Design ? 2 : settings.Clone ? 1 : 0;
            table.Controls.Add(new Label{Text=L("Модель Qwen3 TTS","Qwen3 TTS model"),AutoSize=true},0,0);table.Controls.Add(model,0,1);
            table.Controls.Add(new Label{Text=L("Voice clone использует запись одного голоса и точный текст этой записи. Импорт преобразует образец в WAV 24 кГц, моно. Озвучка создаётся локально и является синтетической.","Voice clone requires a single-speaker recording and its exact transcript. Import converts it to 24 kHz mono WAV. Speech is generated locally and is synthetic."),Dock=DockStyle.Fill},0,2);
            table.Controls.Add(new Label{Text=L("Стиль готового голоса (не применяется к клонированию)","CustomVoice style (not applied to cloning)"),AutoSize=true},0,3);
            var style=new TextBox{Dock=DockStyle.Fill,Multiline=true,Text=settings.Instructions};table.Controls.Add(style,0,4);
            var import=new Button{Text=L("Импортировать образец голоса…","Import voice reference…"),AutoSize=true};
            import.Click+=(s,e)=>{using(var dialog=new ClassicQwenReferenceImport())if(dialog.ShowDialog(this)==DialogResult.OK){settings.Voice=dialog.Reference.Id;model.SelectedIndex=1;}};
            table.Controls.Add(import,0,5);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var ok=new Button{Text="OK",DialogResult=DialogResult.OK};var cancel=new Button{Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel};buttons.Controls.Add(cancel);buttons.Controls.Add(ok);table.Controls.Add(buttons,0,6);
            ok.Click+=(s,e)=>{settings.Clone=model.SelectedIndex==1;settings.Design=model.SelectedIndex==2;settings.Instructions=style.Text;settings.Save();};Controls.Add(table);AcceptButton=ok;CancelButton=cancel;
        }
    }
    public sealed class ClassicQwenReferenceImport : Form
    {
        public ClassicQwenReference Reference { get; private set; }
        private CancellationTokenSource _operation;
        private bool _closeRequested;
        private int _audioTrack;
        public ClassicQwenReferenceImport(string source, Paragraph range, int audioTrack) : this(false)
        {
            if(range==null || range.StartTime.TotalSeconds<0 || range.DurationTotalSeconds<1 || range.DurationTotalSeconds>30)throw new ArgumentException("Select a 1–30 second voice reference range.");
            _audioTrack=Math.Max(0,audioTrack);
            ((TextBox)Controls.Find("textReferencePath",true)[0]).Text=source;
            ((NumericUpDown)Controls.Find("referenceStart",true)[0]).DecimalPlaces=3;
            ((NumericUpDown)Controls.Find("referenceStart",true)[0]).Value=(decimal)range.StartTime.TotalSeconds;
            ((NumericUpDown)Controls.Find("referenceDuration",true)[0]).DecimalPlaces=3;
            ((NumericUpDown)Controls.Find("referenceDuration",true)[0]).Value=(decimal)range.DurationTotalSeconds;
            ((TextBox)Controls.Find("textTranscript",true)[0]).Text=Nikse.SubtitleEdit.Logic.ClassicCast.ClassicVideoVoices.Transcript(new[]{range});
            ((TextBox)Controls.Find("textVoiceName",true)[0]).Text=string.IsNullOrWhiteSpace(range.Actor) ? "Voice " + range.StartTime.ToShortDisplayString().Replace(':','-') : range.Actor;
            var ru=Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase);
            var sourceLabel=Controls.Find("labelReferenceSource",true)[0];sourceLabel.Text=(ru ? "Аудиодорожка: " : "Audio track: ")+(_audioTrack+1);
            sourceLabel.Visible=true;
        }
        public ClassicQwenReferenceImport(ClassicQwenReference voice, bool requireTranscript = true) : this(requireTranscript)
        {
            ClassicQwenReference.Validate(voice.Id, requireTranscript);
            var file=System.IO.Path.Combine(ClassicQwenReference.Folder,voice.Id+".wav");
            ((TextBox)Controls.Find("textReferencePath",true)[0]).Text=file;
            ((TextBox)Controls.Find("textVoiceName",true)[0]).Text=voice.Name+" (clip)";
            ((TextBox)Controls.Find("textTranscript",true)[0]).Text=System.IO.File.ReadAllText(System.IO.Path.ChangeExtension(file,".txt"));
            ((NumericUpDown)Controls.Find("referenceDuration",true)[0]).Value=(decimal)Math.Min(8,Nikse.SubtitleEdit.Logic.ClassicCast.ClassicVoiceAssets.WaveSeconds(file));
            ((CheckBox)Controls.Find("referenceConsent",true)[0]).Checked=voice.Consent;
        }
        public ClassicQwenReferenceImport(bool requireTranscript = true)
        {
            var ru=Nikse.SubtitleEdit.Core.Common.Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase);Func<string,string,string> l=(r,e)=>ru ? r : e;
            Text=l("Импорт голоса","Import voice");Size=new Size(650,500);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;
            var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(12)};
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));foreach(var height in new[]{30,32,35,28})table.RowStyles.Add(new RowStyle(SizeType.Absolute,height));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));table.RowStyles.Add(new RowStyle(SizeType.Absolute,45));table.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            var path=new TextBox{Name="textReferencePath",ReadOnly=true,Dock=DockStyle.Fill};var open=new Button{Text=l("Выбрать аудио или видео…","Select audio or video…"),AutoSize=true};open.Click+=(s,e)=>{using(var picker=new OpenFileDialog{Filter="Media|*.wav;*.mp3;*.flac;*.m4a;*.ogg;*.mp4;*.mkv;*.mov;*.webm|All files|*.*"})if(picker.ShowDialog(this)==DialogResult.OK){_audioTrack=0;path.Text=picker.FileName;var labels=Controls.Find("labelReferenceSource",true);if(labels.Length>0)labels[0].Visible=false;}};
            var transcribe = new Button { Name="buttonTranscribeImportReference",Text=l("Распознать текст…","Transcribe reference…"),AutoSize=true };
            var sourceBar = new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};sourceBar.Controls.AddRange(new Control[]{open,transcribe});
            sourceBar.Controls.Add(new Label{Name="labelReferenceSource",AutoSize=true,Visible=false,Margin=new Padding(3,8,3,3)});
            table.Controls.Add(sourceBar,0,0);table.Controls.Add(path,0,1);
            var name=new TextBox{Name="textVoiceName",Dock=DockStyle.Fill};var nameBar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};nameBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));nameBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));nameBar.Controls.Add(new Label{Text=l("Имя голоса","Voice name"),AutoSize=true},0,0);nameBar.Controls.Add(name,1,0);table.Controls.Add(nameBar,0,2);
            var start=new NumericUpDown{Name="referenceStart",DecimalPlaces=2,Maximum=86400,Width=100};var duration=new NumericUpDown{Name="referenceDuration",Minimum=1,Maximum=30,Value=8,DecimalPlaces=2,Width=80};var range=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};range.Controls.Add(new Label{Text=l("Начало, сек","Start, sec"),AutoSize=true});range.Controls.Add(start);range.Controls.Add(new Label{Text=l("Длительность, сек","Duration, sec"),AutoSize=true});range.Controls.Add(duration);table.Controls.Add(range,0,3);
            table.Controls.Add(new Label{Text=requireTranscript ? l("Точный текст выбранного фрагмента записи:","Exact transcript of the selected recording range:") : l("Текст образца (необязательно для этого движка):", "Reference transcript (optional for this engine):"),AutoSize=true},0,4);var transcript=new TextBox{Name="textTranscript",Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical};table.Controls.Add(transcript,0,5);
            var consent=new CheckBox{Name="referenceConsent",Text=l("Это мой голос или у меня есть разрешение владельца голоса.","This is my voice or I have the speaker's permission."),Dock=DockStyle.Fill};table.Controls.Add(consent,0,6);path.TextChanged+=(s,e)=>consent.Checked=false;
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=l("Отмена","Cancel")};var import=new Button{Text=l("Импорт","Import")};buttons.Controls.Add(cancel);buttons.Controls.Add(import);table.Controls.Add(buttons,0,7);
            cancel.Click+=(s,e)=>{if(_operation!=null)_operation.Cancel();else Close();};FormClosing+=(s,e)=>{if(_operation!=null){_closeRequested=true;_operation.Cancel();e.Cancel=true;}};
            transcribe.Click+=async(s,e)=>{
                if(_operation!=null)return;_operation=new CancellationTokenSource();var job=Path.Combine(Path.GetTempPath(),"se-reference-asr-"+Guid.NewGuid().ToString("N"));import.Enabled=open.Enabled=transcribe.Enabled=false;
                try{
                    var clip=await ClassicReferenceAudio.ExtractAsync(path.Text,(double)start.Value,(double)duration.Value,job,_operation.Token,_audioTrack);
                    _operation.Token.ThrowIfCancellationRequested();
                    using(var asr=new WhisperAudioToText(clip,new Subtitle(),0,this,null))if(asr.ShowDialog(this)==DialogResult.OK){var result=ClassicReferenceAudio.Transcript(asr.TranscribedSubtitle);if(!string.IsNullOrWhiteSpace(result))transcript.Text=result;}
                }catch(OperationCanceledException){}catch(Exception error){MessageBox.Show(this,error.Message,Text);}
                finally{try{if(Directory.Exists(job))Directory.Delete(job,true);}catch(IOException){}catch(UnauthorizedAccessException){}_operation.Dispose();_operation=null;import.Enabled=open.Enabled=transcribe.Enabled=true;if(_closeRequested)Close();}
            };
            import.Click+=async(s,e)=>{_operation=new CancellationTokenSource();import.Enabled=open.Enabled=transcribe.Enabled=false;try{Reference=await ClassicQwenReference.ImportAsync(path.Text,name.Text,transcript.Text,(double)start.Value,(double)duration.Value,consent.Checked,_operation.Token, requireTranscript,_audioTrack);}catch(OperationCanceledException){}catch(Exception error){MessageBox.Show(this,error.Message,Text);}finally{_operation.Dispose();_operation=null;import.Enabled=open.Enabled=transcribe.Enabled=true;if(Reference!=null){DialogResult=DialogResult.OK;Close();}else if(_closeRequested)Close();}};Controls.Add(table);AcceptButton=import;
        }
    }
}
