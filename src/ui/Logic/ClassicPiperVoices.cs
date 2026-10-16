using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.TextToSpeech;

namespace Nikse.SubtitleEdit.Logic
{
    public static class ClassicPiperVoices
    {
        public static string Folder => Path.Combine(Configuration.DataDirectory, "TextToSpeech", "Piper");
        public static void AddCustom(List<PiperModel> voices)
        {
            if (!Directory.Exists(Folder)) return;
            var known = new HashSet<string>(voices.Select(v => v.ModelShort), StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(Folder, "*.onnx").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                if (known.Contains(Path.GetFileName(file)) || !File.Exists(file + ".json")) continue;
                voices.Add(new PiperModel(Path.GetFileNameWithoutExtension(file), "Custom", "custom", Path.GetFileName(file), Path.GetFileName(file) + ".json"));
            }
        }
        public static string Import(string file)
        {
            file = Path.GetFullPath(file);
            if (!file.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) || !File.Exists(file) || new FileInfo(file).Length == 0) throw new InvalidDataException("Choose a non-empty .onnx model.");
            if (!File.Exists(file + ".json")) throw new FileNotFoundException("The matching .onnx.json config must be next to the model.");
            var config = JObject.Parse(File.ReadAllText(file + ".json"));
            if (((int?)config["audio"]?["sample_rate"] ?? 0) <= 0 || config["phoneme_id_map"] == null) throw new InvalidDataException("The file is not a Piper voice config.");
            Directory.CreateDirectory(Folder);
            var target = Path.Combine(Folder, Path.GetFileName(file));
            if (string.Equals(file, target, StringComparison.OrdinalIgnoreCase)) return target;
            // Keep installed/downloaded voices intact when a custom model reuses their name.
            if (File.Exists(target) || File.Exists(target + ".json"))
            {
                var stem = Path.GetFileNameWithoutExtension(file); int suffix = 2;
                do { target = Path.Combine(Folder, stem + "-custom-" + suffix++ + ".onnx"); } while (File.Exists(target) || File.Exists(target + ".json"));
            }
            var stagedModel = target + "." + Guid.NewGuid().ToString("N") + ".part";
            var stagedConfig = stagedModel + ".json";
            bool modelCopied = false;
            try { File.Copy(file, stagedModel); File.Copy(file + ".json", stagedConfig); File.Move(stagedModel, target); modelCopied = true; File.Move(stagedConfig, target + ".json"); }
            catch { if (modelCopied) File.Delete(target); throw; }
            finally { if (File.Exists(stagedModel)) File.Delete(stagedModel); if (File.Exists(stagedConfig)) File.Delete(stagedConfig); }
            return target;
        }
    }
}
