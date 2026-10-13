using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

internal static class MockSpeechEngine
{
    private static string Argument(string[] args, string key) { int i=Array.IndexOf(args,key); return i>=0 && i+1<args.Length ? args[i+1] : ""; }
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if(args.Contains("--child")) { while(true) Thread.Sleep(100); }
        var mode=Environment.GetEnvironmentVariable("MOCK_SPEECH_MODE") ?? "success";
        var prefix=Argument(args,"--output-file");
        File.WriteAllLines(prefix+".args",args,new UTF8Encoding(false));
        Console.WriteLine("crispasr: progress = 42% (русский текст)");
        if(mode=="hang")
        {
            var child=Process.Start(new ProcessStartInfo(typeof(MockSpeechEngine).Assembly.Location,"--child") { UseShellExecute=false,CreateNoWindow=true });
            File.WriteAllText(prefix+".child",child.Id.ToString());
            while(true) Thread.Sleep(100);
        }
        if(mode=="failure") { Console.Error.WriteLine("fixture failure"); return 7; }
        bool empty=mode=="empty-vad" && args.Contains("--vad");
        File.AppendAllText(prefix+".runs",empty ? "vad\n" : "speech\n");
        File.WriteAllText(prefix+".srt",empty ? "" : "1\n00:00:00,500 --> 00:00:02,200\nПривет, это тест.\n",new UTF8Encoding(false));
        return 0;
    }
}
