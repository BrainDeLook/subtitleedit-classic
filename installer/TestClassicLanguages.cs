using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;

// Compile next to the installed application to exercise its real language picker.
internal static class TestClassicLanguages
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        var assembly = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubtitleEdit.exe"));
        var languageType = assembly.GetType("Nikse.SubtitleEdit.Logic.Language", true);
        var load = languageType.GetMethod("Load");
        var compatibleVersion = (string)languageType.GetField("CompatibleTranslationVersion").GetRawConstantValue();
        var count = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages"), "*.xml"))
        {
            load.Invoke(null, new object[] { file });
            var document = new XmlDocument();
            document.Load(file);
            if (document.SelectSingleNode("Language/General/Version").InnerText.Trim() == compatibleVersion) count++;
        }
        using (var picker = (Form)Activator.CreateInstance(assembly.GetType("Nikse.SubtitleEdit.Forms.Options.ChooseLanguage", true)))
        {
            var combo = picker.GetType().GetField("comboBoxLanguages", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(picker);
            var items = (ICollection)combo.GetType().GetProperty("Items").GetValue(combo, null);
            if (items.Count != count + 1) throw new Exception("Not all installed translations are visible: " + items.Count + " vs " + (count + 1));
            var russian = false;
            foreach (var item in items)
                if ((string)item.GetType().GetProperty("CultureName").GetValue(item, null) == "ru-RU") russian = true;
            if (!russian) throw new Exception("Russian is missing from the language picker.");
            Console.WriteLine("Loaded all translation files; {0} compatible translations, picker contains {1} languages including Russian.", count, items.Count);
        }
    }
}
