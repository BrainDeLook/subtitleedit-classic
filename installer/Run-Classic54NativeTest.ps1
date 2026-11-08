param(
    [Parameter(Mandatory = $true)][string[]]$Source,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Name = 'Classic54NativeTest',
    [string[]]$TestArguments = @()
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$buildRoot = Join-Path $repoRoot 'src/seconv/bin/Release/net48'
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
Get-ChildItem -LiteralPath $buildRoot -File | Where-Object { $_.Extension -in @('.exe', '.dll', '.config') } | Copy-Item -Destination $output -Force
foreach ($folder in @('Languages', 'x64')) {
    Copy-Item -LiteralPath (Join-Path $buildRoot $folder) -Destination $output -Recurse -Force
}
$referencePack = Join-Path $env:USERPROFILE '.nuget/packages/microsoft.netframework.referenceassemblies.net48/1.0.3/build/.NETFramework/v4.8'
if (-not (Test-Path -LiteralPath $referencePack)) { throw 'Restore the net48 source project before compiling tests.' }
$references = @{}
foreach ($file in @((Get-ChildItem -LiteralPath $referencePack -File -Filter '*.dll')) + @((Get-ChildItem -LiteralPath $output -File -Filter '*.dll'))) {
    # Both directories also contain native binaries; classify each file once at the compiler boundary.
    try { $null = [Reflection.AssemblyName]::GetAssemblyName($file.FullName); $references[$file.Name] = $file.FullName }
    catch [BadImageFormatException] { }
}
$references['SubtitleEdit.exe'] = Join-Path $output 'SubtitleEdit.exe'
$sdk = (& dotnet --list-sdks | Select-Object -Last 1)
if ($sdk -notmatch '^([^ ]+) \[(.+)\]$') { throw 'Could not locate the .NET SDK C# compiler.' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn/bincore/csc.dll'
$response = @('/nologo', '/nostdlib+', '/target:exe', '/platform:x64', '/langversion:latest', '/unsafe', ('/out:"' + (Join-Path $output ($Name + '.exe')) + '"'))
$response += $references.Values | ForEach-Object { '/reference:"' + $_ + '"' }
$response += $Source | ForEach-Object { '"' + (Resolve-Path -LiteralPath $_).Path + '"' }
$responseFile = Join-Path $output ($Name + '.rsp')
[IO.File]::WriteAllLines($responseFile, $response)
& dotnet $compiler /noconfig ('@' + $responseFile)
if ($LASTEXITCODE -ne 0) { throw 'Native test compilation failed.' }
Copy-Item -LiteralPath (Join-Path $output 'SubtitleEdit.exe.config') -Destination (Join-Path $output ($Name + '.exe.config')) -Force
Push-Location -LiteralPath $output
try {
    & (Join-Path $output ($Name + '.exe')) @TestArguments
    if ($LASTEXITCODE -ne 0) { throw 'Native test failed.' }
} finally { Pop-Location }
