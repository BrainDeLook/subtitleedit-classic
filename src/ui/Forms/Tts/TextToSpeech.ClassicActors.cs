using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        private readonly HashSet<string> _checkedSpeechActors = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> _speechActorFilter;
        private bool _fillingSpeechActors;
        private System.Collections.Generic.List<ClassicDubbing.SpeechRange> _dubbedRanges;

        private void InitializeSpeechActors()
        {
            listViewActors.CheckBoxes = true;
            listViewActors.ItemChecked += (s, e) =>
            {
                if (_fillingSpeechActors) return;
                var actor = e.Item.Tag as ActorAndVoice;
                if (actor == null) return;
                if (e.Item.Checked) _checkedSpeechActors.Add(actor.Actor ?? string.Empty);
                else _checkedSpeechActors.Remove(actor.Actor ?? string.Empty);
                var row=_castSettings.Get(actor.Actor);if(row!=null)row.Selected=e.Item.Checked;
            };
        }

        private bool CaptureSpeechActors()
        {
            _speechActorFilter = null;
            _silentSpeechRows.Clear();
            if (!listViewActors.Visible || _actorAndVoices.Count == 0) return true;
            _speechActorFilter = new HashSet<string>(listViewActors.Items.Cast<ListViewItem>()
                .Where(i => i.Checked).Select(i => ((ActorAndVoice)i.Tag).Actor ?? string.Empty), StringComparer.Ordinal);
            if (_speechActorFilter.Count > 0) return true;
            MessageBox.Show(this, QwenRussian ? "Отметь галочками хотя бы одного актёра для озвучки." : "Check at least one actor to generate speech.");
            return false;
        }

        private Subtitle SpeakingSubtitle(Subtitle subtitle, bool filterActors)
        {
            if (_castSpeakerNames.Count == 0 && _silentSpeechRows.Count == 0 && (!filterActors || _speechActorFilter == null)) return subtitle;
            var speaking = new Subtitle(subtitle);
            foreach (var p in speaking.Paragraphs)
            {
                if (_silentSpeechRows.Contains(ClassicTtsPreparation.Key(p)) || (filterActors && !SpeechActorIncluded(p))) {p.Text = string.Empty;continue;}
                var lines=(p.Text ?? "").SplitToLines();for(int i=0;i<lines.Count;i++)if(Nikse.SubtitleEdit.Logic.ClassicCast.TextSpeakerDetector.TrySplit(lines[i],i+1<lines.Count,out var name,out var text) && _castSpeakerNames.Contains(name))lines[i]=text;
                p.Text=string.Join(Environment.NewLine,lines).Trim();
            }
            return speaking;
        }

        private bool SpeechActorIncluded(Paragraph p) => _speechActorFilter == null || _speechActorFilter.Contains(p.Actor ?? string.Empty);

        private void CaptureDubbedRanges(List<FileNameAndSpeedFactor> files)
        {
            if ((_removeOriginalSpeech == null || !_removeOriginalSpeech.Checked) && _speechActorFilter == null)
            { _dubbedRanges = null; return; }
            var included = new List<ClassicDubbing.SpeechRange>();
            var protectedRanges = new List<ClassicDubbing.SpeechRange>();
            for (var i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                var p = _subtitle.Paragraphs[i];
                var generated = i < files.Count && files[i] != null && System.IO.File.Exists(files[i].Filename)
                    && SpeechActorIncluded(p) && !_silentSpeechRows.Contains(ClassicTtsPreparation.Key(p));
                if (generated)
                {
                    // Cover the actual generated clip, including its retained quiet ending.
                    var seconds = Nikse.SubtitleEdit.Logic.ClassicCast.ClassicVoiceAssets.WaveSeconds(files[i].Filename);
                    included.Add(new ClassicDubbing.SpeechRange(p.StartTime.TotalSeconds,
                        Math.Max(p.EndTime.TotalSeconds, p.StartTime.TotalSeconds + seconds)));
                }
                else if (_speechActorFilter != null && !SpeechActorIncluded(p))
                    protectedRanges.Add(new ClassicDubbing.SpeechRange(p.StartTime.TotalSeconds, p.EndTime.TotalSeconds));
            }
            _dubbedRanges = ClassicDubbing.ExcludeProtectedRanges(included, protectedRanges);
        }
    }
}
