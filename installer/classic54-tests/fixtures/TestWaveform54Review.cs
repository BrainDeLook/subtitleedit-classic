using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;

class TestWaveform54Review
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr process, uint code);
    static object Get(object target, string field) => target.GetType().GetField(field, B).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, B).SetValue(target, value);
    static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethods(B).First(m => m.Name == name && m.GetParameters().Length == arguments.Length).Invoke(target, arguments);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    static IEnumerable<Control> All(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var c in All(child)) yield return c; } }
    [STAThread] static int Main(string[] args)
    {
        var result = 1;
        try
        {
            var folder = Path.GetFullPath(args[0]); Directory.CreateDirectory(folder);
            foreach (var field in new[] { "DataDirectory", "SettingsFileName", "WaveformsDirectory", "AutoBackupDirectory", "DictionariesDirectory" })
                typeof(Configuration).GetField(field).SetValue(null, field == "SettingsFileName" ? Path.Combine(folder, "Settings.xml") : folder + Path.DirectorySeparatorChar);
            Configuration.Settings.General.CheckForUpdates = false; Configuration.Settings.General.AutoSave = false;
            Configuration.Settings.General.Language = "ru-RU";
            Configuration.Settings.General.AllowEditOfOriginalSubtitle = false;
            Configuration.Settings.General.ShowOriginalAsPreviewIfAvailable = false;
            Configuration.Settings.VideoControls.WaveformSingleClickSelect = true;
            using (var main = new Main())
            {
                main.StartPosition = FormStartPosition.Manual; main.Location = new Point(-30000, -30000); main.ShowInTaskbar = false;
                main.Show(); Application.DoEvents();
                var sub = new Subtitle(); sub.Paragraphs.Add(new Paragraph("First copy", 1000, 2000)); sub.Paragraphs.Add(new Paragraph("Selected duplicate", 1000, 2000)); sub.Renumber();
                var original = new Subtitle(); original.Paragraphs.Add(new Paragraph("Original first", 1000, 2000)); original.Paragraphs.Add(new Paragraph("Original duplicate", 1000, 2000)); original.Renumber();
                Set(main, "_subtitle", sub); Set(main, "_subtitleOriginal", original); Set(main, "_isOriginalActive", true); Set(main, "_currentSubtitleFormat", new AdvancedSubStationAlpha());
                var grid = (SubtitleListView)Get(main, "SubtitleListview1");
                grid.ShowOriginalTextColumn("Original"); Call(grid, "Fill", sub, original);
                Call(main, "SelectListViewIndexAndEnsureVisible", 1); Application.DoEvents();
                var wave = (AudioVisualizer)Get(main, "audioVisualizer");
                wave.WavePeaks = new WavePeakData(100, Enumerable.Range(0, 1000).Select(_ => new WavePeak(100, -100)).ToList());
                Call(main, "SetWaveformPosition", 0d, 1.5d, 1);
                Check(ReferenceEquals(wave.SelectedParagraph, sub.Paragraphs[1]), "Waveform keeps the selected duplicate object");
                var hit = (Paragraph)Call(wave, "GetParagraphAtMilliseconds", 1500);
                Check(ReferenceEquals(hit, sub.Paragraphs[1]), "Hit testing prefers the selected duplicate rather than the first row");
                Call(main, "AudioWaveform_OnSingleClick", wave, new AudioVisualizer.ParagraphEventArgs(1.5, hit));
                Check(grid.SelectedIndices.Count == 1 && grid.SelectedIndices[0] == 1, "Actual waveform click retains duplicate row selection");
                var before = new Paragraph(original.Paragraphs[1]); original.Paragraphs[1].StartTime.TotalMilliseconds += 200;
                Call(main, "AudioWaveform_OnStartTimeChanged", wave, new AudioVisualizer.ParagraphEventArgs(1.2, original.Paragraphs[1], before));
                Check(original.Paragraphs[1].StartTime.TotalMilliseconds == 1000, "Start-boundary drag restores a read-only original");
                original.Paragraphs[1].StartTime.TotalMilliseconds += 200; original.Paragraphs[1].EndTime.TotalMilliseconds += 200;
                Call(main, "AudioWaveform_OnTimeChanged", wave, new AudioVisualizer.ParagraphEventArgs(1.2, original.Paragraphs[1], before, AudioVisualizer.MouseDownParagraphType.Whole));
                Check(original.Paragraphs[1].StartTime.TotalMilliseconds == 1000 && original.Paragraphs[1].EndTime.TotalMilliseconds == 2000, "Whole-line drag restores both read-only original boundaries");
                var edited = sub.Paragraphs[1]; var editedBefore = new Paragraph(edited); edited.StartTime.TotalMilliseconds = 1100; edited.EndTime.TotalMilliseconds = 2100;
                Set(main, "_subtitleListViewIndex", 1);
                Call(main, "AudioWaveform_OnTimeChanged", wave, new AudioVisualizer.ParagraphEventArgs(1.1, edited, editedBefore, AudioVisualizer.MouseDownParagraphType.Whole));
                Check(original.Paragraphs.All(p => p.StartTime.TotalMilliseconds == 1000 && p.EndTime.TotalMilliseconds == 2000), "Retiming translation leaves read-only original timings unchanged");
                Check(sub.Paragraphs.All(p => p.StartTime.TotalMilliseconds == 1100 && p.EndTime.TotalMilliseconds == 2100), "Retiming an ASS stack moves both same-time lines");
                Configuration.Settings.General.ShowOriginalAsPreviewIfAvailable = true;
                var replacement = new Subtitle(); replacement.Paragraphs.Add(new Paragraph("New original text", 1000, 2000)); replacement.Paragraphs.Add(new Paragraph("New selected original", 1000, 2000)); replacement.Renumber();
                Set(main, "_subtitleOriginal", replacement); Call(grid, "Fill", sub, replacement); grid.SelectIndexAndEnsureVisible(1);
                Call(main, "SetWaveformPosition", 0d, 1.5d, 1);
                Check(ReferenceEquals(wave.SelectedParagraph, replacement.Paragraphs[1]), "Replacing original subtitle refreshes waveform objects and selected text");
                Check(!ReferenceEquals(wave.SelectedParagraph, original.Paragraphs[1]), "No stale original object remains after refresh");
                main.Hide();
            }
            foreach (var fontSize in new[] { 0, 14 })
            {
            Configuration.Settings.General.SystemSubtitleFontNameOverride = fontSize == 0 ? "" : "Tahoma";
            Configuration.Settings.General.SystemSubtitleFontSizeOverride = fontSize;
            using (var settings = new ClassicEditorPreferencesForm())
            {
                settings.StartPosition = FormStartPosition.Manual; settings.Location = new Point(-30000, -30000); settings.ShowInTaskbar = false;
                settings.Size = settings.MinimumSize; settings.Show(); Application.DoEvents();
                var table = All(settings).OfType<TableLayoutPanel>().Single();
                var label = All(settings).OfType<CheckBox>().Single(c => c.Text.Contains("Профиль правил"));
                Check(table.AutoScroll, "Additional waveform settings use a scrollable native layout");
                table.ScrollControlIntoView(label); Application.DoEvents();
                var rectangle = table.RectangleToClient(label.RectangleToScreen(label.ClientRectangle));
                Check(rectangle.Top >= 0 && rectangle.Bottom <= table.ClientRectangle.Bottom, "Waveform label is accessible after scrolling at minimum window size");
                var measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(1, label.Width - 24), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                Check(measured.Height <= label.Height, "Russian waveform setting label shows all measured wrapped lines");
                var footer = settings.Controls.OfType<FlowLayoutPanel>().Single();
                Check(footer.Bottom <= settings.ClientSize.Height && footer.Controls.OfType<Button>().All(b => b.Bottom <= footer.ClientSize.Height), "Save and Cancel remain accessible while the content scrolls");
                using (var bitmap = new Bitmap(settings.Width, settings.Height)) { settings.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(folder, "editor-minimum-ru-" + fontSize + ".png")); }
                settings.Hide();
            }
            }
            Console.WriteLine("RESULT " + checks + " native waveform/layout checks passed"); result = 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); }
        Console.Out.Flush(); Console.Error.Flush(); TerminateProcess(GetCurrentProcess(), (uint)result); return result;
    }
}

