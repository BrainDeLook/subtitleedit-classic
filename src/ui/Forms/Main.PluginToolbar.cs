using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public partial class Main
    {
        private const string PluginToolbarPrefix = "classicPluginToolbar_";

        private void ClearPluginToolbar()
        {
            foreach (var item in toolStrip1.Items.Cast<ToolStripItem>()
                .Where(p => p.Name.StartsWith(PluginToolbarPrefix, StringComparison.Ordinal)).ToArray())
            {
                toolStrip1.Items.Remove(item);
                item.Dispose();
            }
        }

        private bool HasPluginToolbarButtons()
        {
            return toolStrip1.Items.Cast<ToolStripItem>().Any(p =>
                p.Name.StartsWith(PluginToolbarPrefix, StringComparison.Ordinal));
        }

        // Optional plugin metadata. Existing IPlugin implementations require no changes.
        private void AddPluginToolbar(object plugin, ToolStripMenuItem command, string name)
        {
            var type = plugin.GetType();
            var property = type.GetProperty("ToolbarText", BindingFlags.Public | BindingFlags.Instance);
            if (property == null || property.PropertyType != typeof(string)) return;
            var caption = property.GetValue(plugin, null) as string;
            if (string.IsNullOrWhiteSpace(caption)) return;

            var button = new ToolStripSplitButton(caption)
            {
                Name = PluginToolbarPrefix + name,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = command.Text,
                AutoToolTip = false,
            };
            // Forward the original command to preserve shortcut/menu/plugin dispatch.
            button.ButtonClick += (sender, args) => command.PerformClick();
            var configure = type.GetMethod("Configure", new[] { typeof(Form) });
            if (configure != null)
            {
                var settings = button.DropDownItems.Add(
                    System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
                        ? "Настройки…" : "Settings…");
                settings.Click += (sender, args) =>
                {
                    try
                    {
                        var withSubtitle = type.GetMethod("ConfigureWithSubtitle", new[] {
                            typeof(Form), typeof(Nikse.SubtitleEdit.Core.Common.Subtitle), typeof(int[]) });
                        if (withSubtitle == null) configure.Invoke(plugin, new object[] { this });
                        else withSubtitle.Invoke(plugin, new object[] { this,
                            new Nikse.SubtitleEdit.Core.Common.Subtitle(_subtitle), SubtitleListview1.GetSelectedIndices().ToArray() });
                    }
                    catch (Exception ex) { MessageBox.Show(this, (ex.InnerException ?? ex).Message, command.Text); }
                };
            }
            toolStrip1.Items.Add(button);
            toolStrip1.Visible = true;
        }
    }
}
