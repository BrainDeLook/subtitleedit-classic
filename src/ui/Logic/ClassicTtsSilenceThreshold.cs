using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic
{
    // Stable v5.2.0 TtsSilenceThreshold rules; legacy-compatible process adapter.
    public static class ClassicTtsSilenceThreshold
    {
        public static double ThresholdDbfs(double? peak)
        {
            if(!peak.HasValue || double.IsNaN(peak.Value) || double.IsInfinity(peak.Value))return -40;
            return Math.Max(-70,Math.Min(0,peak.Value)-40);
        }
        public static double Amplitude(double? peak)=>Math.Pow(10,ThresholdDbfs(peak)/20);
        public static double? ParsePeak(string log)
        {
            var match=Regex.Match(log,@"max_volume:\s*(-?\d+(?:\.\d+)?)\s*dB",RegexOptions.IgnoreCase);
            return match.Success && double.TryParse(match.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var value) ? (double?)value : null;
        }
        public static async Task<double?> MeasureAsync(string file,CancellationToken cancel)
        {
            using(var process=new Process{StartInfo=new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -hide_banner -i "+ClassicSpeechRunner.Quote(file)+" -vn -af volumedetect -f null -"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true}})
            {
                try
                {
                    process.Start();
                    using(cancel.Register(()=>{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}}))
                    {
                        var stderr=process.StandardError.ReadToEndAsync();var stdout=process.StandardOutput.ReadToEndAsync();
                        await Task.Run(()=>process.WaitForExit()).ConfigureAwait(false);
                        var log=await stderr.ConfigureAwait(false);await stdout.ConfigureAwait(false);cancel.ThrowIfCancellationRequested();
                        return process.ExitCode==0 ? ParsePeak(log) : null;
                    }
                }
                catch(OperationCanceledException){throw;}catch(Exception){return null;}
            }
        }
    }
}
