using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Dictionaries;
namespace Nikse.SubtitleEdit.Logic.SpellCheck
{
 public static class ClassicRemovedDictionaryWords
 {
    static ClassicRemovedDictionaryWords()
    {
        Utilities.UserDictionarySaved+=file=>{var name=Path.GetFileNameWithoutExtension(file);if(name.EndsWith("_user",StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-5);Refresh(Path.Combine(Path.GetDirectoryName(file),name));};
    }
    private static readonly Dictionary<string,HashSet<string>> Cache=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    private static string FileName(string language)
    {
        var name=Path.GetFileNameWithoutExtension(language).Replace('-','_');
        return Path.Combine(Path.IsPathRooted(language)?Path.GetDirectoryName(language):Utilities.DictionaryFolder,name+"_user.xml");
    }
    public static bool Contains(string language,string word)
    {
        var file=FileName(language);lock(Cache){if(!Cache.TryGetValue(file,out var words)){words=new HashSet<string>(StringComparer.Ordinal);try{Read(file,words);}catch(Exception ex)when(ex is IOException || ex is XmlException || ex is UnauthorizedAccessException){SeLogger.Error(ex,"Read removed dictionary words");}Cache.Add(file,words);}return words.Contains(word);}
    }
    public static HashSet<string> Get(string language)
    {
        var file=FileName(language);lock(Cache){if(!Cache.TryGetValue(file,out var words)){words=new HashSet<string>(StringComparer.Ordinal);try{Read(file,words);}catch(Exception ex)when(ex is IOException || ex is XmlException || ex is UnauthorizedAccessException){SeLogger.Error(ex,"Read removed dictionary words");}Cache.Add(file,words);}return new HashSet<string>(words,StringComparer.Ordinal);}
    }
    private static void Read(string file,HashSet<string> result)
    {
        var user=new XmlDocument{XmlResolver=null};if(File.Exists(file))user.Load(file);else user.LoadXml("<words/>");
        var accepted=new HashSet<string>(UserDictionaryXml.ReadWords(user));
        var shipped=file.Substring(0,file.Length-9)+"_se.xml";
        result.Clear();
        if(File.Exists(shipped)){var doc=new XmlDocument{XmlResolver=null};doc.Load(shipped);result.UnionWith(UserDictionaryXml.ReadRemovedWords(doc).Where(w=>!accepted.Contains(w)));}
        result.UnionWith(UserDictionaryXml.ReadRemovedWords(user));
    }
    public static List<string> UserRemoved(string language)
    {
        var file=FileName(language);if(!File.Exists(file))return new List<string>();var doc=new XmlDocument{XmlResolver=null};doc.Load(file);return UserDictionaryXml.ReadRemovedWords(doc);
    }
    public static void Refresh(string language)
    {var file=FileName(language);lock(Cache){if(Cache.TryGetValue(file,out var words))Read(file,words);}}
    public static void Save(string language,IEnumerable<string> words)
    {
        var file=FileName(language);var doc=new XmlDocument{XmlResolver=null};if(File.Exists(file))doc.Load(file);else doc.LoadXml("<words/>");
        UserDictionaryXml.WriteRemovedWords(doc,words.Select(x=>x.Trim()).Where(x=>x.Length>0).Distinct(StringComparer.Ordinal).ToList());
        Directory.CreateDirectory(Path.GetDirectoryName(file));var pending=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{doc.Save(pending);if(File.Exists(file))File.Replace(pending,file,null);else File.Move(pending,file);}finally{if(File.Exists(pending))File.Delete(pending);}
        Refresh(language);
    }
 }
}
