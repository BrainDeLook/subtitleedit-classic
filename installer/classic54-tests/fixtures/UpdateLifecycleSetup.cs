using System;
using System.IO;
class UpdateLifecycleSetup
{
 static int Main(string[] args){var root=AppDomain.CurrentDomain.BaseDirectory;File.WriteAllText(Path.Combine(root,"setup-args.txt"),string.Join("|",args));return File.Exists(Path.Combine(root,"fail"))?7:0;}
}
