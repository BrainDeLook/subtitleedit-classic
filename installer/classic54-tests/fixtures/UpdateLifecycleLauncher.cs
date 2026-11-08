using System;
using System.Threading;
using Nikse.SubtitleEdit.Logic;
class UpdateLifecycleLauncher
{
 static void Main(string[] args){ClassicReleaseInstaller.InstallAndRestart(args[0],args[1]);Thread.Sleep(1500);}
}
