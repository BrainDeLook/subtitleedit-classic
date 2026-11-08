# Build Classic from source

The Windows Forms application targets .NET Framework 4.8. The verified toolchain is the .NET SDK 10.0.401 on Windows, with the .NET Framework reference assemblies restored through NuGet. No installed copy of Subtitle Edit is required to compile it. Models and paid services are not involved.

From the repository root:

```powershell
./installer/Build-Classic54.ps1 -Revision 57
```

The script restores packages and builds the CLI project together with its UI/core references. All three output versions are checked. After a successful restore, `-NoRestore` avoids restoring again. Output is under `src/seconv/bin/Release/net48`; `-Configuration Debug` selects the debug output instead.

The direct equivalent is:

```powershell
dotnet restore src/seconv/seconv.csproj
dotnet build src/seconv/seconv.csproj --no-restore -c Release -p:ClassicRevision=57
```

`Directory.Build.targets` generates assembly metadata from the existing templates into each project's intermediate folder before that project compiles. It does not modify tracked source files. The revision must be 0–65534 and defaults to 0 without an explicit argument. This avoids the old UI-only helper running after an unversioned core had already compiled. The historical build helpers remain available for older release tooling.

SDK-generated WinForms resources use `System.Resources.Extensions`. Keep the generated EXE configs and runtime libraries with the application; the redirects are needed when reading serialized image resources. Both the Inno Setup file list and the local package builder include the resource runtime dependencies. Existing nullable-annotation and duplicate compatibility-type warnings remain; the verified builds have no errors.

To create an unpublished local patch for an existing Classic 4.0.28 installation:

```powershell
./installer/Build-Classic54LocalPackage.ps1 `
    -ApplicationDirectory ./src/seconv/bin/Release/net48 `
    -ConsoleExecutable ./src/seconv/bin/Release/net48/seconv.exe `
    -OutputDirectory ./artifacts/Classic-4.0.28.57-sdk-local
```

Choose a new output directory for each package. The package contains hashes, the runtime EXE configs and dependencies, license notices, and the backup/rollback installer. It excludes models, generated media and the NuGet cache. Building or packaging does not update the installed application, change AppData or publish to GitHub.

Checks completed for the SDK output: 1,165 regressions, 50 nOCR compatibility cases, 37 main-menu checks, four Russian dialogs/full-screen lifecycle, and native media/package smoke checks. See [the fixed-scope report](UPSTREAM_5_4_STATUS.md) for the verification boundaries.
