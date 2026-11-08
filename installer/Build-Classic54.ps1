param(
    [ValidateRange(0, 65534)][int]$Revision = 0,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'src/seconv/seconv.csproj'
if (-not $NoRestore) {
    & dotnet restore $project
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restoration failed.' }
}
& dotnet build $project --no-restore -c $Configuration -m:1 -nr:false -p:BuildInParallel=false ('-p:ClassicRevision=' + $Revision)
if ($LASTEXITCODE -ne 0) { throw 'Classic compilation failed.' }
$output = Join-Path $repoRoot ('src/seconv/bin/' + $Configuration + '/net48')
foreach ($name in @('SubtitleEdit.exe', 'libse.dll', 'seconv.exe')) {
    $actual = (Get-Item -LiteralPath (Join-Path $output $name)).VersionInfo.FileVersion.Trim()
    if ($actual -ne ('4.0.29.' + $Revision)) { throw "Unexpected version for ${name}: $actual" }
}
Write-Output "Classic UI, core and CLI built from source: $output"
