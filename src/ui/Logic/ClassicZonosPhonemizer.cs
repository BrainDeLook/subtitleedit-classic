using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic
{
    // Read-only extraction of the official eSpeak NG package. Never runs an MSI installation or custom actions.
    public static class ClassicZonosPhonemizer
    {
        public const string Version = "1.52.0";
        public const string PackageUrl = "https://github.com/espeak-ng/espeak-ng/releases/download/1.52.0/espeak-ng.msi";
        public const long PackageBytes = 12765862;
        public const string PackageHash = "7f673c709ea5dd579d3b5ebb98688cc575328a6ab7438d2bc405b88cedaeafb9";
        public static string Root => Path.Combine(ClassicLocalTtsSettings.Root("zonos"), "phonemizer", Version);
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        public static async Task EnsureAsync(CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            await Gate.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                if (File.Exists(Path.Combine(Root, "libespeak-ng.dll")) && Directory.Exists(Path.Combine(Root, "espeak-ng-data"))) { WriteLicense(); return; }
                var staging = Root + "." + Guid.NewGuid().ToString("N") + ".staging";
                var package = staging + ".msi";
                try
                {
                    await ClassicVoiceAssets.EnsureFileAsync(PackageUrl, package, PackageBytes, PackageHash, false, cancel, progress).ConfigureAwait(false);
                    await ExtractAsync(package, staging, cancel).ConfigureAwait(false);
                    var library = Directory.GetFiles(staging, "libespeak-ng.dll", SearchOption.AllDirectories).Single();
                    var folder = Path.GetDirectoryName(library);
                    if (!Directory.Exists(Path.Combine(folder, "espeak-ng-data"))) throw new InvalidDataException("The eSpeak package is missing language data");
                    cancel.ThrowIfCancellationRequested(); Directory.CreateDirectory(Path.GetDirectoryName(Root));
                    if (Directory.Exists(Root)) Directory.Delete(Root, true);
                    Directory.Move(folder, Root); WriteLicense();
                }
                finally { if (File.Exists(package)) File.Delete(package); if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            }
            finally { Gate.Release(); }
        }
        private static void WriteLicense()
        {
            using (var source = typeof(ClassicZonosPhonemizer).Assembly.GetManifestResourceStream("ClassicEspeakLicense.txt"))
            using (var file = File.Create(Path.Combine(Root, "COPYING"))) source.CopyTo(file);
            File.WriteAllText(Path.Combine(Root, "SOURCE.txt"), "eSpeak NG 1.52.0 - GNU GPL version 3 or later.\r\nSource: https://github.com/espeak-ng/espeak-ng/tree/1.52.0\r\nOfficial package: " + PackageUrl + "\r\n");
        }
        public static void Configure(ProcessStartInfo start)
        {
            // The loader searches PATH for libespeak-ng.dll. eSpeak expects the parent of espeak-ng-data.
            start.EnvironmentVariables["PATH"] = Root + Path.PathSeparator + start.EnvironmentVariables["PATH"];
            start.EnvironmentVariables["CRISPASR_ESPEAK_DATA_PATH"] = Root;
        }
        public static async Task ExtractAsync(string package, string destination, CancellationToken cancel)
        {
            Directory.CreateDirectory(destination);
            uint database = 0;
            try
            {
                Error(MsiOpenDatabase(package, IntPtr.Zero, out database));
                var unpacked = Path.Combine(destination, "cabinet"); Directory.CreateDirectory(unpacked);
                foreach (var media in Query(database, "SELECT `Cabinet` FROM `Media`", 1))
                {
                    cancel.ThrowIfCancellationRequested(); var name = media[0];
                    if (!name.StartsWith("#")) throw new InvalidDataException("Expected an embedded MSI cabinet");
                    var cabinet = ClassicSpeechDownload.SafeArchivePath(destination, name.Substring(1));
                    uint view = 0, record = 0;
                    try
                    {
                        Error(MsiDatabaseOpenView(database, "SELECT `Data` FROM `_Streams` WHERE `Name`='" + name.Substring(1).Replace("'", "''") + "'", out view)); Error(MsiViewExecute(view, 0)); Error(MsiViewFetch(view, out record));
                        using (var output = File.Create(cabinet)) { var buffer = new byte[65536]; while (true) { cancel.ThrowIfCancellationRequested(); uint count = (uint)buffer.Length; Error(MsiRecordReadStream(record, 1, buffer, ref count)); if (count == 0) break; output.Write(buffer, 0, (int)count); } }
                    }
                    finally { if (record != 0) MsiCloseHandle(record); if (view != 0) MsiCloseHandle(view); }
                    await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "expand.exe"), "-F:* " + ClassicSpeechRunner.Quote(cabinet) + " " + ClassicSpeechRunner.Quote(unpacked)), cancel, null).ConfigureAwait(false);
                }
                var dirs = Query(database, "SELECT `Directory`,`Directory_Parent`,`DefaultDir` FROM `Directory`", 3).ToDictionary(r => r[0]);
                var components = Query(database, "SELECT `Component`,`Directory_` FROM `Component`", 2).ToDictionary(r => r[0], r => r[1]);
                Func<string, int, string> relative = null;
                relative = (id, depth) => { if (depth > 100 || !dirs.TryGetValue(id, out var row)) throw new InvalidDataException("Invalid MSI directory tree"); if (row[1].Length == 0) return ""; var name = row[2].Split(':')[0].Split('|').Last(); return Path.Combine(relative(row[1], depth + 1), name == "." ? "" : name); };
                foreach (var file in Query(database, "SELECT `File`,`Component_`,`FileName` FROM `File`", 3))
                {
                    cancel.ThrowIfCancellationRequested();
                    var target = ClassicSpeechDownload.SafeArchivePath(destination, Path.Combine("files", relative(components[file[1]], 0), file[2].Split('|').Last()));
                    var source = ClassicSpeechDownload.SafeArchivePath(unpacked, file[0]);
                    if (!File.Exists(source)) throw new InvalidDataException("The MSI cabinet is missing a required file");
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target);
                }
            }
            finally { if (database != 0) MsiCloseHandle(database); }
        }
        private static List<string[]> Query(uint database, string sql, int columns)
        {
            uint view = 0, record = 0; var rows = new List<string[]>();
            try { Error(MsiDatabaseOpenView(database, sql, out view)); Error(MsiViewExecute(view, 0)); while (true) { var result = MsiViewFetch(view, out record); if (result == 259) break; Error(result); try { var row = new string[columns]; for (int i = 0; i < columns; i++) { uint length = 4096; var buffer = new StringBuilder((int)length); Error(MsiRecordGetString(record, (uint)i + 1, buffer, ref length)); row[i] = buffer.ToString(); } rows.Add(row); } finally { MsiCloseHandle(record); record = 0; } } }
            finally { if (record != 0) MsiCloseHandle(record); if (view != 0) MsiCloseHandle(view); }
            return rows;
        }
        private static void Error(uint result) { if (result != 0) throw new InvalidDataException("Unable to read eSpeak MSI package (Windows Installer " + result + ")"); }
        [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiOpenDatabaseW")] private static extern uint MsiOpenDatabase(string path, IntPtr persist, out uint database);
        [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiDatabaseOpenViewW")] private static extern uint MsiDatabaseOpenView(uint database, string query, out uint view);
        [DllImport("msi.dll")] private static extern uint MsiViewExecute(uint view, uint record);
        [DllImport("msi.dll")] private static extern uint MsiViewFetch(uint view, out uint record);
        [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiRecordGetStringW")] private static extern uint MsiRecordGetString(uint record, uint field, StringBuilder value, ref uint length);
        [DllImport("msi.dll")] private static extern uint MsiRecordReadStream(uint record, uint field, byte[] buffer, ref uint length);
        [DllImport("msi.dll")] private static extern uint MsiCloseHandle(uint handle);
    }
}
