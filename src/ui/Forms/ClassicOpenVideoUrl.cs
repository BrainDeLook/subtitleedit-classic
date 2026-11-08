using System;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicOpenVideoUrl : Form
    {
        private readonly TextBox _url;
        public string InputText => _url.Text.Trim();

        public ClassicOpenVideoUrl()
        {
            UiUtil.PreInitialize(this);
            Text = LanguageSettings.Current.Main.OpenVideoFile;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(650, 230);
            MinimumSize = new Size(470, 240);
            var settings = ClassicEditorPreferences.Load();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _url = new TextBox { Dock = DockStyle.Fill, MaxLength = 4096 };
            var browser = new ComboBox { Name = "classicUrlCookieBrowser", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (ClassicCookieBrowser value in Enum.GetValues(typeof(ClassicCookieBrowser))) browser.Items.Add(value == ClassicCookieBrowser.None ? ClassicErrorReport.L("Не использовать", "None") : value.ToString());
            browser.SelectedIndex = (int)settings.VideoCookiesBrowser;
            root.Controls.Add(new Label { Text = "URL", AutoSize = true, Margin = new Padding(3, 6, 8, 8) }, 0, 0); root.Controls.Add(_url, 1, 0);
            root.Controls.Add(new Label { Text = ClassicErrorReport.L("Cookies браузера", "Browser cookies"), AutoSize = true, Margin = new Padding(3, 6, 8, 8) }, 0, 1); root.Controls.Add(browser, 1, 1);
            var hint = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0), Text = ClassicErrorReport.L("Если сайт просит подтвердить, что вы не робот: откройте ссылку в браузере, войдите в аккаунт и выберите этот браузер здесь. Если cookies заняты браузером, закройте его перед повторной попыткой.", "If the site asks you to confirm you are not a bot, open the link in your browser, sign in and select that browser here. If its cookie database is locked, close the browser before retrying.") };
            root.Controls.Add(hint, 0, 2); root.SetColumnSpan(hint, 2);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };
            var cancel = new Button { Text = LanguageSettings.Current.General.Cancel, AutoSize = true, DialogResult = DialogResult.Cancel };
            var open = new Button { Text = ClassicErrorReport.L("Открыть", "Open"), AutoSize = true };
            open.Click += (sender, args) => { if (InputText.Length == 0) { _url.Focus(); return; } settings.VideoCookiesBrowser = (ClassicCookieBrowser)browser.SelectedIndex; settings.Save(); DialogResult = DialogResult.OK; };
            footer.Controls.AddRange(new Control[] { cancel, open }); Controls.Add(root); Controls.Add(footer); AcceptButton = open; CancelButton = cancel;
            UiUtil.FixFonts(this);
        }
    }
}
