using System;
using System.IO;

namespace Nikse.SubtitleEdit.Logic
{
    // Stable 5.3 checks on window activation instead of running a live file watcher.
    public sealed class ClassicSubtitleFileStamp
    {
        public string FileName {get;private set;}
        public DateTime LastWriteUtc {get;private set;}
        public long Length {get;private set;}
        public static ClassicSubtitleFileStamp Read(string path)
        {
            try
            {
                var file=new FileInfo(path);
                return file.Exists?new ClassicSubtitleFileStamp{FileName=file.FullName,LastWriteUtc=file.LastWriteTimeUtc,Length=file.Length}:null;
            }
            catch(Exception ex)when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException){return null;}
        }
        public bool SameFile(string path)
        {
            try{return string.Equals(FileName,Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase);}
            catch(Exception ex)when(ex is ArgumentException || ex is NotSupportedException){return false;}
        }
        public bool SameContent(ClassicSubtitleFileStamp other)=>other!=null && SameFile(other.FileName) && LastWriteUtc==other.LastWriteUtc && Length==other.Length;
    }
}
