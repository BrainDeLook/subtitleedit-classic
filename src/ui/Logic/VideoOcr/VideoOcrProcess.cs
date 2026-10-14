using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic.VideoOcr
{
    internal static class VideoOcrProcess
    {
        private sealed class InlineProgress : IProgress<SpeechProgress>
        {
            private readonly Action<string> _line;
            public InlineProgress(Action<string> line) { _line=line; }
            public void Report(SpeechProgress progress) { _line?.Invoke(progress.Message ?? ""); }
        }
        public static string Quote(string text) => ClassicSpeechRunner.Quote(text);
        public static Task RunFfmpegAsync(string arguments,string workFolder,CancellationToken cancel,Action<string> line=null)
        {
            var exe=Configuration.Settings.General.FFmpegLocation;
            if(!File.Exists(exe)) throw new FileNotFoundException("Configure FFmpeg before running Video OCR.",exe);
            return RunAsync(exe,arguments,workFolder,cancel,line);
        }
        public static Task RunAsync(string exe,string arguments,string workFolder,CancellationToken cancel,Action<string> line=null)
        {
            var start=new ProcessStartInfo(exe,arguments){WorkingDirectory=workFolder};
            start.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";
            start.EnvironmentVariables["PYTHONUTF8"]="1";
            start.EnvironmentVariables["PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK"]="True";
            start.EnvironmentVariables["DS2_CROP_MODE"]="0";
            start.EnvironmentVariables["CRISPEMBED_CONV2D_MK"]="1";
            return ClassicSpeechRunner.RunProcessAsync(start,cancel,new InlineProgress(line));
        }
    }
}
