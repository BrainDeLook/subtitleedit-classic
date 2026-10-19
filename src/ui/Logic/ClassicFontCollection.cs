using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicFontFile
    {
        public string Family { get; set; }
        public string Path { get; set; }
        public byte[] Bytes { get; set; }
        public bool Embedded { get; set; }
        public byte[] Read() => Bytes ?? File.ReadAllBytes(Path);
    }

    public static class ClassicFontCollection
    {
        public static string Folder => System.IO.Path.Combine(Configuration.DataDirectory, "Fonts");
        public static string FilterOption => Directory.Exists(Folder) ? ":fontsdir=" + EscapeFilterValue(Folder) : string.Empty;
        private static string EscapeFilterValue(string path)
        {
            // The graph parser and the ASS option parser each consume one escaping layer.
            // Quoting the whole value loses apostrophes after those two passes.
            return path.Replace("\\", "/").Replace(":", @"\\:").Replace("'", @"\\\'")
                .Replace(",", @"\,").Replace(";", @"\;").Replace("[", @"\[").Replace("]", @"\]").Replace(" ", @"\ ");
        }
        private static readonly PrivateFontCollection Loaded = new PrivateFontCollection();
        private static readonly HashSet<string> LoadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        [DllImport("gdi32.dll", CharSet=CharSet.Unicode)]
        private static extern int AddFontResourceEx(string name, uint flags, IntPtr reserved);

        public static void LoadCollected()
        {
            if (!Directory.Exists(Folder)) return;
            lock (LoadedPaths)
            foreach (var file in Directory.GetFiles(Folder).Where(IsFont))
                if (LoadedPaths.Add(file))
                    try { Loaded.AddFontFile(file); AddFontResourceEx(file, 0x10, IntPtr.Zero); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }
        public static IEnumerable<string> CollectedNames()
        {
            LoadCollected();
            lock (LoadedPaths) return Loaded.Families.Select(f=>f.Name).Distinct().ToArray();
        }
        public static System.Drawing.FontFamily[] CollectedFamilies()
        {
            LoadCollected();
            lock (LoadedPaths) return Loaded.Families;
        }
        private static string _collectionStamp;
        private static List<ClassicFontFile> _collectionFiles = new List<ClassicFontFile>();
        public static List<ClassicFontFile> CollectedFiles()
        {
            lock (LoadedPaths)
            {
                var files = Directory.Exists(Folder) ? Directory.GetFiles(Folder).Where(IsFont).OrderBy(p => p).ToArray() : new string[0];
                var stamp = string.Join("|", files.Select(p => p + File.GetLastWriteTimeUtc(p).Ticks + new FileInfo(p).Length));
                if (_collectionStamp != stamp)
                {
                    var result = new List<ClassicFontFile>();
                    foreach (var path in files)
                        try { foreach (var family in ReadFamilies(File.ReadAllBytes(path))) result.Add(new ClassicFontFile { Family = family, Path = path }); }
                        catch (IOException ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    _collectionFiles = result; _collectionStamp = stamp;
                }
                return _collectionFiles;
            }
        }
        private static bool IsFont(string path) => new[]{".ttf",".otf",".ttc"}.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());

        public static List<ClassicFontFile> Scan(Subtitle subtitle, CancellationToken token)
        {
            var result = new List<ClassicFontFile>();
            foreach (var font in AssaFontEmbedder.GetEmbeddedFonts(subtitle.Footer))
            {
                token.ThrowIfCancellationRequested();
                foreach (var family in ReadFamilies(font.Bytes))
                    result.Add(new ClassicFontFile { Family=family, Path=font.FileName, Bytes=font.Bytes, Embedded=true });
            }
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in new[]{Folder,Environment.GetFolderPath(Environment.SpecialFolder.Fonts),System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Fonts")})
                if (Directory.Exists(directory)) foreach (var file in Directory.GetFiles(directory).Where(IsFont)) files.Add(file);
            // Registry entries also cover fonts installed outside the standard folders.
            foreach (var hive in new[]{Registry.LocalMachine, Registry.CurrentUser})
            using (var key=hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"))
                if (key!=null) foreach (var name in key.GetValueNames())
                {
                    var file=key.GetValue(name) as string;
                    if (string.IsNullOrEmpty(file)) continue;
                    if (!System.IO.Path.IsPathRooted(file)) file=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),file);
                    if (File.Exists(file) && IsFont(file)) files.Add(file);
                }
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using (var fonts=new PrivateFontCollection())
                    {
                        fonts.AddFontFile(file);
                        foreach(var family in fonts.Families) result.Add(new ClassicFontFile { Family=family.Name, Path=file });
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
            return result;
        }
        private static IEnumerable<string> ReadFamilies(byte[] bytes)
        {
            var pointer=Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes,0,pointer,bytes.Length);
                using(var fonts=new PrivateFontCollection())
                { fonts.AddMemoryFont(pointer,bytes.Length); return fonts.Families.Select(f=>f.Name).ToArray(); }
            }
            catch { return new string[0]; }
            finally { Marshal.FreeHGlobal(pointer); }
        }
        public static string Copy(ClassicFontFile font, string folder, bool trim, Subtitle subtitle)
        {
            Directory.CreateDirectory(folder);
            var bytes=font.Read();
            if (trim) bytes=FontTrimmer.Trim(bytes,AssaFontEmbedder.GetUsedTextLines(subtitle)).Bytes;
            var name=System.IO.Path.GetFileName(font.Path);
            var path=System.IO.Path.Combine(folder,name);
            if (File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                // Never replace another collected face just because its file name matches.
                using(var hash=SHA256.Create()) name=System.IO.Path.GetFileNameWithoutExtension(name)+"_"+BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").Substring(0,12)+System.IO.Path.GetExtension(name);
                path=System.IO.Path.Combine(folder,name);
            }
            if (!File.Exists(path)) File.WriteAllBytes(path,bytes);
            return path;
        }
    }
}
