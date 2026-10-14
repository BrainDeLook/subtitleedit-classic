using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Dictionaries;
using System;
using System.IO;
using System.Reflection;

namespace Nikse.SubtitleEdit.Logic.VideoOcr
{
    public sealed class ClassicVideoOcrRules : IDisposable
    {
        private readonly string _folder=Path.Combine(ClassicPaddleOcr.Root,"Rules",Guid.NewGuid().ToString("N"));
        public OcrFixReplaceList ReplaceList { get; }
        public ClassicVideoOcrRules(string language)
        {
            if(language!="eng" && language!="rus" && language!="ron" && language!="hrv") return;
            Directory.CreateDirectory(_folder);
            var name=language+"_OCRFixReplaceList.xml";var path=Path.Combine(_folder,name);
            using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClassicVideoOcrRules."+language+".xml"))
            using(var output=File.Create(path))
            {
                if(source==null) throw new InvalidDataException("Pinned OCR replacement rules are missing");
                source.CopyTo(output);
            }
            // The classic loader layers existing user additions/removals onto the
            // v5.2 base. Never overwrite the user's dictionary or replacement files.
            var userName=language+"_OCRFixReplaceList_User.xml";
            var legacyUser=Path.Combine(Configuration.DictionariesDirectory,userName);
            if(File.Exists(legacyUser)) File.Copy(legacyUser,Path.Combine(_folder,userName),true);
            ReplaceList=new OcrFixReplaceList(path);
        }
        public void Dispose(){if(Directory.Exists(_folder))Directory.Delete(_folder,true);}
        // Exact hardcoded apostrophe rule from v5.2 OcrFixEngine.
        public static string NormalizeApostrophes(string text)
        {
            if(string.IsNullOrEmpty(text)) return text;
            var opening=text.IndexOf('‘')>=0;var closing=text.IndexOf('’')>=0;
            if(opening==closing) return text;
            return text.Replace("'‘","'").Replace("‘'","'").Replace("'’","'").Replace("’'","'").Replace('‘','\'').Replace('’','\'');
        }
        public static string ThreeLetterLanguage(string language)
        {
            switch(language){case "japan":return "jpn";case "korean":return "kor";case "ch":case "chinese_cht":return "zho";case "french":return "fra";case "german":return "deu";}
            return Iso639Dash2LanguageCode.GetThreeLetterCodeFromTwoLetterCode(language) ?? "";
        }
    }
}
