using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.SpeechToText
{
    public enum SelectedSpeechState { Pending, Running, Complete, Failed }
    public sealed class SelectedSpeechRow
    {
        public Paragraph Source { get; }
        public string AudioFile { get; }
        public string Text { get; internal set; }
        public string Error { get; internal set; }
        public SelectedSpeechState State { get; internal set; }
        internal SelectedSpeechRow(Paragraph paragraph, string audioFile) { Source = paragraph; AudioFile = audioFile; }
    }
    public sealed class ClassicSelectedSpeech
    {
        public IReadOnlyList<SelectedSpeechRow> Rows { get; }
        public bool Running { get; private set; }
        public ClassicSelectedSpeech(IEnumerable<Tuple<Paragraph, string>> clips)
        {
            Rows = clips.Select(c => new SelectedSpeechRow(c.Item1, c.Item2)).ToList();
            if (Rows.Count == 0) throw new ArgumentException("No selected audio clips.");
        }
        public async Task RunAsync(Func<SelectedSpeechRow, CancellationToken, Task<string>> transcribe, bool failedOnly, CancellationToken cancel, Action<int> changed = null)
        {
            if (Running) throw new InvalidOperationException("Transcription is already running.");
            Running = true;
            try
            {
                for (int i = 0; i < Rows.Count; i++)
                {
                    var row = Rows[i];
                    if (failedOnly && row.State == SelectedSpeechState.Complete) continue;
                    cancel.ThrowIfCancellationRequested();
                    var previousText = row.Text; var previousState = row.State;
                    row.State = SelectedSpeechState.Running; row.Error = null; changed?.Invoke(i);
                    try
                    {
                        var text = await transcribe(row, cancel);
                        cancel.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("No speech detected. Check language, model and VAD.");
                        row.Text = text.Trim(); row.State = SelectedSpeechState.Complete;
                    }
                    catch (OperationCanceledException) { row.Text = previousText; row.State = previousState; changed?.Invoke(i); throw; }
                    catch (Exception ex) { row.Text = previousText; row.State = SelectedSpeechState.Failed; row.Error = ex.Message; }
                    changed?.Invoke(i);
                }
            }
            finally { Running = false; }
        }
        public int Apply()
        {
            if (Running) throw new InvalidOperationException("Wait for transcription to finish.");
            int count = 0;
            foreach (var row in Rows.Where(r => r.State == SelectedSpeechState.Complete)) { row.Source.Text = row.Text; count++; }
            return count;
        }
    }
}
