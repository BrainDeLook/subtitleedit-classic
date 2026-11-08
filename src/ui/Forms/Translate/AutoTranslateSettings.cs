using Nikse.SubtitleEdit.Core.AutoTranslate;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Settings;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Translate
{
    public sealed partial class AutoTranslateSettings : Form
    {
        private readonly Type _engineType;
        private readonly TextBox _microsoftRegion = new TextBox();
        private readonly CheckBox _keepMusic = new CheckBox();

        public AutoTranslateSettings(Type engineType, string engineName)
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            _engineType = engineType;
            var oldHeight=ClientSize.Height;ClientSize=new System.Drawing.Size(ClientSize.Width,oldHeight+30);
            nikseTextBoxPrompt.Height-=30;
            _keepMusic.Text=Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Не переводить реплики с музыкальными символами", "Keep music/lyric lines untranslated");
            _keepMusic.Name="classicKeepMusicLines";_keepMusic.AutoSize=true;_keepMusic.Left=12;_keepMusic.Top=buttonOk.Top-27;_keepMusic.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
            _keepMusic.Checked=Configuration.Settings.Tools.AutoTranslateKeepMusicLines;Controls.Add(_keepMusic);_keepMusic.BringToFront();

            Text = LanguageSettings.Current.General.Advanced;
            labelDelay.Text = LanguageSettings.Current.GoogleTranslate.Delay;
            labelMaxBytes.Text = LanguageSettings.Current.GoogleTranslate.MaxBytes;
            labelParagraphHandling.Text = LanguageSettings.Current.GoogleTranslate.LineMergeHandling;
            labelPrompt.Text = string.Format(LanguageSettings.Current.GoogleTranslate.PromptX, engineName);
            buttonOk.Text = LanguageSettings.Current.General.Ok;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;

            nikseUpDownDelay.Value = Configuration.Settings.Tools.AutoTranslateDelaySeconds;

            if (Configuration.Settings.Tools.AutoTranslateMaxBytes <= 0)
            {
                Configuration.Settings.Tools.AutoTranslateMaxBytes = new ToolsSettings().AutoTranslateMaxBytes;
            }
            nikseUpDownMaxBytes.Value = Configuration.Settings.Tools.AutoTranslateMaxBytes;

            comboBoxParagraphHandling.Left = labelParagraphHandling.Right + 4;
            nikseUpDownDelay.Left = labelDelay.Right + 4;
            nikseUpDownMaxBytes.Left = labelMaxBytes.Right + 4;

            labelTemperature.Visible = false;
            nikseUpDownTemperature.Visible = false;
            nikseUpDownTemperature.Left = labelTemperature.Right + 4;

            if (_engineType == typeof(ChatGptTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.ChatGptPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().ChatGptPrompt;
                }
            }
            else if (_engineType == typeof(OllamaTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.OllamaPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().OllamaPrompt;
                }
            }
            else if (_engineType == typeof(LmStudioTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.LmStudioPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().LmStudioPrompt;
                }
            }
            else if (_engineType == typeof(KoboldCppTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.KoboldCppPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().KoboldCppPrompt;
                }

                labelTemperature.Visible = true;
                nikseUpDownTemperature.Visible = true;
                if (Configuration.Settings.Tools.KoboldCppTemperature < 0.0m || Configuration.Settings.Tools.KoboldCppTemperature > 1.5m)
                {
                    Configuration.Settings.Tools.KoboldCppTemperature = new ToolsSettings().KoboldCppTemperature;
                }
                nikseUpDownTemperature.Value = Configuration.Settings.Tools.KoboldCppTemperature;
            }
            else if (_engineType == typeof(AnthropicTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.AnthropicPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().AnthropicPrompt;
                }
            }
            else if (_engineType == typeof(GroqTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.GroqPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().GroqPrompt;
                }
            }
            else if (_engineType == typeof(DeepSeekTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.DeepSeekPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().DeepSeekPrompt;
                }
            }
            else if (_engineType == typeof(AvalAi))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.AvalAiPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().AvalAiPrompt;
                }
            }
            else if (_engineType == typeof(OpenRouterTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.OpenRouterPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().OpenRouterPrompt;
                }
            }
            else if (_engineType == typeof(MistralTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.AutoTranslateMistralPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().AutoTranslateMistralPrompt;
                }
            }
            else if (_engineType == typeof(GeminiTranslate))
            {
                nikseTextBoxPrompt.Text = Configuration.Settings.Tools.GeminiPrompt;
                if (string.IsNullOrWhiteSpace(nikseTextBoxPrompt.Text))
                {
                    nikseTextBoxPrompt.Text = new ToolsSettings().GeminiPrompt;
                }
            }
            else if (_engineType == typeof(MicrosoftTranslator))
            {
                labelPrompt.Text = Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Регион Azure Translator", "Azure Translator region");
                nikseTextBoxPrompt.Visible = false;
                _microsoftRegion.Text = Configuration.Settings.Tools.MicrosoftTranslatorRegion;
                _microsoftRegion.SetBounds(nikseTextBoxPrompt.Left, nikseTextBoxPrompt.Top, 350, 25); Controls.Add(_microsoftRegion);
                Controls.Add(new Label { Text = Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Например westus2. Пустое поле или global используют общий endpoint.", "For example westus2. Empty or global uses the configured global endpoint."), AutoSize = true, MaximumSize = new System.Drawing.Size(730, 0), Left = nikseTextBoxPrompt.Left, Top = nikseTextBoxPrompt.Top + 35 });
                MinimumSize = new System.Drawing.Size(MinimumSize.Width, MinimumSize.Height - 130); Height -= 130;
            }
            else
            {
                labelPrompt.Visible = false;
                nikseTextBoxPrompt.Visible = false;
                MinimumSize = new System.Drawing.Size(MinimumSize.Width, MinimumSize.Height - nikseTextBoxPrompt.Height);
                Height -= nikseTextBoxPrompt.Height;
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
            }

            comboBoxParagraphHandling.Items.Clear();
            comboBoxParagraphHandling.Items.Add(LanguageSettings.Current.GenerateVideoWithEmbeddedSubs.Default);
            comboBoxParagraphHandling.Items.Add(LanguageSettings.Current.GoogleTranslate.TranslateLinesSeparately);
            comboBoxParagraphHandling.SelectedIndex = 0;
            if (Enum.TryParse<TranslateStrategy>(Configuration.Settings.Tools.AutoTranslateStrategy, out var ts) &&
                ts == TranslateStrategy.TranslateEachLineSeparately)
            {
                comboBoxParagraphHandling.SelectedIndex = 1;
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            Configuration.Settings.Tools.AutoTranslateKeepMusicLines = _keepMusic.Checked;
            if (_engineType == typeof(MicrosoftTranslator))
            {
                try { MicrosoftTranslator.GetTokenEndpoint(Configuration.Settings.Tools.MicrosoftTranslatorTokenEndpoint, _microsoftRegion.Text); }
                catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, Text); return; }
                Configuration.Settings.Tools.MicrosoftTranslatorRegion = _microsoftRegion.Text.Trim();
            }

            if (nikseTextBoxPrompt.Text.Replace("{0}", string.Empty).Replace("{1}", string.Empty).Contains('{'))
            {
                MessageBox.Show("Character not allowed in prompt: '{' (besides '{0}' and '{1}')");
                return;
            }

            if (nikseTextBoxPrompt.Text.Replace("{0}", string.Empty).Replace("{1}", string.Empty).Contains('}'))
            {
                MessageBox.Show("Character not allowed in prompt: '}' (besides '{0}' and '{1}')");
                return;
            }

            if (nikseTextBoxPrompt.Text.Length > 4000)
            {
                MessageBox.Show("Warning: Prompt may be too long for some translation engines.");
            }

            Configuration.Settings.Tools.AutoTranslateDelaySeconds = (int)nikseUpDownDelay.Value;
            Configuration.Settings.Tools.AutoTranslateMaxBytes = (int)nikseUpDownMaxBytes.Value;

            if (_engineType == typeof(ChatGptTranslate))
            {
                Configuration.Settings.Tools.ChatGptPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(OllamaTranslate))
            {
                Configuration.Settings.Tools.OllamaPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(LmStudioTranslate))
            {
                Configuration.Settings.Tools.LmStudioPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(KoboldCppTranslate))
            {
                Configuration.Settings.Tools.KoboldCppPrompt = nikseTextBoxPrompt.Text;
                Configuration.Settings.Tools.KoboldCppTemperature = (decimal)nikseUpDownTemperature.Value;
            }
            else if (_engineType == typeof(AnthropicTranslate))
            {
                Configuration.Settings.Tools.AnthropicPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(GroqTranslate))
            {
                Configuration.Settings.Tools.GroqPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(DeepSeekTranslate))
            {
                Configuration.Settings.Tools.DeepSeekPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(AvalAi))
            {
                Configuration.Settings.Tools.AvalAiPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(OpenRouterTranslate))
            {
                Configuration.Settings.Tools.OpenRouterPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(MistralTranslate))
            {
                Configuration.Settings.Tools.AutoTranslateMistralPrompt = nikseTextBoxPrompt.Text;
            }
            else if (_engineType == typeof(GeminiTranslate))
            {
                Configuration.Settings.Tools.GeminiPrompt = nikseTextBoxPrompt.Text;
            }

            if (comboBoxParagraphHandling.SelectedIndex == 1)
            {
                Configuration.Settings.Tools.AutoTranslateStrategy = TranslateStrategy.TranslateEachLineSeparately.ToString();
            }
            else
            {
                Configuration.Settings.Tools.AutoTranslateStrategy = TranslateStrategy.Default.ToString();
            }

            DialogResult = DialogResult.OK;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void buttonCancel_KeyDown(object sender, KeyEventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void AutoTranslateSettings_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
            else if (e.KeyData == UiUtil.HelpKeys)
            {
                UiUtil.ShowHelp("#translation");
            }
        }
    }
}
