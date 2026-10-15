using System;
using System.IO;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.Ocr
{
    public static class ClassicNOcrPreferences
    {
        private static string FileName=>Path.Combine(Configuration.DataDirectory,"ClassicNOcrAlgorithm.txt");
        public static NOcrLineAlgorithm LoadAlgorithm(){try{if(Enum.TryParse(File.ReadAllText(FileName),out NOcrLineAlgorithm value)&&Enum.IsDefined(typeof(NOcrLineAlgorithm),value))return value;}catch(IOException){}return NOcrLineAlgorithm.Random;}
        public static void SaveAlgorithm(NOcrLineAlgorithm value){Directory.CreateDirectory(Configuration.DataDirectory);File.WriteAllText(FileName,value.ToString());}
    }
}
