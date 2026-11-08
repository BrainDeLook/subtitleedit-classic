param(
    [string]$InstallDirectory = (Join-Path $env:ProgramFiles 'Subtitle Edit Classic'),
    [string]$BackupDirectory
)
$ErrorActionPreference = 'Stop'
$packageRoot = $PSScriptRoot
$package = Get-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Raw | ConvertFrom-Json
$installRoot = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
$application = Join-Path $installRoot 'SubtitleEdit.exe'
if (-not (Test-Path -LiteralPath $application)) { throw 'Install Subtitle Edit Classic 4.0.28 first, or pass its existing folder with -InstallDirectory.' }
$installedVersion = [version](Get-Item -LiteralPath $application).VersionInfo.FileVersion.Trim()
if ($installedVersion -lt [version]$package.minimumVersion -or $installedVersion -gt [version]$package.version) {
    throw "This local package supports Classic $($package.minimumVersion) through $($package.version); installed: $installedVersion."
}
foreach ($dependency in $package.requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $installRoot $dependency))) { throw "Classic runtime is incomplete: $dependency" }
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if (-not $BackupDirectory) { $BackupDirectory = Join-Path $packageRoot ('backup-' + $stamp + '-' + [guid]::NewGuid().ToString('N')) }
$backupRoot = [IO.Path]::GetFullPath($BackupDirectory).TrimEnd('\')
if ($backupRoot -eq $installRoot -or $backupRoot.StartsWith($installRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose a backup folder outside the application folder.'
}
$records = foreach ($entry in $package.files) {
    $target = Join-Path $installRoot $entry.path
    [ordered]@{
        path = $entry.path
        sha256 = $entry.sha256
        oldSha256 = if (Test-Path -LiteralPath $target) { (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash } else { $null }
    }
}
$manifestPath = Join-Path $packageRoot ('installation-' + $stamp + '-' + [guid]::NewGuid().ToString('N') + '.json')
$statusPath = $manifestPath + '.result.json'
[ordered]@{
    version = $package.version
    installDirectory = $installRoot
    candidateDirectory = Join-Path $packageRoot 'files'
    backupDirectory = $backupRoot
    statusPath = $statusPath
    settingsPath = if (Test-Path -LiteralPath (Join-Path $installRoot 'Settings.xml')) { Join-Path $installRoot 'Settings.xml' } else { Join-Path $env:APPDATA 'Subtitle Edit/Settings.xml' }
    files = @($records)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
& (Join-Path $packageRoot 'Install-ClassicLocalPackage.ps1') -ManifestPath $manifestPath
$result = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
if (-not $result.success) { throw "Installation failed: $($result.error). See $statusPath" }
Write-Output "Installed local Classic $($result.version). Backup: $backupRoot"
Write-Output 'Settings, dictionaries and downloaded models were retained. Start SubtitleEdit.exe normally.'
