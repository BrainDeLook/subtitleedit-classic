# Building Subtitle Edit Classic

[Back to the project](../README.md) · [Русский README](../README.ru.md)


From a Visual Studio Developer PowerShell on Windows, with the .NET 8 SDK and .NET Framework 4.8 / 4.8.1 targeting packs:

```powershell
msbuild src/UpdateAssemblyInfo/UpdateAssemblyInfo.csproj /restore /p:Configuration=Release
& ./src/UpdateAssemblyInfo/bin/Release/net48/win-x64/UpdateAssemblyInfo.exe src/ui/Properties/AssemblyInfo.cs.template src/libse/Properties/AssemblyInfo.cs.template
msbuild SubtitleEdit.sln /restore /p:Configuration=Release /p:SolutionDir="$PWD\"
```

The solution builds the original Windows Forms application. Build the normal Windows installer with Inno Setup 6.2.1 or newer: `ISCC.exe /DCLASSIC_FORK installer/Subtitle_Edit_installer.iss`. The classic-fork switch gives the installer its own application identity, installation directory, and shortcuts.

Original Subtitle Edit is developed by Nikolaj Olsson and contributors. This fork retains the upstream GPL-3.0 license and attribution.

The [Windows CI workflow](../.github/workflows/classic-build.yml) is the reference for dependency setup, packaging and installed-application checks. Tests use isolated application data. Paid provider tests require separate credentials and are not part of the automated validation.
