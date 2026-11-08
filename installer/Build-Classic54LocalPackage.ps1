param(
    [Parameter(Mandatory = $true)][string]$ApplicationDirectory,
    [Parameter(Mandatory = $true)][string]$ConsoleExecutable,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$appRoot = (Resolve-Path -LiteralPath $ApplicationDirectory).Path
$cli = (Resolve-Path -LiteralPath $ConsoleExecutable).Path
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Choose a new output directory; existing packages are not overwritten.' }
$filesRoot = Join-Path $destination 'files'
New-Item -ItemType Directory -Path $filesRoot -Force | Out-Null
$applicationFiles = @('SubtitleEdit.exe', 'SubtitleEdit.exe.config', 'libse.dll', 'System.Resources.Extensions.dll', 'System.Formats.Nrbf.dll', 'System.Collections.Immutable.dll', 'System.Reflection.Metadata.dll', 'Microsoft.Bcl.HashCode.dll')
foreach ($name in $applicationFiles) { Copy-Item -LiteralPath (Join-Path $appRoot $name) -Destination $filesRoot }
Copy-Item -LiteralPath $cli -Destination (Join-Path $filesRoot 'seconv.exe')
Copy-Item -LiteralPath ($cli + '.config') -Destination (Join-Path $filesRoot 'seconv.exe.config')
$version = (Get-Item -LiteralPath (Join-Path $filesRoot 'SubtitleEdit.exe')).VersionInfo.FileVersion.Trim()
$records = foreach ($name in ($applicationFiles + @('seconv.exe', 'seconv.exe.config'))) {
    [ordered]@{ path = $name; sha256 = (Get-FileHash -LiteralPath (Join-Path $filesRoot $name) -Algorithm SHA256).Hash }
}
[ordered]@{
    version = $version
    minimumVersion = '4.0.28.0'
    upstreamReference = 'v5.4.0 / 57273b2'
    kind = 'unpublished-local-patch'
    requiredFiles = @('Newtonsoft.Json.dll', 'SkiaSharp.dll', 'SkiaSharp.HarfBuzz.dll', 'HarfBuzzSharp.dll', 'NAudio.Core.dll', 'NAudio.WinMM.dll', 'System.Memory.dll', 'x64/libSkiaSharp.dll', 'x64/libHarfBuzzSharp.dll')
    files = @($records)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'package.json') -Encoding UTF8
foreach ($name in @('Install-Classic54.ps1', 'Install-ClassicLocalPackage.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $destination }
$repoRoot = Split-Path $PSScriptRoot -Parent
$packageDocs = Join-Path $destination 'docs'
New-Item -ItemType Directory -Path $packageDocs -Force | Out-Null
foreach ($name in @('UPSTREAM_5_4_STATUS.md', 'UPSTREAM_5_4_AUDIT.md', 'UPSTREAM_5_4_AUDIT_ITEMS.json', 'CLASSIC_54_COMPLETION.md', 'CLASSIC_54_LOCALIZATION.md', 'CLASSIC_54_LOCALIZATION_COVERAGE.json', 'CLASSIC_54_CHECK.ru.md', 'BUILD_CLASSIC.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot ('docs/' + $name)) -Destination $destination
    Copy-Item -LiteralPath (Join-Path $repoRoot ('docs/' + $name)) -Destination $packageDocs
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'CLASSIC_CHANGELOG.md') -Destination $destination
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE.txt') -Destination $destination
Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses/classic-rendering') -Destination (Join-Path $destination 'licenses') -Recurse
$sums = Get-ChildItem -LiteralPath $destination -File -Recurse | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.FullName.Substring($destination.Length + 1).Replace('\', '/')
}
$sums | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS') -Encoding ASCII
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath ($destination + '.zip')
Get-FileHash -LiteralPath ($destination + '.zip') -Algorithm SHA256 | Format-List
