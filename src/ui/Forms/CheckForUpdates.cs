using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Windows.Forms;
using CheckForUpdatesHelper = Nikse.SubtitleEdit.Logic.CheckForUpdatesHelper;

namespace Nikse.SubtitleEdit.Forms
{
    public partial class CheckForUpdates : Form
    {
        private CheckForUpdatesHelper _updatesHelper;
        private double _seconds;
        private readonly bool _performCheckOnShown = true;
        private readonly Main _mainForm;

        public bool UpdatePlugins { get; set; }

        public CheckForUpdates(Main mainForm)
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);
            UiUtil.FixLargeFonts(this, buttonOK);

            _mainForm = mainForm;
            InitLanguage();
        }

        private void InitLanguage()
        {
            Text = LanguageSettings.Current.CheckForUpdates.Title + " — Classic";
            labelStatus.Text = LanguageSettings.Current.CheckForUpdates.CheckingForUpdates;
            buttonDownloadAndInstall.Text = Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase) ? "Скачать Classic" : "Download Classic";
            buttonDownloadAndInstall.Visible = false;
            textBoxChangeLog.Visible = false;
            buttonOK.Text = LanguageSettings.Current.General.Ok;
            buttonOK.Visible = false;
            buttonDontCheckUpdates.Text = LanguageSettings.Current.CheckForUpdates.NoUpdates;
            buttonDontCheckUpdates.Visible = false;

            labelPluginsHaveUpdates.Visible = false;
            linkLabelUpdatePlugins.Visible = false;

            Location = new System.Drawing.Point(_mainForm.Location.X + (_mainForm.Width / 2) - (Width / 2), _mainForm.Location.Y + (_mainForm.Height / 2) - (Height / 2) - 200);
        }

        public CheckForUpdates(Main mainForm, CheckForUpdatesHelper checkForUpdatesHelper)
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            _mainForm = mainForm;
            _updatesHelper = checkForUpdatesHelper;
            InitLanguage();
            ShowAvailableUpdate();
            _performCheckOnShown = false;
            UiUtil.FixLargeFonts(this, buttonOK);
        }

        private void CheckForUpdates_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
        }

        private async void CheckForUpdates_Shown(object sender, EventArgs e)
        {
            if (!_performCheckOnShown)
            {
                Activate();
                return;
            }

            _updatesHelper = new CheckForUpdatesHelper();
            await _updatesHelper.CheckForUpdates(true);
            if(IsDisposed || Disposing)return;
            timerCheckForUpdates.Start();

            buttonOK.Focus();
            Activate();
        }

        private void timerCheckForUpdates_Tick(object sender, EventArgs e)
        {
            if (_seconds > 10)
            {
                timerCheckForUpdates.Stop();
                labelStatus.Text = string.Format(LanguageSettings.Current.CheckForUpdates.CheckingForUpdatesFailedX, "Time out");
                buttonOK.Visible = true;
            }
            else if (_updatesHelper.Error != null)
            {
                timerCheckForUpdates.Stop();
                labelStatus.Text = string.Format(LanguageSettings.Current.CheckForUpdates.CheckingForUpdatesFailedX, _updatesHelper.Error);
                buttonOK.Visible = true;
            }
            else if (_updatesHelper.Done)
            {
                timerCheckForUpdates.Stop();
                ShowAvailableUpdate();
            }

            _seconds += timerCheckForUpdates.Interval / TimeCode.BaseUnit;

            if (buttonDownloadAndInstall.Visible)
            {
                buttonDownloadAndInstall.Focus();
            }
            else if (buttonOK.Visible)
            {
                buttonOK.Focus();
            }
        }

        private void ShowAvailableUpdate()
        {
            //TODO: clean this method a little up

            var hideChangeLog = !_updatesHelper.ManualCheck;
            if (_updatesHelper.IsNewSubtitleEditAvailable())
            {
                hideChangeLog = false;
            }

            if (_updatesHelper.PluginUpdates > 0)
            {
                if (_updatesHelper.PluginUpdates == 1)
                {
                    labelPluginsHaveUpdates.Text = LanguageSettings.Current.CheckForUpdates.OnePluginsHasAnUpdate;
                }
                else
                {
                    labelPluginsHaveUpdates.Text = string.Format(LanguageSettings.Current.CheckForUpdates.XPluginsHasAnUpdate, _updatesHelper.PluginUpdates);
                }

                linkLabelUpdatePlugins.Text = LanguageSettings.Current.CheckForUpdates.Update;
                labelPluginsHaveUpdates.Visible = true;
                linkLabelUpdatePlugins.Visible = true;

                if (hideChangeLog)
                {
                    MinimizeBox = false;
                    MaximizeBox = false;
                    FormBorderStyle = FormBorderStyle.FixedDialog;
                    labelStatus.Visible = false;
                    var w = linkLabelUpdatePlugins.Right + 75;
                    var h = 120;
                    MinimumSize = new System.Drawing.Size(w, h);
                    Height = h;
                    Width = w;
                }

                linkLabelUpdatePlugins.Left = labelPluginsHaveUpdates.Right - 2;
                linkLabelUpdatePlugins.BringToFront();
            }
            else
            {
                labelPluginsHaveUpdates.Visible = false;
                linkLabelUpdatePlugins.Visible = false;
                textBoxChangeLog.Height += 14;
            }

            if (!hideChangeLog)
            {
                Height = 600;
                MinimumSize = new System.Drawing.Size(500, 400);
            }

            buttonDownloadAndInstall.Visible = _updatesHelper.IsNewSubtitleEditAvailable();
            if (ClassicReleaseInstaller.CanInstallAndRestart && _updatesHelper.InstallerUrl != null && _updatesHelper.ChecksumsUrl != null) buttonDownloadAndInstall.Text = Configuration.Settings.General.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "Установить и перезапустить" : "Install and restart";

            if (_updatesHelper.IsNewSubtitleEditAvailable() || _updatesHelper.ManualCheck)
            {
                textBoxChangeLog.Text = _updatesHelper.LatestChangeLog;
                textBoxChangeLog.Visible = true;
                buttonOK.Visible = true;
                if (_updatesHelper.IsNewSubtitleEditAvailable())
                {
                    buttonDontCheckUpdates.Visible = true;
                    labelStatus.Text = LanguageSettings.Current.CheckForUpdates.CheckingForUpdatesNewVersion;
                }
                else
                {
                    buttonDontCheckUpdates.Visible = false;
                    buttonDownloadAndInstall.Left = buttonOK.Left - 6 - buttonDownloadAndInstall.Width;
                    labelStatus.Text = LanguageSettings.Current.CheckForUpdates.CheckingForUpdatesNoneAvailable;
                }
            }
            else
            {
                textBoxChangeLog.Visible = false;
            }
        }

        private async void buttonDownloadAndInstall_Click(object sender, EventArgs e)
        {
            if (!ClassicReleaseInstaller.CanInstallAndRestart || _updatesHelper?.InstallerUrl == null || _updatesHelper.ChecksumsUrl == null) { UiUtil.OpenUrl(_updatesHelper?.ReleasePageUrl ?? CheckForUpdatesHelper.LatestStableUrl); return; }
            buttonDownloadAndInstall.Enabled = false;
            using (var cancel = new System.Threading.CancellationTokenSource())
            {
                string installer = null; bool handedOff = false;
                FormClosingEventHandler closing = (s, args) => cancel.Cancel(); FormClosing += closing;
                try
                {
                    installer = await ClassicReleaseInstaller.Download(_updatesHelper, cancel.Token, new Progress<long>(bytes => labelStatus.Text = (bytes / 1048576.0).ToString("0.0") + " MB"));
                    if (IsDisposed) return;
                    var ru = Configuration.Settings.General.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
                    if (System.Windows.Forms.MessageBox.Show(this, ru ? "Установщик проверен. Сохраните изменения при закрытии редактора. Установить обновление и перезапустить Classic?" : "The installer is verified. Save your changes when the editor closes. Install the update and restart Classic?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                    var application = typeof(Main).Assembly.Location;
                    // Main owns the existing unsaved-work confirmation. A cancelled close leaves it running.
                    Close(); _mainForm.Close();
                    if (_mainForm.IsDisposed) { ClassicReleaseInstaller.InstallAndRestart(installer, application); handedOff = true; }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex is System.IO.IOException || ex is System.Net.Http.HttpRequestException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
                { if (!IsDisposed) labelStatus.Text = ex.Message; }
                finally { if (!handedOff && installer != null && System.IO.File.Exists(installer)) System.IO.File.Delete(installer); FormClosing -= closing; if (!IsDisposed) buttonDownloadAndInstall.Enabled = true; }
            }
        }

        private void buttonDontCheckUpdates_Click(object sender, EventArgs e)
        {
            Configuration.Settings.General.CheckForUpdates = false;
            DialogResult = DialogResult.Cancel;
        }

        private void linkLabelUpdatePlugins_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            UpdatePlugins = true;
            DialogResult = DialogResult.OK;
        }
    }
}
