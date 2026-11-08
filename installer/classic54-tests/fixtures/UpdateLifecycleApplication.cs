using System;
using System.IO;
class UpdateLifecycleApplication{static void Main(){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"restarted.txt"),"restarted");}}
