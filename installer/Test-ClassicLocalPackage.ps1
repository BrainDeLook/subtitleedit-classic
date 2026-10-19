param([Parameter(Mandatory=$true)][string]$ApplicationDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$root=(Resolve-Path $OutputDirectory).Path
$candidate=Join-Path $root 'candidate';New-Item -ItemType Directory -Path $candidate -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $ApplicationDirectory 'SubtitleEdit.exe') -Destination $candidate -Force
Set-Content -LiteralPath (Join-Path $candidate 'existing.txt') -Value 'new'
Set-Content -LiteralPath (Join-Path $candidate 'added.txt') -Value 'new file'
$checks=0
foreach($mode in @('install','rollback','reject-traversal')){
    $base=Join-Path $root $mode;$install=Join-Path $base 'app';$backup=Join-Path $base 'backup';New-Item -ItemType Directory -Path $install -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $candidate 'SubtitleEdit.exe') -Destination $install
    Set-Content -LiteralPath (Join-Path $install 'existing.txt') -Value 'old'
    $settings=Join-Path $base 'Settings.xml';Set-Content -LiteralPath $settings -Value 'settings retained'
    $old=(Get-FileHash -LiteralPath (Join-Path $install 'existing.txt')).Hash
    $records=foreach($name in @('SubtitleEdit.exe','existing.txt','added.txt')){[ordered]@{path=$name;sha256=(Get-FileHash -LiteralPath (Join-Path $candidate $name)).Hash;oldSha256=if(Test-Path -LiteralPath (Join-Path $install $name)){(Get-FileHash -LiteralPath (Join-Path $install $name)).Hash}else{$null}}}
    if($mode -eq 'reject-traversal'){$records[0].path='..\escaped.txt'}
    $status=Join-Path $base 'status.json';$manifest=Join-Path $base 'manifest.json'
    @{version=if($mode -eq 'rollback'){'0.0.0.0'}else{(Get-Item (Join-Path $candidate 'SubtitleEdit.exe')).VersionInfo.FileVersion.Trim()};candidateDirectory=$candidate;installDirectory=$install;backupDirectory=$backup;statusPath=$status;settingsPath=$settings;files=@($records)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding utf8
    $process=Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+(Join-Path $PSScriptRoot 'Install-ClassicLocalPackage.ps1')+'"'),'-ManifestPath',('"'+$manifest+'"')) -WindowStyle Hidden -PassThru -Wait
    $result=Get-Content -LiteralPath $status -Raw | ConvertFrom-Json
    if($mode -eq 'install'){
        if(-not $result.success -or (Get-Content -LiteralPath (Join-Path $install 'existing.txt') -Raw).Trim() -ne 'new' -or -not(Test-Path -LiteralPath (Join-Path $install 'added.txt'))){throw 'Package installation fixture failed.'}
        if((Get-FileHash -LiteralPath (Join-Path $backup 'existing.txt')).Hash -ne $old){throw 'Backup checksum differs.'}
    }else{
        if($result.success -or (Get-FileHash -LiteralPath (Join-Path $install 'existing.txt')).Hash -ne $old -or (Test-Path -LiteralPath (Join-Path $install 'added.txt'))){throw "Package $mode did not retain/restore the original files."}
        if($mode -eq 'rollback' -and $result.rollbackErrors.Count -ne 0){throw 'Rollback reported errors.'}
    }
    if((Get-Content -LiteralPath $settings -Raw).Trim() -ne 'settings retained'){throw 'Package altered settings.'}
    $checks++;Write-Output "PASS: local package $mode, backup and unchanged settings"
}
Write-Output "PACKAGE CHECKS: $checks"
