using Newtonsoft.Json;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    // Uses the stable v5.3 TtsImportExport names; Classic adds cue/format metadata without credentials.
    public sealed class ClassicTtsCueMetadata
    {
        public bool Forced { get; set; }
        public bool IsComment { get; set; }
        public string Region { get; set; }
        public string MarginL { get; set; }
        public string MarginR { get; set; }
        public string MarginV { get; set; }
        public string Effect { get; set; }
        public string Language { get; set; }
        public string Style { get; set; }
        public bool NewSection { get; set; }
        public string Bookmark { get; set; }
    }
    public sealed class ClassicTtsSessionItem
    {
        public string Text { get; set; } = "";
        public long StartMs { get; set; }
        public long EndMs { get; set; }
        public string AudioFileName { get; set; } = "";
        public decimal SpeedFactor { get; set; } = 1;
        public string EngineName { get; set; } = "";
        public string VoiceName { get; set; } = "";
        public string VoiceDisplayName { get; set; } = "";
        public string Model { get; set; } = "";
        public string Instruction { get; set; } = "";
        public string Language { get; set; } = "";
        public string VoiceFileName { get; set; } = "";
        public string VoiceTranscript { get; set; } = "";
        public string Actor { get; set; } = "";
        public string Style { get; set; } = "";
        public int Layer { get; set; }
        public bool Include { get; set; } = true;
        public bool PerLineVoice { get; set; }
        public ClassicTtsCueMetadata CueMetadata { get; set; }
    }
    public sealed class ClassicTtsSessionCast
    {
        public string Actor { get; set; } = "";
        public string EngineName { get; set; } = "";
        public string VoiceName { get; set; } = "";
        public string Model { get; set; } = "";
        public string Instruction { get; set; } = "";
        public string Language { get; set; } = "";
    }
    public sealed class ClassicTtsSession
    {
        public string VideoFileName { get; set; } = "";
        public List<ClassicTtsSessionItem> Items { get; set; } = new List<ClassicTtsSessionItem>();
        public List<ClassicTtsSessionCast> ActorVoiceMappings { get; set; } = new List<ClassicTtsSessionCast>();
        public string SubtitleHeader { get; set; } = "";
        public string SubtitleFooter { get; set; } = "";
        public string SubtitleFileName { get; set; } = "";
        [JsonIgnore] public string SourceFile { get; private set; }
        public static ClassicTtsSession Copy(ClassicTtsSession value) => JsonConvert.DeserializeObject<ClassicTtsSession>(JsonConvert.SerializeObject(value));
        public static ClassicTtsSession Load(string jsonFile)
        {
            if (new FileInfo(jsonFile).Length > 64 * 1024 * 1024) throw new InvalidDataException("TTS session metadata is too large");
            var session = JsonConvert.DeserializeObject<ClassicTtsSession>(File.ReadAllText(jsonFile)) ?? throw new InvalidDataException("Empty TTS session");
            if (session.Items == null || session.Items.Count == 0 || session.Items.Count > 200000) throw new InvalidDataException("The TTS session contains no usable rows");
            session.ActorVoiceMappings = session.ActorVoiceMappings ?? new List<ClassicTtsSessionCast>();
            var root = Path.GetDirectoryName(Path.GetFullPath(jsonFile));
            foreach (var row in session.Items)
            {
                if (row == null || row.StartMs < 0 || row.EndMs <= row.StartMs || row.EndMs > int.MaxValue || row.SpeedFactor <= 0 || row.SpeedFactor > 100) throw new InvalidDataException("Invalid TTS row timing or speed");
                if (!string.IsNullOrWhiteSpace(row.AudioFileName)) row.AudioFileName = ResolveAsset(root, row.AudioFileName);
                else if (row.Include) throw new InvalidDataException("An enabled TTS row has no audio file");
                if (!string.IsNullOrWhiteSpace(row.VoiceFileName)) {
                    var referencePath = row.VoiceFileName;
                    row.VoiceFileName = ResolveAsset(root, referencePath);
                    var sidecar = Path.ChangeExtension(referencePath, ".txt");
                    if (string.IsNullOrEmpty(row.VoiceTranscript) && File.Exists(Path.Combine(root, sidecar))) {
                        var transcript = ResolveAsset(root, sidecar);
                        if (new FileInfo(transcript).Length > 1024 * 1024) throw new InvalidDataException("Reference transcript is too large");
                        row.VoiceTranscript = File.ReadAllText(transcript);
                    }
                }
            }
            session.SourceFile = Path.GetFullPath(jsonFile); return session;
        }
        public static string ResolveAsset(string root, string relative)
        {
            if (Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0) throw new InvalidDataException("Session assets must use relative paths");
            var path = ClassicSpeechDownload.SafeArchivePath(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("A TTS session asset is missing", relative);
            var current = path;
            while (!string.Equals(current.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("TTS session assets cannot use directory links"); current = Path.GetDirectoryName(current); }
            return path;
        }
        public static Task ExportAsync(ClassicTtsSession source, string jsonFile, CancellationToken cancel) => Task.Run(() =>
        {
            var session = Copy(source); var root = Path.GetDirectoryName(Path.GetFullPath(jsonFile)); Directory.CreateDirectory(root);
            var token = Guid.NewGuid().ToString("N");
            var waveFolder = Path.Combine(root, "wav", token); var referenceFolder = Path.Combine(root, "refs", token); var partial = jsonFile + "." + token + ".part";
            var committed = false; var references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                for (int i = 0; i < session.Items.Count; i++)
                {
                    cancel.ThrowIfCancellationRequested(); var row = session.Items[i];
                    if (!string.IsNullOrWhiteSpace(row.AudioFileName))
                    {
                        var extension = Path.GetExtension(row.AudioFileName); if (!new[] { ".wav", ".mp3", ".flac", ".ogg", ".m4a" }.Contains(extension.ToLowerInvariant())) throw new InvalidDataException("Unsupported session audio file");
                        var relative = "wav/" + token + "/" + (i + 1).ToString("D6") + extension;
                        CopyAsset(row.AudioFileName, Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), cancel); row.AudioFileName = relative;
                    }
                    if (!string.IsNullOrWhiteSpace(row.VoiceFileName))
                    {
                        var original = Path.GetFullPath(row.VoiceFileName);
                        if (!references.TryGetValue(original, out var relative))
                        {
                            relative = "refs/" + token + "/" + references.Count.ToString("D4") + ".wav";
                            var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                            CopyAsset(original, target, cancel);
                            File.WriteAllText(Path.ChangeExtension(target, ".txt"), row.VoiceTranscript ?? "", new UTF8Encoding(false));
                            references[original] = relative;
                        }
                        row.VoiceFileName = relative;
                    }
                }
                cancel.ThrowIfCancellationRequested(); File.WriteAllText(partial, JsonConvert.SerializeObject(session, Formatting.Indented), new UTF8Encoding(false));
                cancel.ThrowIfCancellationRequested(); if (File.Exists(jsonFile)) File.Replace(partial, jsonFile, null); else File.Move(partial, jsonFile); committed = true;
            }
            finally
            {
                if (File.Exists(partial)) File.Delete(partial);
                // Only this export's unique child directories can be removed; existing sessions/takes are untouched.
                if (!committed) foreach (var directory in new[] { waveFolder, referenceFolder }) if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }, cancel);
        private static void CopyAsset(string source, string target, CancellationToken cancel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using (var input = File.OpenRead(source)) using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
            { var buffer = new byte[1024 * 1024]; int count; while ((count = input.Read(buffer, 0, buffer.Length)) > 0) { cancel.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); } }
        }
    }
}
