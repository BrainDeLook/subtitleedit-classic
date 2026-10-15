using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        private ClassicTtsSession _restoredSpeechSession;
        public event Action<Paragraph, FileNameAndSpeedFactor> SpeechTakeGenerated;
        private readonly Dictionary<string, ClassicTtsSessionItem> _speechSessionRows = new Dictionary<string, ClassicTtsSessionItem>();
        private Button _importSpeechSession;
        private bool _speechSessionActive;
        private readonly List<ClassicQwenReference> _importedSessionReferences = new List<ClassicQwenReference>();
        private void SaveClassicCastSettings() { if (!_speechSessionActive) _castSettings.Save(); }
        private void CleanupSessionReferences() { foreach (var voice in _importedSessionReferences) ClassicVoiceLibrary.Delete(voice); _importedSessionReferences.Clear(); }
        public static string SessionLabel(string ru, string en) => CL(ru, en);
        public bool? SessionRowEnabled(Paragraph p) => _speechSessionRows.TryGetValue(p.Id, out var row) ? (bool?)row.Include : null;
        public void ForgetSessionRow(Paragraph p) { _speechSessionRows.Remove(p.Id); }
        private ClassicCastRow SpeechCastRow(Paragraph paragraph)
        {
            if (_speechSessionRows.TryGetValue(paragraph.Id, out var item))
                return new ClassicCastRow { Actor = paragraph.Actor, Engine = ResolveSessionEngine(item.EngineName)?.Name ?? item.EngineName, Voice = item.VoiceName, Model = item.Model, Instructions = item.Instruction, Language = item.Language, Selected = item.Include };
            return _castSettings.Get(paragraph.Actor);
        }
        private TextToSpeechEngine ResolveSessionEngine(string name) => _engines.FirstOrDefault(e => e.Name == name || e.Id.ToString() == name || StableSessionEngineName(e.Id) == name);
        private static string StableSessionEngineName(TextToSpeechEngineId id)
        {
            switch (id)
            {
                case TextToSpeechEngineId.Piper: return "Piper";
                case TextToSpeechEngineId.EdgeTts: return "EdgeTts";
                case TextToSpeechEngineId.AllTalk: return "AllTalk";
                case TextToSpeechEngineId.ElevenLabs: return "ElevenLabs";
                case TextToSpeechEngineId.AzureTextToSpeech: return "AzureSpeech";
                case TextToSpeechEngineId.MistralSpeech: return "MistralSpeech";
                case TextToSpeechEngineId.OpenAiCompatible: return "OpenAI-compatible";
                case TextToSpeechEngineId.Murf: return "Murf";
                case TextToSpeechEngineId.GoogleSpeech: return "GoogleSpeech";
                case TextToSpeechEngineId.KokoroLocal: return "Kokoro TTS";
                case TextToSpeechEngineId.SupertonicCrisp: return "Supertonic (CrispASR)";
                case TextToSpeechEngineId.KugelAudio: return "KugelAudio (audio.cpp)";
                case TextToSpeechEngineId.ZonosCrisp: return "Zonos TTS (CrispASR)";
                case TextToSpeechEngineId.OmniStandalone: return "OmniVoice TTS";
                case TextToSpeechEngineId.Qwen3Crisp: return "Qwen3 TTS (CrispASR)";
                case TextToSpeechEngineId.VibeCrisp: return "VibeVoice (CrispASR)";
                case TextToSpeechEngineId.IndexCrisp: return "IndexTTS (CrispASR)";
                case TextToSpeechEngineId.DotsCrisp: return "dots.tts (CrispASR)";
                case TextToSpeechEngineId.ConfuciusCrisp: return "Confucius4-TTS (CrispASR)";
                case TextToSpeechEngineId.CosyVoice3: return "CosyVoice3 (CrispASR)";
                case TextToSpeechEngineId.PocketCrisp: return "Pocket TTS (CrispASR)";
                case TextToSpeechEngineId.IndexTts25: return "IndexTTS 2.5 (audio.cpp)";
                case TextToSpeechEngineId.HiggsAudio: return "Higgs Audio v3 (audio.cpp)";
                case TextToSpeechEngineId.FishAudio: return "Fish Audio S2 Pro (audio.cpp)";
                case TextToSpeechEngineId.BreezeTts: return "Breeze TTS 2 (audio.cpp)";
                case TextToSpeechEngineId.FireRedTts3: return "FireRedTTS3 (audio.cpp)";
                case TextToSpeechEngineId.VoxCpm2: return "VoxCPM2 (CrispASR)";
                case TextToSpeechEngineId.MossCrisp: return "MOSS-TTS (CrispASR)";
                case TextToSpeechEngineId.OmniCrisp: return "OmniVoice TTS (CrispASR)";
                case TextToSpeechEngineId.Chatterbox: return "Chatterbox TTS (CrispASR)";
                case TextToSpeechEngineId.F5Crisp: return "F5-TTS (CrispASR)";
                default: return id.ToString();
            }
        }
        public ClassicTtsSessionItem CaptureSessionRow(Paragraph paragraph, FileNameAndSpeedFactor audio, bool include)
        {
            var engine = GetParagraphEngine(paragraph); var cast = SpeechCastRow(paragraph);
            var item = _speechSessionRows.TryGetValue(paragraph.Id, out var existing) ? CloneSessionRow(existing) : new ClassicTtsSessionItem { EngineName = StableSessionEngineName(engine.Id), VoiceName = cast?.Voice ?? GetParagraphAudio(paragraph), Model = cast?.Model ?? "", Instruction = cast?.Instructions ?? "", Language = cast?.Language ?? "" };
            if (existing == null)
            {
                var local = LocalId(engine.Id);
                if (local != null) { var settings = ClassicLocalTtsSettings.Load(local); item.VoiceName = string.IsNullOrEmpty(item.VoiceName) ? settings.Voice : ClassicLocalTts.ResolveVoice(item.VoiceName); item.Model = string.IsNullOrEmpty(item.Model) ? settings.Model : item.Model; item.Language = string.IsNullOrEmpty(item.Language) ? settings.Language : item.Language; item.Instruction = string.IsNullOrEmpty(item.Instruction) ? settings.Instructions : item.Instruction; }
                else if (IsLocalCrispTts(engine.Id)) { var settings = ClassicQwenTtsSettings.Load(engine.Id == TextToSpeechEngineId.SupertonicCrisp); item.Model = string.IsNullOrEmpty(item.Model) ? settings.Design ? "VoiceDesign" : settings.Clone ? "Clone" : "CustomVoice" : item.Model; item.Language = string.IsNullOrEmpty(item.Language) ? settings.Language : item.Language; item.Instruction = string.IsNullOrEmpty(item.Instruction) ? settings.Instructions : item.Instruction; item.VoiceName = string.IsNullOrEmpty(item.VoiceName) ? settings.Voice : ClassicLocalTts.ResolveVoice(item.VoiceName); }
                else if (FinalOnlineId(engine.Id) != null) { var id = FinalOnlineId(engine.Id); var settings = ClassicOnlineTtsSettings.Load(id); item.VoiceName = ClassicOnlineTts.ResolveVoice(id, item.VoiceName, settings.Voice); item.Model = string.IsNullOrEmpty(item.Model) ? settings.Model : item.Model; }
                else if (engine.Id == TextToSpeechEngineId.OpenAiCompatible) { var settings = ClassicCompatibleTtsSettings.Load().Selected(); item.Model = string.IsNullOrEmpty(item.Model) ? settings.Model : item.Model; item.Instruction = string.IsNullOrEmpty(item.Instruction) ? settings.Instructions : item.Instruction; item.VoiceName = string.IsNullOrEmpty(item.VoiceName) ? settings.Voice : item.VoiceName; }
            }
            item.Text = paragraph.Text; item.StartMs = (long)Math.Round(paragraph.StartTime.TotalMilliseconds); item.EndMs = (long)Math.Round(paragraph.EndTime.TotalMilliseconds);
            item.CueMetadata = new ClassicTtsCueMetadata { Forced = paragraph.Forced, IsComment = paragraph.IsComment, Region = paragraph.Region, MarginL = paragraph.MarginL, MarginR = paragraph.MarginR, MarginV = paragraph.MarginV, Effect = paragraph.Effect, Language = paragraph.Language, Style = paragraph.Style, NewSection = paragraph.NewSection, Bookmark = paragraph.Bookmark };
            item.Actor = paragraph.Actor ?? ""; item.Style = paragraph.Extra ?? ""; item.Layer = paragraph.Layer; item.AudioFileName = audio?.Filename ?? ""; item.SpeedFactor = audio?.Factor ?? 1; item.Include = include;
            var reference = ClassicQwenReference.List(false).FirstOrDefault(v => v.Id == item.VoiceName || v.Name == item.VoiceName);
            if (reference != null) { item.VoiceDisplayName = reference.Name; item.VoiceName = reference.Id; item.VoiceFileName = Path.Combine(ClassicQwenReference.Folder, reference.Id + ".wav"); item.VoiceTranscript = File.ReadAllText(Path.Combine(ClassicQwenReference.Folder, reference.Id + ".txt")); }
            return item;
        }
        public static ClassicTtsSessionItem CloneSessionRow(ClassicTtsSessionItem row) => Newtonsoft.Json.JsonConvert.DeserializeObject<ClassicTtsSessionItem>(Newtonsoft.Json.JsonConvert.SerializeObject(row));
        public ClassicTtsSession CreateSpeechSession(Subtitle subtitle, List<FileNameAndSpeedFactor> audio, IList<bool> includes)
        {
            var session = new ClassicTtsSession { VideoFileName = _videoFileName ?? _restoredSpeechSession?.VideoFileName ?? "", SubtitleHeader = _subtitle.Header, SubtitleFooter = _subtitle.Footer, SubtitleFileName = _subtitle.FileName };
            foreach (var p in _subtitle.Paragraphs) {
                var index = subtitle.Paragraphs.FindIndex(r => r.Id == p.Id);
                session.Items.Add(CaptureSessionRow(p, index < 0 ? null : audio[index], index >= 0 && includes[index]));
            }
            session.ActorVoiceMappings = _castSettings.Rows.Select(r => new ClassicTtsSessionCast { Actor = r.Actor, EngineName = ResolveSessionEngine(r.Engine)?.Id is TextToSpeechEngineId id ? StableSessionEngineName(id) : r.Engine, VoiceName = r.Voice, Model = r.Model, Instruction = r.Instructions, Language = r.Language }).ToList();
            return session;
        }
        public void RestoreSessionRow(Paragraph paragraph, ClassicTtsSessionItem item)
        {
            var copy = CloneSessionRow(item); _speechSessionRows[paragraph.Id] = copy;
            paragraph.Text = copy.Text; paragraph.StartTime.TotalMilliseconds = copy.StartMs; paragraph.EndTime.TotalMilliseconds = copy.EndMs;
        }
        private void RememberRegeneratedSessionRow(Paragraph paragraph, string voice, FileNameAndSpeedFactor audio)
        {
            var item = CaptureSessionRow(paragraph, audio, true);
            if (_perLineOriginal == null || !item.PerLineVoice) {
                if (item.VoiceName != voice) { item.VoiceFileName = ""; item.VoiceTranscript = ""; }
                item.VoiceName = voice;
            }
            _speechSessionRows[paragraph.Id] = item;
        }
        private void PreserveSessionReference(Paragraph paragraph, ClassicQwenReference voice)
        {
            if (_playbackCancellation != null || string.IsNullOrEmpty(_waveFolder)) return;
            var folder = Path.Combine(_waveFolder, "session-refs"); Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, voice.Id + ".wav");
            File.Copy(Path.Combine(ClassicQwenReference.Folder, voice.Id + ".wav"), target);
            var item = CaptureSessionRow(paragraph, null, true);
            item.VoiceDisplayName = voice.Name; item.VoiceName = voice.Id; item.VoiceFileName = target; item.VoiceTranscript = File.ReadAllText(Path.Combine(ClassicQwenReference.Folder, voice.Id + ".txt")); item.PerLineVoice = true;
            _speechSessionRows[paragraph.Id] = item;
        }
        private void UpdateSessionCastBindings(IList<ClassicCastRow> previous)
        {
            foreach (var p in _subtitle.Paragraphs) {
                if (!_speechSessionRows.TryGetValue(p.Id, out var item)) continue;
                var row = _castSettings.Get(p.Actor); if (row == null) continue;
                var old = previous.FirstOrDefault(r => r.Actor == row.Actor);
                if (old == null || old.Engine != row.Engine) { var engine = ResolveSessionEngine(row.Engine); if (engine != null) item.EngineName = StableSessionEngineName(engine.Id); }
                if ((old == null || old.Voice != row.Voice) && !string.IsNullOrEmpty(row.Voice)) { item.VoiceName = row.Voice; item.VoiceFileName = ""; item.VoiceTranscript = ""; item.PerLineVoice = false; }
                if (old == null || old.Model != row.Model) item.Model = row.Model;
                if (old == null || old.Language != row.Language) item.Language = row.Language;
                if (old == null || old.Instructions != row.Instructions) item.Instruction = row.Instructions;
            }
        }
        private async Task ChooseSpeechSessionAsync()
        {
            if (SpeechWorkIsActive) return;
            using (var dialog = new OpenFileDialog { Title = CL("Открыть сессию озвучки", "Open speech session"), Filter = "TTS session|*.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _importSpeechSession.Enabled = false;
                try
                {
                    var session = await Task.Run(() => ClassicTtsSession.Load(dialog.FileName));
                    if (IsDisposed) return;
                    if (session.Items.Any(r => !string.IsNullOrEmpty(r.VoiceFileName)) && MessageBox.Show(this, CL("Сессия содержит образцы голосов. У тебя есть разрешение использовать их для клонирования?", "This session contains voice references. Do you have permission to use them for cloning?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    await LoadSpeechSessionAsync(session);
                }
                catch (Exception error) { if (!IsDisposed) HandleError(error); }
                finally { if (!IsDisposed) _importSpeechSession.Enabled = true; }
            }
        }
        public async Task LoadSpeechSessionAsync(ClassicTtsSession session)
        {
            if (SpeechWorkIsActive) throw new InvalidOperationException("Stop speech work before importing a session");
            _compatibleCancellation = new CancellationTokenSource(); SetQwenBusy(true);
            var addedReferences = new List<ClassicQwenReference>(); var committed = false;
            try
            {
                session = ClassicTtsSession.Copy(session);
                var imported = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in session.Items.Where(r => !string.IsNullOrEmpty(r.VoiceFileName)))
                {
                    if (!imported.TryGetValue(item.VoiceFileName, out var id))
                    {
                        var transcript = item.VoiceTranscript;
                        var reference = await ClassicQwenReference.ImportAsync(item.VoiceFileName, string.IsNullOrEmpty(item.VoiceDisplayName) ? item.VoiceName : item.VoiceDisplayName, transcript ?? "", 0, Math.Min(30, ClassicVoiceAssets.WaveSeconds(item.VoiceFileName)), true, _compatibleCancellation.Token, false);
                        id = reference.Id; imported.Add(item.VoiceFileName, id); addedReferences.Add(reference);
                    }
                    item.VoiceName = id; item.VoiceFileName = Path.Combine(ClassicQwenReference.Folder, id + ".wav");
                }
                var video = File.Exists(session.VideoFileName) ? session.VideoFileName : null;
                var info = video == null ? null : await Task.Run(() => UiUtil.GetVideoInfo(video));
                _compatibleCancellation.Token.ThrowIfCancellationRequested();
                var videoChanged = !string.Equals(_videoFileName, video, StringComparison.OrdinalIgnoreCase);
                _subtitle.Paragraphs.Clear(); _speechSessionRows.Clear();
                _subtitle.Header = session.SubtitleHeader ?? ""; _subtitle.Footer = session.SubtitleFooter ?? ""; _subtitle.FileName = session.SubtitleFileName ?? "";
                foreach (var item in session.Items)
                {
                    var p = new Paragraph(item.Text ?? "", item.StartMs, item.EndMs) { Actor = item.Actor ?? "", Extra = item.Style ?? "", Layer = item.Layer };
                    var metadata = item.CueMetadata;
                    if (metadata != null) { p.Forced = metadata.Forced; p.IsComment = metadata.IsComment; p.Region = metadata.Region; p.MarginL = metadata.MarginL; p.MarginR = metadata.MarginR; p.MarginV = metadata.MarginV; p.Effect = metadata.Effect; p.Language = metadata.Language; p.Style = metadata.Style; p.NewSection = metadata.NewSection; p.Bookmark = metadata.Bookmark; }
                    _subtitle.Paragraphs.Add(p); _speechSessionRows[p.Id] = item;
                }
                _subtitle.Renumber(); _subtitleFormat = string.IsNullOrWhiteSpace(_subtitle.Header) ? (SubtitleFormat)new SubRip() : new AdvancedSubStationAlpha();
                _videoFileName = video; _videoInfo = info; _restoredSpeechSession = session; _perLineOriginal = null; _silentSpeechRows.Clear(); _castSpeakerNames.Clear(); _speechActorFilter = null;
                _speechSessionActive = true;
                _castSettings = new ClassicCastSettings();
                foreach (var actor in session.Items.Select(r => r.Actor ?? "").Distinct())
                {
                    var item = session.Items.First(r => (r.Actor ?? "") == actor); _castSettings.Rows.Add(new ClassicCastRow { Actor = actor, Engine = ResolveSessionEngine(item.EngineName)?.Name ?? item.EngineName, Voice = item.VoiceName, Model = item.Model, Instructions = item.Instruction, Language = item.Language, Selected = session.Items.Any(r => (r.Actor ?? "") == actor && r.Include) });
                }
                _checkedSpeechActors.Clear(); foreach (var row in _castSettings.Rows.Where(r => r.Selected)) _checkedSpeechActors.Add(row.Actor);
                var firstEngine = ResolveSessionEngine(session.Items[0].EngineName); if (firstEngine != null) nikseComboBoxEngine.SelectedIndex = firstEngine.Index;
                RefreshClassicActors(); checkBoxShowPreview.Checked = true; checkBoxAddToVideoFile.Checked = video != null;
                if (videoChanged) { PlaybackVideo = null; SeekPlaybackVideo = null; SourceVideoIsCurrent = null; if (_playSpeech != null) _playSpeech.Enabled = false; }
                CleanupSessionReferences(); _importedSessionReferences.AddRange(addedReferences);
                committed = true;
                labelProgress.Text = CL("Сессия загружена: ", "Session loaded: ") + session.Items.Count; buttonGenerateTTS.Text = CL("Проверить и собрать озвучку", "Review and assemble speech");
            }
            finally { if (!committed) foreach (var reference in addedReferences) ClassicVoiceLibrary.Delete(reference); _compatibleCancellation.Dispose(); _compatibleCancellation = null; SetQwenBusy(false); }
        }
        private List<FileNameAndSpeedFactor> StageSpeechSession()
        {
            var files = new List<FileNameAndSpeedFactor>();
            foreach (var row in _restoredSpeechSession.Items)
            {
                if (string.IsNullOrEmpty(row.AudioFileName)) { files.Add(null); continue; }
                var target = Path.Combine(_waveFolder, Guid.NewGuid().ToString("N") + Path.GetExtension(row.AudioFileName));
                if (_abort) throw new OperationCanceledException();
                File.Copy(row.AudioFileName, target); files.Add(new FileNameAndSpeedFactor { Filename = target, Factor = row.SpeedFactor });
            }
            return files;
        }
    }
}
