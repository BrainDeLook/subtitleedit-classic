using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        private CheckBox _removeOriginalSpeech;
        private Button _dubbingSettingsButton;
        private CancellationTokenSource _dubbingCancellation;
        private readonly int _dubbingAudioTrack;
        private void InitializeClassicDubbing()
        {
            var bottomControls=groupBoxSettings.Controls.Cast<Control>().Where(c=>(c.Anchor & AnchorStyles.Bottom)!=0).ToArray();
            ClientSize=new Size(ClientSize.Width,ClientSize.Height+32);
            MinimumSize=new Size(MinimumSize.Width,MinimumSize.Height+32);
            foreach(var control in bottomControls)control.Top-=32;
            var settings=ClassicDubbingSettings.Load();
            _removeOriginalSpeech=new CheckBox{Name="checkBoxRemoveOriginalSpeech",Text=QwenRussian ? "Удалить исходную речь" : "Remove original speech",AutoSize=true,Anchor=AnchorStyles.Bottom|AnchorStyles.Left,Location=new Point(checkBoxVoiceOver.Left,checkBoxVoiceOver.Bottom+8),Checked=settings.RemoveSpeech,Enabled=_videoFileName!=null && checkBoxAddToVideoFile.Checked};
            _dubbingSettingsButton=new Button{Name="buttonDubbingSettings",Text=QwenRussian ? "Настройки…" : "Settings…",Anchor=AnchorStyles.Bottom|AnchorStyles.Right,Size=new Size(105,_removeOriginalSpeech.Height+6),Location=new Point(groupBoxSettings.ClientSize.Width-117,_removeOriginalSpeech.Top-3)};
            _dubbingSettingsButton.Click+=(s,e)=>EditDubbingSettings();
            groupBoxSettings.Controls.Add(_removeOriginalSpeech);groupBoxSettings.Controls.Add(_dubbingSettingsButton);
            checkBoxAddToVideoFile.CheckedChanged+=(s,e)=>_removeOriginalSpeech.Enabled=_videoFileName!=null && checkBoxAddToVideoFile.Checked;
        }
        private void EditDubbingSettings()
        {
            var settings=ClassicDubbingSettings.Load();
            using(var dialog=new Form{Text=QwenRussian ? "Удаление исходной речи" : "Remove original speech",ClientSize=new Size(460,218),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,StartPosition=FormStartPosition.CenterParent})
            {
                var modeLabel=new Label{Text=QwenRussian ? "Модель" : "Model",AutoSize=true,Location=new Point(15,22)};
                var mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(130,18),Width=310};mode.Items.AddRange(new object[]{QwenRussian ? "HTDemucs — быстрее (81 МБ)" : "HTDemucs — faster (81 MB)",QwenRussian ? "RoFormer — качество (457 МБ)" : "RoFormer — quality (457 MB)"});mode.SelectedIndex=settings.Fast ? 0 : 1;
                var computeLabel=new Label{Text=QwenRussian ? "Вычисления" : "Compute",AutoSize=true,Location=new Point(15,61)};
                var compute=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(130,57),Width=310};compute.Items.AddRange(new object[]{"cpu","cpu-legacy","vulkan","cuda","cuda13"});compute.SelectedItem=settings.Variant;if(compute.SelectedIndex<0)compute.SelectedIndex=0;
                var note=new Label{Location=new Point(15,96),Size=new Size(425,70),Text=QwenRussian ? "Музыка и эффекты сохраняются и смешиваются с новой озвучкой. Модель загрузится при первом запуске. На CPU обработка полного видео может занять долгое время." : "Retain music/effects and mix generated speech. The model downloads on first use. Full-video processing on CPU can take considerable time."};
                var ok=new Button{Text="OK",DialogResult=DialogResult.OK,Location=new Point(275,178),Width=75};var cancel=new Button{Text=QwenRussian ? "Отмена" : "Cancel",DialogResult=DialogResult.Cancel,Location=new Point(365,178),Width=75};
                dialog.Controls.AddRange(new Control[]{modeLabel,mode,computeLabel,compute,note,ok,cancel});dialog.AcceptButton=ok;dialog.CancelButton=cancel;
                if(dialog.ShowDialog(this)==DialogResult.OK){settings.Fast=mode.SelectedIndex==0;settings.Variant=compute.Text;settings.RemoveSpeech=_removeOriginalSpeech.Checked;settings.Save();}
            }
        }
        private async Task<bool> AddDubbingBackgroundAsync(string speech,string output,string encoding)
        {
            if(_removeOriginalSpeech==null || !_removeOriginalSpeech.Checked)return false;
            var settings=ClassicDubbingSettings.Load();settings.RemoveSpeech=true;settings.Save();
            _dubbingCancellation=new CancellationTokenSource();_removeOriginalSpeech.Enabled=_dubbingSettingsButton.Enabled=false;
            try
            {
                var progress=new Progress<SpeechProgress>(p=>labelProgress.Text=p.Message.Length>140 ? p.Message.Substring(0,140) : p.Message);
                var background=await ClassicDubbing.BackgroundAsync(_videoFileName,System.IO.Path.Combine(_waveFolder,"separation"),settings,_dubbingCancellation.Token,progress,_dubbingAudioTrack);
                await ClassicDubbing.MuxAsync(_videoFileName,speech,background,output,encoding,_dubbingCancellation.Token,progress);
                _ttsMuxedPath=output;return true;
            }
            finally{_dubbingCancellation.Dispose();_dubbingCancellation=null;_removeOriginalSpeech.Enabled=_videoFileName!=null && checkBoxAddToVideoFile.Checked;_dubbingSettingsButton.Enabled=true;}
        }
    }
}
