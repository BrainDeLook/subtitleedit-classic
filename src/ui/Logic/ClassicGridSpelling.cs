using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Interfaces;
using Nikse.SubtitleEdit.Core.SpellCheck;
using Nikse.SubtitleEdit.Logic.SpellCheck;

namespace Nikse.SubtitleEdit.Logic
{
    // The classic edit-box word rules, also used by the independent grid renderer.
    public sealed class ClassicGridSpelling : IDisposable, IDoSpell
    {
        private Hunspell _hunspell;
        private SpellCheckWordLists _spellCheckWordLists;
        private List<SpellCheckWord> _words, _wrongWords;
        private readonly List<string> _skipAllList = new List<string>();
        private readonly HashSet<string> _skipOnceList = new HashSet<string>();
        private readonly Dictionary<string,List<SpellCheckWord>> _cache = new Dictionary<string,List<SpellCheckWord>>();
        private string _text, _dictionary, _folder, _settingsKey;
        private int CurrentLineIndex => -1;
        public string CurrentLanguage { get; private set; }
        public bool Ready => _hunspell != null;
        public string Dictionary => _dictionary;
        private static readonly Regex Special = new Regex(@"(?i)(?:https?://|www\.)[^\s<>]+|[^\s<>@]+@[^\s<>@]+|(?<![\p{L}\p{N}])#[\p{L}\p{N}_]+",RegexOptions.Compiled);
        public void Load(string dictionary)
        {
            if(_dictionary==dictionary && Ready)return;
            var candidate=Hunspell.GetHunspell(dictionary);
            try
            {
                var name=System.IO.Path.GetFileName(dictionary);
                var folder=System.IO.Path.GetDirectoryName(dictionary)+System.IO.Path.DirectorySeparatorChar;
                var lists=new SpellCheckWordLists(folder,name,this);
                _hunspell?.Dispose();_hunspell=candidate;candidate=null;
                _dictionary=dictionary;_folder=folder;CurrentLanguage=name.Split('_','-')[0];_spellCheckWordLists=lists;
                _skipAllList.Clear();_cache.Clear();
            }
            finally{candidate?.Dispose();}
        }
        public void RefreshWords()
        {
            if(Ready)_spellCheckWordLists=new SpellCheckWordLists(_folder,System.IO.Path.GetFileName(_dictionary),this);
            _cache.Clear();
        }
        public List<SpellCheckWord> Check(string text)
        {
            if(!Ready || string.IsNullOrEmpty(text))return new List<SpellCheckWord>();
            var settings=Configuration.Settings.Tools;
            var key=settings.CheckOneLetterWords+":"+settings.SpellCheckEnglishAllowInQuoteAsIng;
            if(key!=_settingsKey){_settingsKey=key;_cache.Clear();}
            if(_cache.TryGetValue(text,out var cached))return cached;
            _text=text;_words=GetWords(text);SetWrongWords();
            var result=_wrongWords;_words=null;_wrongWords=null;_text=null;
            if(_cache.Count>=512)_cache.Clear();if(text.Length<=16384)_cache[text]=result;
            return result;
        }
        public List<string> Suggest(string word) => Ready && word.Length<=80?_hunspell.Suggest(word).Take(12).ToList():new List<string>();
        public void Ignore(string word){if(!_skipAllList.Contains(word))_skipAllList.Add(word);_cache.Clear();}
        public void AddWord(string word,bool name)
        {
            if(!Ready)return;
            try{if(name)_spellCheckWordLists.AddName(word);else _spellCheckWordLists.AddUserWord(word);}
            catch{RefreshWords();throw;}
            _cache.Clear();
        }
        public bool DoSpell(string word)=>_hunspell!=null && _hunspell.Spell(word);
        public void Dispose(){_hunspell?.Dispose();_hunspell=null;_dictionary=null;_cache.Clear();}
        private List<SpellCheckWord> GetWords(string s)
        {
            s = _spellCheckWordLists.ReplaceHtmlTagsWithBlanks(s);
            s = _spellCheckWordLists.ReplaceAssTagsWithBlanks(s);
            s = _spellCheckWordLists.ReplaceKnownWordsOrNamesWithBlanks(s);
            s = Special.Replace(s, m => new string(' ',m.Length));
            return SpellCheckWordLists.Split(s);
        }

        private void SetWrongWords()
        {
            _wrongWords = new List<SpellCheckWord>();

            for (var i = 0; i < _words.Count; i++)
            {
                var currentWord = _words[i];
                var currentWordText = _words[i].Text;
                var minLength = 2;
                if (Configuration.Settings.Tools.CheckOneLetterWords)
                {
                    minLength = 1;
                }

                var key = CurrentLineIndex + "-" + currentWord.Text + "-" + currentWord.Index;
                if (DoSpell(currentWordText) || Utilities.IsNumber(currentWordText) || _skipAllList.Contains(currentWordText)
                    || _skipOnceList.Contains(key) || _spellCheckWordLists.HasUserWord(currentWordText) || _spellCheckWordLists.HasName(currentWordText)
                    || currentWordText.Length < minLength || currentWordText == "&")
                {
                    continue;
                }

                string prefix = string.Empty;
                string postfix = string.Empty;
                if (currentWordText.RemoveControlCharacters().Trim().Length >= minLength)
                {
                    if (currentWordText.Length > 0)
                    {
                        const string trimChars = "'`*#\u200E\u200F\u202A\u202B\u202C\u202D\u202E\u200B\uFEFF";
                        var charHit = true;
                        while (charHit)
                        {
                            charHit = false;
                            foreach (var c in trimChars)
                            {
                                if (currentWordText.StartsWith(c))
                                {
                                    prefix += c;
                                    currentWordText = currentWordText.Substring(1);
                                    charHit = true;
                                }
                                if (currentWordText.EndsWith(c))
                                {
                                    postfix = c + postfix;
                                    currentWordText = currentWordText.Remove(currentWordText.Length - 1);
                                    charHit = true;
                                }
                            }
                        }
                    }
                }

                if (prefix == "'" && currentWordText.Length >= 1 && (DoSpell(prefix + currentWordText) || _spellCheckWordLists.HasUserWord(prefix + currentWordText)))
                {
                    continue;
                }

                if (currentWordText.Length > 1)
                {
                    if ("`'".Contains(currentWordText[currentWordText.Length - 1]) && DoSpell(currentWordText.TrimEnd('\'').TrimEnd('`')))
                    {
                        continue;
                    }

                    if (currentWordText.EndsWith("'s", StringComparison.Ordinal) && currentWordText.Length > 4
                                                                                 && DoSpell(currentWordText.TrimEnd('s').TrimEnd('\'')))
                    {
                        continue;
                    }

                    if (currentWordText.EndsWith('\'') && DoSpell(currentWordText.TrimEnd('\'')))
                    {
                        continue;
                    }

                    var removeUnicode = currentWordText.Replace("\u200b", string.Empty); // zero width space
                    removeUnicode = removeUnicode.Replace("\u2060", string.Empty); // word joiner
                    removeUnicode = removeUnicode.Replace("\ufeff", string.Empty); // zero width no-break space
                    if (DoSpell(removeUnicode))
                    {
                        continue;
                    }

                    if (i > 0 && i < _words.Count && _words.ElementAtOrDefault(i + 1) != null &&
                        string.Equals(_words[i - 1].Text, "www", StringComparison.InvariantCultureIgnoreCase) &&
                        (string.Equals(_words[i + 1].Text, "com", StringComparison.InvariantCultureIgnoreCase) ||
                         string.Equals(_words[i + 1].Text, "org", StringComparison.InvariantCultureIgnoreCase) ||
                         string.Equals(_words[i + 1].Text, "net", StringComparison.InvariantCultureIgnoreCase)) &&
                        _text.IndexOf(_words[i - 1].Text + "." + currentWordText + "." +
                                     _words[i + 1].Text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue; // do not spell check urls
                    }

                    if (CurrentLanguage == "ar")
                    {
                        var trimmed = currentWordText.Trim('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ',', '،');
                        if (trimmed != currentWordText)
                        {
                            if (_spellCheckWordLists.HasName(trimmed) || _skipAllList.Contains(trimmed.ToUpperInvariant())
                                                                      || _spellCheckWordLists.HasUserWord(trimmed) || DoSpell(trimmed))
                            {
                                continue;
                            }
                        }
                    }

                    // check if dash concatenated word with previous or next word is in spell check dictionary
                    if (i > 0 && (_text[currentWord.Index - 1] == '-' || _text[currentWord.Index - 1] == '‑'))
                    {
                        var wordWithDash = _words[i - 1].Text + "-" + currentWordText;
                        if (DoSpell(wordWithDash))
                        {
                            continue;
                        }

                        wordWithDash = _words[i - 1].Text + "‑" + currentWordText; // non break hyphen
                        if (DoSpell(wordWithDash) || _spellCheckWordLists.HasUserWord(wordWithDash)
                                                  || _spellCheckWordLists.HasUserWord(wordWithDash.Replace("‑", "-")) || _spellCheckWordLists.HasUserWord("-" + currentWordText))
                        {
                            continue;
                        }

                    }

                    if (i < _words.Count - 1 && _words[i + 1].Index - 1 < _text.Length &&
                        (_text[_words[i + 1].Index - 1] == '-' || _text[_words[i + 1].Index - 1] == '‑'))
                    {
                        var wordWithDash = currentWordText + "-" + _words[i + 1].Text;
                        if (DoSpell(wordWithDash))
                        {
                            continue;
                        }

                        wordWithDash = currentWordText + "‑" + _words[i + 1].Text; // non break hyphen
                        if (DoSpell(wordWithDash) || _spellCheckWordLists.HasUserWord(wordWithDash) || _spellCheckWordLists.HasUserWord(wordWithDash.Replace("‑", "-")))
                        {
                            continue;
                        }
                    }
                }
                else
                {
                    if (currentWordText == "'")
                    {
                        continue;
                    }

                    if (CurrentLanguage == "en" && (currentWordText.Equals("a", StringComparison.OrdinalIgnoreCase) || currentWordText == "I"))
                    {
                        continue;
                    }

                    if (CurrentLanguage == "da" && currentWordText.Equals("i", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                if (Configuration.Settings.Tools.SpellCheckEnglishAllowInQuoteAsIng && CurrentLanguage == "en"
                                                                                    && _words[i].Text.EndsWith("in'", StringComparison.OrdinalIgnoreCase) && DoSpell(currentWordText.TrimEnd('\'') + "g"))
                {
                    continue;
                }

                _wrongWords.Add(currentWord);
            }
        }

    }
}
