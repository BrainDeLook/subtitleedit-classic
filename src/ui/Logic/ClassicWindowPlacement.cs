using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    public static class ClassicWindowPlacement
    {
        private static string FileName => Path.Combine(Configuration.DataDirectory, "ClassicWindowPlacement.json");

        private static Dictionary<string, Rectangle> Read()
        {
            if (!File.Exists(FileName)) return new Dictionary<string, Rectangle>();
            try
            {
                var saved = JsonConvert.DeserializeObject<Dictionary<string, Rectangle>>(File.ReadAllText(FileName));
                return saved == null ? new Dictionary<string, Rectangle>() : saved.Where(p => p.Value.Width >= 100 && p.Value.Height >= 80).ToDictionary(p => p.Key, p => p.Value);
            }
            catch (IOException) { return new Dictionary<string, Rectangle>(); }
            catch (JsonException) { return new Dictionary<string, Rectangle>(); }
        }

        public static void Restore(Form form, string name)
        {
            if (!Read().TryGetValue(name, out var saved)) return;
            var area = Screen.AllScreens.Select(s => s.WorkingArea).OrderByDescending(a => Rectangle.Intersect(a, saved).Width * (long)Rectangle.Intersect(a, saved).Height).First();
            var size = new Size(Math.Min(area.Width, saved.Width), Math.Min(area.Height, saved.Height));
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(Math.Max(area.Left, Math.Min(saved.X, area.Right - size.Width)), Math.Max(area.Top, Math.Min(saved.Y, area.Bottom - size.Height)), size.Width, size.Height);
        }

        public static void Save(Form form, string name)
        {
            var saved = Read();
            saved[name] = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            var temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FileName));
                File.WriteAllText(temporary, JsonConvert.SerializeObject(saved, Formatting.Indented));
                if (File.Exists(FileName)) File.Replace(temporary, FileName, null); else File.Move(temporary, FileName);
            }
            catch (IOException exception) { System.Diagnostics.Debug.WriteLine(exception.Message); }
            catch (UnauthorizedAccessException exception) { System.Diagnostics.Debug.WriteLine(exception.Message); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
