using System;
using System.IO;
using System.Xml.Serialization;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicBurnInOptions
    {
        public string Container { get; set; } = ".mp4";
        public bool UseTextStyle { get; set; }
        public int Alignment { get; set; } = 2;
        public int MarginHorizontal { get; set; } = 10;
        public int MarginVertical { get; set; } = 10;
        public decimal ShadowWidth { get; set; } = 2;
        public int ShadowColorArgb { get; set; } = unchecked((int)0xff000000);
        public decimal Spacing { get; set; }
        public int BoxType { get; set; }
        public string Effects { get; set; } = string.Empty;
        public string LogoFileName { get; set; } = string.Empty;
        public int LogoX { get; set; }
        public int LogoY { get; set; }
        public int LogoOpacity { get; set; } = 100;
        public int LogoSize { get; set; } = 100;
        public bool MatchSourceFileSize { get; set; }
        public int StereoMode { get; set; }
        public int StereoDepth { get; set; }
        [XmlIgnore] public string ImageSubtitleFileName { get; set; } = string.Empty;
        [XmlIgnore] public bool InputIsAudioOnly { get; set; }

        public ClassicBurnInOptions Clone() => (ClassicBurnInOptions)MemberwiseClone();

        public static ClassicBurnInOptions Load(string fileName)
        {
            if (!File.Exists(fileName)) return new ClassicBurnInOptions();
            try
            {
                using (var reader = System.Xml.XmlReader.Create(fileName, new System.Xml.XmlReaderSettings { XmlResolver = null }))
                    return (ClassicBurnInOptions)new XmlSerializer(typeof(ClassicBurnInOptions)).Deserialize(reader);
            }
            catch (InvalidOperationException) { return new ClassicBurnInOptions(); }
            catch (IOException) { return new ClassicBurnInOptions(); }
        }

        public void Save(string fileName)
        {
            // Replace atomically so an interrupted write cannot destroy the previous settings.
            var temporary = fileName + ".tmp";
            using (var writer = new StreamWriter(temporary))
                new XmlSerializer(typeof(ClassicBurnInOptions)).Serialize(writer, this);
            if (File.Exists(fileName)) File.Replace(temporary, fileName, null);
            else File.Move(temporary, fileName);
        }
    }
}
