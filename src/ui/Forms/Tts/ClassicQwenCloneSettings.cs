using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

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
            var model=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};model.Items.AddRange(new object[]{"1.7B CustomVoice","1.7B Voice clone"});model.SelectedIndex=settings.Clone ? 1 : 0;
            table.Controls.Add(new Label{Text=L("Модель Qwen3 TTS","Qwen3 TTS model"),AutoSize=true},0,0);table.Controls.Add(model,0,1);
            table.Controls.Add(new Label{Text=L("Voice clone использует запись одного голоса и точный текст этой записи. Импорт преобразует образец в WAV 24 кГц, моно. Озвучка создаётся локально и является синтетической.","Voice clone requires a single-speaker recording and its exact transcript. Import converts it to 24 kHz mono WAV. Speech is generated locally and is synthetic."),Dock=DockStyle.Fill},0,2);
            table.Controls.Add(new Label{Text=L("Стиль готового голоса (не применяется к клонированию)","CustomVoice style (not applied to cloning)"),AutoSize=true},0,3);
            var style=new TextBox{Dock=DockStyle.Fill,Multiline=true,Text=settings.Instructions};table.Controls.Add(style,0,4);
            var import=new Button{Text=L("Импортировать образец голоса…","Import voice reference…"),AutoSize=true};
            import.Click+=(s,e)=>{using(var dialog=new ClassicQwenReferenceImport())if(dialog.ShowDialog(this)==DialogResult.OK){settings.Voice=dialog.Reference.Id;model.SelectedIndex=1;}};
            table.Controls.Add(import,0,5);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var ok=new Button{Text="OK",DialogResult=DialogResult.OK};var cancel=new Button{Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel};buttons.Controls.Add(cancel);buttons.Controls.Add(ok);table.Controls.Add(buttons,0,6);
            ok.Click+=(s,e)=>{settings.Clone=model.SelectedIndex==1;settings.Instructions=style.Text;settings.Save();};Controls.Add(table);AcceptButton=ok;CancelButton=cancel;
        }
    }
    public sealed class ClassicQwenReferenceImport : Form
    {
        public ClassicQwenReference Reference { get; private set; }
        private CancellationTokenSource _operation;
        public ClassicQwenReferenceImport()
        {
            var ru=Nikse.SubtitleEdit.Core.Common.Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase);Func<string,string,string> l=(r,e)=>ru ? r : e;
            Text=l("Импорт голоса","Import voice");Size=new Size(650,500);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;
            var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(12)};
            foreach(var height in new[]{30,30,32,35,28})table.RowStyles.Add(new RowStyle(SizeType.Absolute,height));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));table.RowStyles.Add(new RowStyle(SizeType.Absolute,45));table.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            var path=new TextBox{ReadOnly=true,Dock=DockStyle.Fill};var open=new Button{Text=l("Выбрать аудио или видео…","Select audio or video…"),AutoSize=true};open.Click+=(s,e)=>{using(var picker=new OpenFileDialog{Filter="Media|*.wav;*.mp3;*.flac;*.m4a;*.ogg;*.mp4;*.mkv;*.mov;*.webm|All files|*.*"})if(picker.ShowDialog(this)==DialogResult.OK)path.Text=picker.FileName;};
            table.Controls.Add(open,0,0);table.Controls.Add(path,0,1);
            var name=new TextBox{Dock=DockStyle.Fill};var nameBar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};nameBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));nameBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));nameBar.Controls.Add(new Label{Text=l("Имя голоса","Voice name"),AutoSize=true},0,0);nameBar.Controls.Add(name,1,0);table.Controls.Add(nameBar,0,2);
            var start=new NumericUpDown{DecimalPlaces=2,Maximum=86400,Width=100};var duration=new NumericUpDown{Minimum=1,Maximum=30,Value=8,DecimalPlaces=2,Width=80};var range=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};range.Controls.Add(new Label{Text=l("Начало, сек","Start, sec"),AutoSize=true});range.Controls.Add(start);range.Controls.Add(new Label{Text=l("Длительность, сек","Duration, sec"),AutoSize=true});range.Controls.Add(duration);table.Controls.Add(range,0,3);
            table.Controls.Add(new Label{Text=l("Точный текст выбранного фрагмента записи:","Exact transcript of the selected recording range:"),AutoSize=true},0,4);var transcript=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical};table.Controls.Add(transcript,0,5);
            var consent=new CheckBox{Text=l("Это мой голос или у меня есть разрешение владельца голоса.","This is my voice or I have the speaker's permission."),Dock=DockStyle.Fill};table.Controls.Add(consent,0,6);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=l("Отмена","Cancel")};var import=new Button{Text=l("Импорт","Import")};buttons.Controls.Add(cancel);buttons.Controls.Add(import);table.Controls.Add(buttons,0,7);
            cancel.Click+=(s,e)=>{if(_operation!=null)_operation.Cancel();else Close();};FormClosing+=(s,e)=>{if(_operation!=null){_operation.Cancel();e.Cancel=true;}};
            import.Click+=async(s,e)=>{_operation=new CancellationTokenSource();import.Enabled=open.Enabled=false;try{Reference=await ClassicQwenReference.ImportAsync(path.Text,name.Text,transcript.Text,(double)start.Value,(double)duration.Value,consent.Checked,_operation.Token);}catch(OperationCanceledException){}catch(Exception error){MessageBox.Show(this,error.Message,Text);}finally{_operation.Dispose();_operation=null;import.Enabled=open.Enabled=true;if(Reference!=null){DialogResult=DialogResult.OK;Close();}}};Controls.Add(table);AcceptButton=import;
        }
    }
}
