using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class GenerateVideoWithHardSubs
    {
        private readonly List<object> _hardSubPlugins = new List<object>();
        private double _pluginCutStart, _pluginCutEnd, _pluginClockScale;
        private string _hardSubPluginError;

        // No plugin = no additional controls, formats, metadata or processing.
        public void InitializeHardSubPlugins(Subtitle document, int[] selected)
        {
            if (_hardSubPlugins.Count != 0) return;
            foreach (var file in Configuration.GetPlugins())
            {
                try
                {
                    var plugin = Main.GetPropertiesAndDoAction(file, out _, out _, out _, out _, out _, out _, out _);
                    if (plugin == null) continue;
                    var type = plugin.GetType();
                    var create = type.GetMethod("CreateHardSubControl", new[] { typeof(Form), typeof(Subtitle), typeof(int[]) });
                    if (create == null || type.GetMethod("ValidateHardSub") == null || type.GetMethod("ProcessHardSub") == null) continue;
                    var control = create.Invoke(plugin, new object[] { this, new Subtitle(document), (int[])(selected ?? new int[0]).Clone() }) as Control;
                    if (control == null) continue;
                    var extra = Math.Max(30, control.Height) + 8;
                    var preview = videoPlayerContainer1.Bounds;
                    ClientSize = new Size(ClientSize.Width, ClientSize.Height + extra);
                    MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + extra);
                    groupBoxAudio.Height += extra;
                    videoPlayerContainer1.Bounds = new Rectangle(preview.X, preview.Y + extra, preview.Width, preview.Height);
                    groupBoxAudio.Controls.Add(control);
                    control.BringToFront();
                    _hardSubPlugins.Add(plugin);
                }
                catch (Exception ex) { MessageBox.Show(this, (ex.InnerException ?? ex).Message, "Export plugin"); }
            }
        }

        private bool ValidateHardSubPlugins()
        {
            _hardSubPluginError = null;
            if (_hardSubPlugins.Count == 0) return true;
            _pluginCutStart = checkBoxCut.Checked ? GetCutStart().TotalMilliseconds : 0;
            _pluginCutEnd = checkBoxCut.Checked ? GetCutEnd().TotalMilliseconds : double.PositiveInfinity;
            _pluginClockScale = Configuration.Settings.General.CurrentVideoIsSmpte && (decimal)_videoInfo.FramesPerSecond % 1 != 0 ? 1.001 : 1;
            try
            {
                foreach (var plugin in _hardSubPlugins)
                    plugin.GetType().GetMethod("ValidateHardSub").Invoke(plugin,
                        new object[] { VideoFileName, BatchMode, _pluginCutStart, _pluginCutEnd, _pluginClockScale });
                return true;
            }
            catch (Exception ex)
            {
                _hardSubPluginError = (ex.InnerException ?? ex).Message;
                MessageBox.Show(this, _hardSubPluginError, "Export plugin");
                return false;
            }
        }

        private bool ProcessHardSubPlugins()
        {
            if (_abort || _hardSubPlugins.Count == 0) return !_abort;
            var work = Task.Run(() =>
            {
                foreach (var plugin in _hardSubPlugins)
                    plugin.GetType().GetMethod("ProcessHardSub").Invoke(plugin,
                        new object[] { VideoFileName, _pluginCutStart, _pluginCutEnd, _pluginClockScale,
                            new Func<bool>(() => System.Threading.Volatile.Read(ref _abort)) });
            });
            // The existing renderer pumps the UI while FFmpeg works. Keep cancellation
            // and the same form-close guard active during streaming metadata writes.
            while (!work.IsCompleted) { Application.DoEvents(); System.Threading.Thread.Sleep(25); }
            try { work.GetAwaiter().GetResult(); return !_abort; }
            catch (Exception ex)
            {
                _hardSubPluginError = (ex.InnerException ?? ex).Message;
                _log.AppendLine("Marker plugin: " + _hardSubPluginError);
                return false;
            }
        }
    }
}
