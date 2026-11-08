using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using Nikse.SubtitleEdit.Core.Common;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    public static class ClassicReleaseInstaller
    {
        public static bool CanInstallAndRestart => File.Exists(Path.Combine(Path.GetDirectoryName(typeof(ClassicReleaseInstaller).Assembly.Location), "unins000.exe"));
        public static async Task<string> Download(CheckForUpdatesHelper release, CancellationToken token, IProgress<long> progress, HttpMessageHandler handler = null)
        {
            if (release.InstallerUrl == null || release.ChecksumsUrl == null) throw new InvalidOperationException("This release has no verified Classic installer assets. Open its release page instead.");
            var name = Path.GetFileName(new Uri(release.InstallerUrl).AbsolutePath);
            var folder = Path.Combine(Path.GetTempPath(), "SubtitleEditClassicUpdate", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            var output = Path.Combine(folder, name); var complete = false;
            try
            {
                using (var http = new HttpClient(handler ?? CreateHandler(), handler == null) { Timeout = TimeSpan.FromMinutes(20) })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("SubtitleEdit-Classic-Updater");
                    string sums;
                    using (var response = await http.GetAsync(release.ChecksumsUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var buffer = new MemoryStream())
                        {
                            var block = new byte[8192]; int count;
                            while ((count = await input.ReadAsync(block, 0, block.Length, token).ConfigureAwait(false)) > 0)
                            {
                                if (buffer.Length + count > 1024 * 1024) throw new InvalidDataException("Release checksum file exceeds the expected size.");
                                buffer.Write(block, 0, count);
                            }
                            sums = Encoding.UTF8.GetString(buffer.ToArray());
                        }
                    }
                    var line = sums.TrimStart('\ufeff').Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(s => s.Length > 66 && s.Substring(64).Trim().TrimStart('*') == name);
                    if (line == null || line.Substring(0, 64).Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("The release checksum file has no SHA-256 for this installer.");
                    using (var response = await http.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            var buffer = new byte[81920]; long total = 0; int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                            {
                                total += count; if (total > 256L * 1024 * 1024) throw new InvalidDataException("The Classic installer exceeds the expected download limit.");
                                await file.WriteAsync(buffer, 0, count, token).ConfigureAwait(false); progress?.Report(total);
                            }
                        }
                    }
                    using (var sha = SHA256.Create()) using (var input = File.OpenRead(output))
                    {
                        var actual = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
                        if (!actual.Equals(line.Substring(0, 64), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Classic installer SHA-256 mismatch. The downloaded file will not be started.");
                    }
                }
                token.ThrowIfCancellationRequested(); complete = true; return output;
            }
            finally { if (!complete) { if (File.Exists(output)) File.Delete(output); Directory.Delete(folder); } }
        }

        private static HttpMessageHandler CreateHandler()
        {
            var handler = new HttpClientHandler();
            var settings = Configuration.Settings.Proxy;
            if (!string.IsNullOrWhiteSpace(settings.ProxyAddress))
            {
                var proxy = new WebProxy(settings.ProxyAddress);
                if (settings.UseDefaultCredentials) proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                else if (!string.IsNullOrWhiteSpace(settings.UserName)) proxy.Credentials = new NetworkCredential(settings.UserName, settings.Password, settings.Domain);
                handler.Proxy = proxy; handler.UseProxy = true;
            }
            return handler;
        }

        public static void InstallAndRestart(string installer, string application)
        {
            // Only the updater's fixed local paths are passed to PowerShell, as single-quoted literals.
            string P(string value) => "'" + value.Replace("'", "''") + "'";
            var script = Path.Combine(Path.GetDirectoryName(installer), "install-and-restart.ps1");
            File.WriteAllText(script,
                "$ErrorActionPreference='Stop'\r\n" +
                "$current=Get-Process -Id " + Process.GetCurrentProcess().Id + " -ErrorAction SilentlyContinue\r\nif($current){$current.WaitForExit()}\r\n" +
                "$setup=Start-Process -FilePath " + P(installer) + " -ArgumentList @('/SILENT','/NORESTART') -WindowStyle Hidden -Wait -PassThru\r\n" +
                "if($setup.ExitCode -eq 0){Start-Process -FilePath " + P(application) + "}\r\n", new UTF8Encoding(true));
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            Process.Start(new ProcessStartInfo(powershell, "-NoProfile -ExecutionPolicy Bypass -File " + SpeechToText.ClassicSpeechRunner.Quote(script)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        }
    }
}
