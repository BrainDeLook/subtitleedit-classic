using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestModelessSpeech
{
    static void Check(bool value, string description) { if (!value) throw new Exception(description); Console.WriteLine("PASS: " + description); }
    static void Pump(int milliseconds = 250) { var end = DateTime.UtcNow.AddMilliseconds(milliseconds); while (DateTime.UtcNow < end) { Application.DoEvents(); Thread.Sleep(10); } }
    static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    static void Set(object value, string name, object content) => value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, content);
    static object Call(object value, string name) => value.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(value, null);
    static TextToSpeech Open(Form owner, Func<Subtitle> source, Func<int> selected)
    {
        var form = new TextToSpeech(source(), new SubRip(), "", null) { MainSubtitleSource = source, MainSelectedIndex = selected, MainSubtitleFormat = () => new SubRip(), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        form.InitializeModelessSession(); form.Show(owner); Pump(); return form;
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var data = Path.GetFullPath(args[0]); Directory.CreateDirectory(data);
            typeof(Configuration).GetField("DataDirectory", BindingFlags.Public | BindingFlags.Static).SetValue(null, data + Path.DirectorySeparatorChar);
            Configuration.Settings.General.Language = "ru-RU";
            LanguageSettings.Current = Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages/ru-RU.xml"));
            Application.EnableVisualStyles();
            var source = new Subtitle(); source.Paragraphs.Add(new Paragraph("First", 0, 2000)); source.Paragraphs.Add(new Paragraph("Second", 3000, 5000));
            int selected = 0;
            using (var owner = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
            {
                owner.Show();
                using (var form = Open(owner, () => source, () => selected))
                {
                    Check(owner.Enabled && !form.Modal, "main editor remains enabled with speech settings open");
                    selected = 1; Pump();
                    Check(form.PlaybackFromIndex == 1 && ((Button)Field(form, "_playSpeech")).Text.Contains("2"), "selecting another main row updates speech start line");
                    source.Paragraphs[1].Text = "Edited in main window";
                    Call(form, "SynchronizeMainSubtitle");
                    Check(((Subtitle)Field(form, "_subtitle")).Paragraphs[1].Text == source.Paragraphs[1].Text, "live main edits refresh the speech snapshot");
                    var baseline = (Subtitle)Field(form, "_modelessBaseline");
                    ((Subtitle)Field(form, "_subtitle")).Paragraphs[0].Text = "Speech preparation";
                    source.Paragraphs[0].Text = "Main edit preserved";
                    Call(form, "SynchronizeMainSubtitle");
                    Check(((Subtitle)Field(form, "_subtitle")).Paragraphs[0].Text == "Speech preparation" && source.Paragraphs[0].Text == "Main edit preserved", "concurrent preparation and main edits remain separate");
                    bool called = false;
                    form.ApplyModelessSubtitle = (before, edited) => { called = true; return false; };
                    ((Button)Field(form, "buttonOK")).PerformClick(); Pump();
                    Check(called && !form.IsDisposed, "rejected conflicting edits keep the settings window open");
                    form.ApplyModelessSubtitle = (before, edited) => true;
                    ((Button)Field(form, "buttonOK")).PerformClick(); Pump();
                    Check(form.IsDisposed && owner.Enabled, "OK closes modeless settings and leaves editor usable");
                }
                foreach (var cancellationField in new[] { "_localCancellation", "_qwenCancellation", "_castCancellation", "_compatibleCancellation", "_dubbingCancellation", "_playbackCancellation" })
                using (var form = Open(owner, () => source, () => selected))
                {
                    using (var cancellation = new CancellationTokenSource())
                    {
                        Set(form, cancellationField, cancellation); form.Close(); Pump();
                        Check(cancellation.IsCancellationRequested && !form.IsDisposed, "close cancels active work and waits for cleanup: " + cancellationField);
                        Set(form, cancellationField, null); Pump();
                        Check(form.IsDisposed && owner.Enabled, "settings close automatically after cleanup: " + cancellationField);
                    }
                }
                using (var form = Open(owner, () => source, () => selected))
                {
                    ((Button)Field(form, "buttonCancel")).PerformClick(); Pump();
                    Check(form.IsDisposed && owner.Enabled, "Cancel closes modeless settings");
                }
                owner.Close();
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

