using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicLlamaForm : ClassicServiceSettingsForm
    {
        public ClassicLlamaForm() : base(ClassicSpeechSettings.L("Локальная AI-модель — llama.cpp", "Local AI model — llama.cpp"))
        {
            ClientSize=new Size(780,590);MinimumSize=new Size(700,530);
            var s=ClassicLlamaSettings.Load();
            var variants=new ComboBox{Name="llamaVariant",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};variants.Items.AddRange(new object[]{"cpu","vulkan","cuda","cuda13"});variants.SelectedItem=s.Variant;
            AddControl(ClassicSpeechSettings.L("Вычисления", "Compute"),variants);
            var models=new ComboBox{Name="llamaModel",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DropDownWidth=650};
            foreach(var model in ClassicLlamaCatalog.ReviewModels.Concat(ClassicLlamaCatalog.TranslateModels).GroupBy(m=>m.FileName).Select(g=>g.First()))models.Items.Add(model);
            AddControl(ClassicSpeechSettings.L("Модель", "Model"),models);
            Add("file",ClassicSpeechSettings.L("Файл модели", "Model file"),s.Model);Fields["file"].ReadOnly=true;
            models.SelectedIndexChanged+=(o,e)=>{if(models.SelectedItem is LlamaCppModel model)Fields["file"].Text=model.FileName;};
            models.SelectedItem=models.Items.Cast<LlamaCppModel>().FirstOrDefault(m=>m.FileName==s.Model);
            Add("folder",ClassicSpeechSettings.L("Папка моделей (необязательно)","Model folder (optional)"),s.Folder);
            Add("context",ClassicSpeechSettings.L("Контекст, токенов", "Context tokens"),s.Context.ToString());
            Add("layers",ClassicSpeechSettings.L("Слоёв на GPU", "GPU layers"),s.GpuLayers.ToString());
            Add("status",ClassicSpeechSettings.L("Установка", "Installation"),ClassicLlamaServer.Installed(s)?ClassicSpeechSettings.L("Установлено", "Installed"):ClassicSpeechSettings.L("Скачай движок и модель. Размер модели указан в списке. Сервер запускается только на 127.0.0.1.","Download engine and model. Model size is shown in the list. The server binds only to 127.0.0.1."),multiline:true);Fields["status"].ReadOnly=true;
            Action read=()=>{s.Variant=(string)variants.SelectedItem;s.Model=Fields["file"].Text;s.Folder=Fields["folder"].Text.Trim();s.Context=int.Parse(Fields["context"].Text);s.GpuLayers=int.Parse(Fields["layers"].Text);s.Validate();};
            SaveWith(()=>{read();s.Save();});
            DownloadWith(async(cancel,progress)=>{read();await ClassicLlamaServer.EnsureInstalled(s,cancel,progress);Fields["status"].Text=ClassicSpeechSettings.L("Установлено. Сохрани настройки.","Installed. Save settings.");});
            DownloadWith(async(cancel,progress)=>{read();await ClassicLlamaServer.Start(s,cancel,progress);s.Save();Fields["status"].Text=ClassicSpeechSettings.L("Сервер запущен.","Server started.");},ClassicSpeechSettings.L("Запустить", "Start"));
            // A second row prevents action buttons from clipping at the minimum width.
            _footer.Height=82;_footer.WrapContents=true;
            var stop=new Button{Name="llamaStop",Text=ClassicSpeechSettings.L("Остановить", "Stop"),AutoSize=true};_footer.Controls.Add(stop);stop.Click+=(o,e)=>{ClassicLlamaServer.Stop();Fields["status"].Text=ClassicSpeechSettings.L("Остановлено", "Stopped");};
            var import=new Button{Text=ClassicSpeechSettings.L("Импорт GGUF…", "Import GGUF…"),AutoSize=true};AddControl("",import);
            import.Click+=(o,e)=>{using(var picker=new OpenFileDialog{Filter="GGUF model|*.gguf"})if(picker.ShowDialog(this)==DialogResult.OK){try{read();Directory.CreateDirectory(ClassicLlamaServer.Models(s));var dest=Path.Combine(ClassicLlamaServer.Models(s),Path.GetFileName(picker.FileName));if(!File.Exists(dest))File.Copy(picker.FileName,dest);Fields["file"].Text=Path.GetFileName(dest);models.SelectedIndex=-1;}catch(Exception error){MessageBox.Show(this,error.Message,Text);}}};
        }
        private void AddControl(string label,Control control){var row=_table.RowCount++;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));_table.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,7,3,8)},0,row);_table.Controls.Add(control,1,row);}
    }
}
