using Nikse.SubtitleEdit.Logic;
using System;
using System.IO;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        private string _ttsExportPath;
        private string _ttsMuxedPath;
        private bool ChooseSpeechExport()
        {
            var video=checkBoxAddToVideoFile.Checked && _videoFileName!=null;
            var extension=video ? (Path.GetExtension(_videoFileName).Equals(".mp4",StringComparison.OrdinalIgnoreCase) ? ".mp4" : ".mkv") : ".wav";
            var settings=ClassicQwenTtsSettings.Load();
            var initial=Directory.Exists(settings.ExportFolder) ? settings.ExportFolder : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var basename=Path.GetFileNameWithoutExtension(_videoFileName ?? _subtitle.FileName);
            using(var save=new SaveFileDialog{Title=QwenRussian ? "Куда сохранить озвучку" : "Save generated speech",Filter=video ? "Video|*"+extension : "WAV audio|*.wav",DefaultExt=extension.TrimStart('.'),AddExtension=true,OverwritePrompt=true,InitialDirectory=initial,FileName=(string.IsNullOrWhiteSpace(basename) ? "Speech" : basename)+"_tts"+extension})
            {
                if(save.ShowDialog(this)!=DialogResult.OK)return false;
                if(!string.IsNullOrEmpty(_videoFileName) && string.Equals(Path.GetFullPath(save.FileName),Path.GetFullPath(_videoFileName),StringComparison.OrdinalIgnoreCase))
                {MessageBox.Show(this,QwenRussian ? "Выбери другое имя, чтобы сохранить исходное видео." : "Choose another name to preserve the source video.");return false;}
                _ttsExportPath=save.FileName;_ttsMuxedPath=null;settings.ExportFolder=Path.GetDirectoryName(save.FileName);settings.Save();return true;
            }
        }
        private void PublishSpeechExport(string source)
        {
            if(string.IsNullOrEmpty(source) || !File.Exists(source))throw new IOException("Generated output was not created");
            var temporary=_ttsExportPath+"."+Guid.NewGuid().ToString("N")+".part";
            try {File.Copy(source,temporary);if(File.Exists(_ttsExportPath))File.Replace(temporary,_ttsExportPath,null);else File.Move(temporary,_ttsExportPath);}
            finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
        private void CleanupSpeechJob()
        {
            if(string.IsNullOrEmpty(_waveFolder))return;
            var path=Path.GetFullPath(_waveFolder).TrimEnd(Path.DirectorySeparatorChar);
            var parent=Path.GetDirectoryName(path).TrimEnd(Path.DirectorySeparatorChar);
            if(!string.Equals(parent,Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(Path.GetFileName(path),out _))return;
            try{if(Directory.Exists(path))Directory.Delete(path,true);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
