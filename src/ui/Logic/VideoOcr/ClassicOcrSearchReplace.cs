using System;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Logic.VideoOcr
{
    // v5.3 OCR Find/Replace adapted to the classic result grid; timings remain untouched.
    public static class ClassicOcrSearchReplace
    {
        public static Regex Pattern(string find,bool regex,bool caseSensitive,bool wholeWord)
        {
            if(string.IsNullOrEmpty(find))throw new ArgumentException("Enter text to find");
            var pattern=regex ? find : Regex.Escape(find);
            if(wholeWord)pattern=@"(?<!\p{L}|\p{N}|_)"+"(?:"+pattern+")"+@"(?!\p{L}|\p{N}|_)";
            return new Regex(pattern,RegexOptions.Multiline|(caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase),TimeSpan.FromSeconds(2));
        }
        public static string Replace(string input,Regex pattern,string replacement,bool regex,out int count)
        {
            var replacements=0;
            var result=pattern.Replace(input ?? "",match=>{replacements++;return regex ? match.Result(replacement ?? "") : replacement ?? "";});
            count=replacements;return result;
        }
    }
}
