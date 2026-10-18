using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Settings;
using Profile = Nikse.SubtitleEdit.Core.Settings.BeautifyTimeCodesSettings.BeautifyTimeCodesProfile;

namespace Nikse.SubtitleEdit.Logic
{
    // A separate library preserves compatibility with the SE4 Settings.xml schema.
    public sealed class ClassicBeautifyProfiles
    {
        private readonly string _file;
        public ClassicBeautifyProfiles(string file) { _file = file; }
        public static Profile Clone(Profile profile) => JsonConvert.DeserializeObject<Profile>(JsonConvert.SerializeObject(profile));
        public Dictionary<string, Profile> Read()
        {
            var result = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_file)) return result;
            var root = JObject.Parse(File.ReadAllText(_file));
            if ((int?)root["version"] != 1 || !(root["profiles"] is JArray profiles)) throw new InvalidDataException("Unsupported timing-profile library.");
            foreach (var entry in profiles)
            {
                var name = ValidateName((string)entry["name"]);
                if (result.ContainsKey(name)) throw new InvalidDataException("Duplicate timing-profile name.");
                var profile = entry["values"]?.ToObject<Profile>();
                if (profile == null) throw new InvalidDataException("Missing timing-profile values.");
                Validate(profile); result.Add(name, profile);
            }
            return result;
        }
        public static string ValidateName(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 100 || name.Any(char.IsControl)) throw new ArgumentException("Enter a profile name of 1–100 characters.");
            return name;
        }
        private static void Validate(Profile profile)
        {
            foreach (var property in typeof(Profile).GetProperties())
            {
                var value = property.GetValue(profile);
                if (property.PropertyType == typeof(int) && ((int)value < 0 || (int)value > 100000)) throw new InvalidDataException("Invalid timing-profile value: " + property.Name);
                if (property.PropertyType.IsEnum && !Enum.IsDefined(property.PropertyType, value)) throw new InvalidDataException("Invalid timing-profile behavior.");
            }
        }
        public void Save(string name, Profile profile, bool overwrite = false)
        {
            name = ValidateName(name); Validate(profile);
            var profiles = Read();
            if (profiles.ContainsKey(name) && !overwrite) throw new IOException("A profile with this name already exists.");
            profiles.Remove(name); profiles.Add(name, Clone(profile)); Write(profiles);
        }
        public void Delete(string name)
        {
            var profiles = Read(); if (profiles.Remove(name)) Write(profiles);
        }
        private void Write(Dictionary<string, Profile> profiles)
        {
            var root = new JObject { ["version"] = 1, ["profiles"] = new JArray(profiles.OrderBy(p => p.Key, StringComparer.CurrentCultureIgnoreCase).Select(p => new JObject { ["name"] = p.Key, ["values"] = JObject.FromObject(p.Value) })) };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_file)));
            var temp = _file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, root.ToString());
                if (File.Exists(_file)) File.Replace(temp, _file, _file + ".bak"); else File.Move(temp, _file);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
