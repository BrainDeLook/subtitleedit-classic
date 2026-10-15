param(
    [Parameter(Mandatory=$true)][string]$ApplicationDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$DownloadSmoke
)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory=(Resolve-Path $OutputDirectory).Path
$ApplicationDirectory=(Resolve-Path $ApplicationDirectory).Path
Copy-Item "$ApplicationDirectory/SubtitleEdit.exe", "$ApplicationDirectory/SubtitleEdit.exe.config", "$ApplicationDirectory/*.dll" $OutputDirectory -Force
Copy-Item "$ApplicationDirectory/Languages" $OutputDirectory -Recurse -Force
Copy-Item "$PSScriptRoot/english-sample.wav" $OutputDirectory -Force
$sdk=Get-ChildItem 'C:/Program Files/dotnet/sdk' -Directory | Sort-Object { [version]$_.Name } | Select-Object -Last 1
$framework="$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319"
$refs=@('/reference:"'+(Join-Path $OutputDirectory 'SubtitleEdit.exe')+'"')
foreach($name in @('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Data','System.Xml','System.Runtime.Serialization')) {
    $refs+='/reference:"'+(Join-Path $framework "$name.dll")+'"'
}
foreach($dll in Get-ChildItem $OutputDirectory -Filter '*.dll') {
    try { [Reflection.AssemblyName]::GetAssemblyName($dll.FullName) | Out-Null; $refs+='/reference:"'+$dll.FullName+'"' } catch [BadImageFormatException] {}
}
foreach($name in @('TestClassicSpeech','MockSpeechEngine','TestComboScroll')) {
    $compilerArguments=@('/nologo','/target:exe','/platform:x64','/langversion:latest')
    $compilerArguments+=('/out:"'+(Join-Path $OutputDirectory "$name.exe")+'"')
    $compilerArguments+=$refs
    $compilerArguments+=('"'+(Join-Path $PSScriptRoot "$name.cs")+'"')
    $response=Join-Path $OutputDirectory "$name.rsp"
    [IO.File]::WriteAllLines($response,$compilerArguments)
    & dotnet "$($sdk.FullName)/Roslyn/bincore/csc.dll" "@$response"
    if($LASTEXITCODE -ne 0){throw "$name compilation failed"}
    Copy-Item "$OutputDirectory/SubtitleEdit.exe.config" "$OutputDirectory/$name.exe.config" -Force
}
foreach($mode in @('unit','ui')) {
    & "$OutputDirectory/TestClassicSpeech.exe" $mode (Join-Path $OutputDirectory "results-$mode")
    if($LASTEXITCODE -ne 0){throw "Speech $mode tests failed"}
}
& "$OutputDirectory/TestComboScroll.exe"
if($LASTEXITCODE -ne 0){throw 'Model-list scrolling tests failed'}
if($DownloadSmoke) {
    & "$OutputDirectory/TestClassicSpeech.exe" clean-install (Join-Path $OutputDirectory 'results-download')
    if($LASTEXITCODE -ne 0){throw 'Clean engine/model download and transcription failed'}
}
