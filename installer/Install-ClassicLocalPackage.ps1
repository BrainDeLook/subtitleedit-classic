param(
    [Parameter(Mandatory=$true)][string]$ManifestPath
)
$ErrorActionPreference='Stop'
$manifest=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$installRoot=[IO.Path]::GetFullPath($manifest.installDirectory).TrimEnd('\')
$candidateRoot=[IO.Path]::GetFullPath($manifest.candidateDirectory).TrimEnd('\')
$backupRoot=[IO.Path]::GetFullPath($manifest.backupDirectory).TrimEnd('\')
$status=$manifest.statusPath
$changed=[Collections.Generic.List[object]]::new()
function Target([string]$root,[string]$relative) {
    if([IO.Path]::IsPathRooted($relative)){throw 'Absolute package entry rejected.'}
    $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(-not $path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Package entry leaves its intended folder.'}
    return $path
}
try {
    $exe=Join-Path $installRoot 'SubtitleEdit.exe'
    if(Get-Process -Name SubtitleEdit -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $exe}){throw 'Save and close Subtitle Edit Classic before installing this package.'}
    foreach($item in $manifest.files){
        $source=Target $candidateRoot $item.path;$target=Target $installRoot $item.path
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $item.sha256){throw "Candidate changed: $($item.path)"}
        if($item.oldSha256){if(-not(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $item.oldSha256){throw "Installed file changed: $($item.path)"}}
        elseif(Test-Path -LiteralPath $target){throw "A new target file appeared: $($item.path)"}
    }
    $settingsHash=if(Test-Path -LiteralPath $manifest.settingsPath){(Get-FileHash -LiteralPath $manifest.settingsPath -Algorithm SHA256).Hash}else{$null}
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    foreach($item in $manifest.files){
        $source=Target $candidateRoot $item.path;$target=Target $installRoot $item.path;$backup=Target $backupRoot $item.path
        if($item.oldSha256){
            New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
            if(Test-Path -LiteralPath $backup){if((Get-FileHash -LiteralPath $backup).Hash -ne $item.oldSha256){throw "Backup differs: $($item.path)"}}
            else{Copy-Item -LiteralPath $target -Destination $backup}
        }
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        $staged=$target+'.classic-package.tmp'
        try {
            Copy-Item -LiteralPath $source -Destination $staged -Force
            if((Get-FileHash -LiteralPath $staged).Hash -ne $item.sha256){throw 'Staged file checksum differs.'}
            $changed.Add($item)
            if(Test-Path -LiteralPath $target){[IO.File]::Replace($staged,$target,[System.Management.Automation.Language.NullString]::Value)}else{[IO.File]::Move($staged,$target)}
        } finally {if(Test-Path -LiteralPath $staged){Remove-Item -LiteralPath $staged -Force}}
    }
    foreach($item in $manifest.files){if((Get-FileHash -LiteralPath (Target $installRoot $item.path)).Hash -ne $item.sha256){throw "Installed checksum differs: $($item.path)"}}
    if((Get-Item -LiteralPath $exe).VersionInfo.FileVersion.Trim() -ne $manifest.version){throw 'Installed version differs.'}
    $after=if(Test-Path -LiteralPath $manifest.settingsPath){(Get-FileHash -LiteralPath $manifest.settingsPath).Hash}else{$null}
    if($settingsHash -ne $after){throw 'Settings changed during the installation; rolling back the application package.'}
    @{success=$true;version=$manifest.version;sha256=(Get-FileHash -LiteralPath $exe).Hash;files=$manifest.files.Count;backup=$backupRoot;settingsUnchanged=$true} | ConvertTo-Json | Set-Content -LiteralPath $status -Encoding utf8
} catch {
    $errorText=$_.Exception.Message;$rollbackErrors=[Collections.Generic.List[string]]::new()
    for($i=$changed.Count-1;$i -ge 0;$i--){
        $item=$changed[$i]
        try {
            $target=Target $installRoot $item.path
            if($item.oldSha256){Copy-Item -LiteralPath (Target $backupRoot $item.path) -Destination $target -Force}
            elseif(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Force}
        } catch {$rollbackErrors.Add($_.Exception.Message)}
    }
    @{success=$false;error=$errorText;rollbackErrors=$rollbackErrors.ToArray()} | ConvertTo-Json | Set-Content -LiteralPath $status -Encoding utf8
    exit 1
}
