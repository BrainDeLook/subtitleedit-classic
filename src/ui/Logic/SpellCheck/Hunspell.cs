using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nikse.SubtitleEdit.Logic.SpellCheck
{
    public abstract class Hunspell : IDisposable
    {
        public static Hunspell GetHunspell(string dictionary)
        {
            // Finnish uses Voikko (not available via hunspell)
            if (dictionary.EndsWith("fi_fi", StringComparison.OrdinalIgnoreCase))
            {
                return new RemovedWordHunspell(new VoikkoSpellCheck(Configuration.BaseDirectory, Configuration.DictionariesDirectory),dictionary);
            }

            if (Configuration.IsRunningOnLinux)
            {
                return new RemovedWordHunspell(new LinuxHunspell(dictionary + ".aff", dictionary + ".dic"),dictionary);
            }

            if (Configuration.IsRunningOnMac)
            {
                return new RemovedWordHunspell(new MacHunspell(dictionary + ".aff", dictionary + ".dic"),dictionary);
            }

            return new RemovedWordHunspell(new WindowsHunspell(dictionary + ".aff", dictionary + ".dic"),dictionary);
        }

        private sealed class RemovedWordHunspell : Hunspell
        {
            private readonly Hunspell _inner;private readonly string _dictionary;
            public RemovedWordHunspell(Hunspell inner,string dictionary){_inner=inner;_dictionary=dictionary;}
            public override bool Spell(string word)=>!ClassicRemovedDictionaryWords.Contains(_dictionary,word) && _inner.Spell(word);
            public override List<string> Suggest(string word)=>_inner.Suggest(word).Where(x=>!ClassicRemovedDictionaryWords.Contains(_dictionary,x)).ToList();
            public override void Dispose()=>_inner.Dispose();
        }

        public abstract bool Spell(string word);
        public abstract List<string> Suggest(string word);

        public virtual void Dispose()
        {
        }

        protected void AddIShouldBeLowercaseLSuggestion(List<string> suggestions, string word)
        {
            if (suggestions == null)
            {
                return;
            }

            // "I" can often be an ocr bug - should really be "l"
            if (word.Length > 1 && word.StartsWith('I') && !suggestions.Contains("l" + word.Substring(1)) && Spell("l" + word.Substring(1)))
            {
                suggestions.Add("l" + word.Substring(1));
            }
        }

    }
}
