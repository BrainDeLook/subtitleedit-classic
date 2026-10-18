using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Profile = Nikse.SubtitleEdit.Core.Settings.BeautifyTimeCodesSettings.BeautifyTimeCodesProfile;
using MessageBox = Nikse.SubtitleEdit.Forms.SeMsgBox.MessageBox;

namespace Nikse.SubtitleEdit.Forms.BeautifyTimeCodes
{
    public sealed partial class BeautifyTimeCodesProfile
    {
        private Profile _workingProfile;
        private bool _loadingClassicProfile;
        private ClassicBeautifyProfiles _profileLibrary;
        private ToolStripMenuItem _deleteCustomProfile;
        private readonly List<ToolStripItem> _customLoadItems = new List<ToolStripItem>();
        private bool ProfileRu => (Configuration.Settings.General.Language ?? "").StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        private void InitializeClassicProfiles()
        {
            _workingProfile = ClassicBeautifyProfiles.Clone(Configuration.Settings.BeautifyTimeCodes.Profile);
            _profileLibrary = new ClassicBeautifyProfiles(Path.Combine(Configuration.DataDirectory, "ClassicBeautifyProfiles.json"));
            var save = new ToolStripMenuItem(ProfileRu ? "Сохранить как профиль…" : "Save as profile…") { Name = "classicSaveTimingProfile" };
            save.Click += (s, e) => ProfileAction(() =>
            {
                var name = PromptProfileName(); if (name == null) return;
                var exists = _profileLibrary.Read().ContainsKey(name);
                if (exists && MessageBox.Show(this, ProfileRu ? "Заменить профиль «" + name + "»?" : "Overwrite profile '" + name + "'?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                SaveNamedProfile(name, exists);
            });
            _deleteCustomProfile = new ToolStripMenuItem(ProfileRu ? "Удалить профиль" : "Delete profile") { Name = "classicDeleteTimingProfile" };
            menuStrip.Items.Add(save); menuStrip.Items.Add(_deleteCustomProfile);
            RefreshCustomProfiles();
        }
        private Profile CaptureCurrentProfile()
        {
            var profile = ClassicBeautifyProfiles.Clone(_workingProfile); SaveControlsTo(profile); return profile;
        }
        private void SaveNamedProfile(string name, bool overwrite)
        {
            _profileLibrary.Save(name, CaptureCurrentProfile(), overwrite); RefreshCustomProfiles();
        }
        private void LoadNamedProfile(string name)
        {
            var previous = _workingProfile;
            try { _workingProfile = ClassicBeautifyProfiles.Clone(_profileLibrary.Read()[name]); LoadSettings(); }
            catch { _workingProfile = previous; LoadSettings(); throw; }
        }
        private void RefreshCustomProfiles()
        {
            foreach (var item in _customLoadItems) { toolStripMenuItemLoadPreset.DropDownItems.Remove(item); item.Dispose(); }
            _customLoadItems.Clear();
            foreach (ToolStripItem item in _deleteCustomProfile.DropDownItems.Cast<ToolStripItem>().ToArray()) item.Dispose();
            _deleteCustomProfile.DropDownItems.Clear(); _deleteCustomProfile.Enabled = false;
            try
            {
                var profiles = _profileLibrary.Read(); if (profiles.Count == 0) return;
                var separator = new ToolStripSeparator(); toolStripMenuItemLoadPreset.DropDownItems.Add(separator); _customLoadItems.Add(separator);
                foreach (var name in profiles.Keys.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
                {
                    var load = new ToolStripMenuItem(name); load.Click += (s, e) => ProfileAction(() => LoadNamedProfile(name));
                    toolStripMenuItemLoadPreset.DropDownItems.Add(load); _customLoadItems.Add(load);
                    var delete = new ToolStripMenuItem(name);
                    delete.Click += (s, e) => ProfileAction(() =>
                    {
                        if (MessageBox.Show(this, ProfileRu ? "Удалить профиль «" + name + "»?" : "Delete profile '" + name + "'?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                        _profileLibrary.Delete(name); RefreshCustomProfiles();
                    });
                    _deleteCustomProfile.DropDownItems.Add(delete);
                }
                _deleteCustomProfile.Enabled = true;
            }
            catch (Exception ex)
            {
                var error = new ToolStripMenuItem(ProfileRu ? "Ошибка загрузки своих профилей" : "Custom profiles could not be loaded") { Enabled = false, ToolTipText = ex.Message };
                toolStripMenuItemLoadPreset.DropDownItems.Add(error); _customLoadItems.Add(error);
            }
        }
        private void ProfileAction(Action action)
        {
            try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        private string PromptProfileName()
        {
            using (var prompt = new Form { Text = ProfileRu ? "Имя профиля" : "Profile name", StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, ClientSize = new Size(430, 110), FormBorderStyle = FormBorderStyle.FixedDialog })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 2, ColumnCount = 1 };
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                var name = new TextBox { Dock = DockStyle.Top, MaxLength = 100 };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                var cancel = new Button { Text = LanguageSettings.Current.General.Cancel, DialogResult = DialogResult.Cancel, AutoSize = true };
                var ok = new Button { Text = LanguageSettings.Current.General.Ok, DialogResult = DialogResult.OK, AutoSize = true, Enabled = false };
                name.TextChanged += (s, e) => ok.Enabled = !string.IsNullOrWhiteSpace(name.Text);
                buttons.Controls.Add(cancel); buttons.Controls.Add(ok); layout.Controls.Add(name, 0, 0); layout.Controls.Add(buttons, 0, 1); prompt.Controls.Add(layout);
                prompt.AcceptButton = ok; prompt.CancelButton = cancel;
                return prompt.ShowDialog(this) == DialogResult.OK ? ClassicBeautifyProfiles.ValidateName(name.Text) : null;
            }
        }
        private static IEnumerable<Control> ProfileControls(Control parent)
        {
            foreach (Control child in parent.Controls) { yield return child; foreach (var descendant in ProfileControls(child)) yield return descendant; }
        }
        private void ResetZoneMinimums()
        {
            foreach (var control in ProfileControls(this).OfType<NikseUpDown>())
                if (control.Name.Contains("GreenZone")) control.Minimum = 0;
        }
    }
}
