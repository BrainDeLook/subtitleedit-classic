param(
    [Parameter(Mandatory=$true)][string]$ApplicationDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$FFmpegLocation,
    [switch]$Unused
)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory=(Resolve-Path $OutputDirectory).Path
$ApplicationDirectory=(Resolve-Path $ApplicationDirectory).Path
Copy-Item "$ApplicationDirectory/SubtitleEdit.exe", "$ApplicationDirectory/SubtitleEdit.exe.config", "$ApplicationDirectory/*.dll" $OutputDirectory -Force
Copy-Item "$ApplicationDirectory/Languages" $OutputDirectory -Recurse -Force

$sdk=Get-ChildItem 'C:/Program Files/dotnet/sdk' -Directory | Sort-Object { [version]$_.Name } | Select-Object -Last 1
$framework="$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319"
$refs=@('/reference:"'+(Join-Path $OutputDirectory 'SubtitleEdit.exe')+'"')
foreach($name in @('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Data','System.Xml','System.Xml.Linq','System.Runtime.Serialization','System.Net.Http')) {
 $refs+='/reference:"'+(Join-Path $framework "$name.dll")+'"'
}
foreach($dll in Get-ChildItem $OutputDirectory -Filter '*.dll') {
 try{[Reflection.AssemblyName]::GetAssemblyName($dll.FullName)|Out-Null;$refs+='/reference:"'+$dll.FullName+'"'}catch [BadImageFormatException]{}
}
$tests=@{
    TestClassicVideoOcr='video-ocr-tests/TestClassicVideoOcr.cs'
    TestClassicTts='tts-tests/TestClassicTts.cs'
    TestClassicBurnInCanvas='TestClassicBurnInCanvas.cs'
    TestClassic53Tts='tts-tests/TestClassic53Tts.cs'
    TestClassic53Ocr='video-ocr-tests/TestClassic53Ocr.cs'
    TestClassicTiming='TestClassicTiming.cs'
    TestClassicNOcr='video-ocr-tests/TestClassicNOcr.cs'
    TestClassicActors='tts-tests/TestClassicActors.cs'
}
foreach($name in $tests.Keys) {
    $arguments=@('/nologo','/target:exe','/platform:x64','/langversion:latest',('/out:"'+(Join-Path $OutputDirectory "$name.exe")+'"'))+$refs+('"'+(Join-Path $PSScriptRoot $tests[$name])+'"')
    $response=Join-Path $OutputDirectory "$name.rsp"
    [IO.File]::WriteAllLines($response,$arguments)
    & dotnet "$($sdk.FullName)/Roslyn/bincore/csc.dll" "@$response"
    if($LASTEXITCODE -ne 0){throw "$name compilation failed"}
    Copy-Item "$OutputDirectory/SubtitleEdit.exe.config" "$OutputDirectory/$name.exe.config" -Force
}
foreach($mode in @('unit','ui')) {
    & "$OutputDirectory/TestClassicVideoOcr.exe" $mode (Join-Path $OutputDirectory "ocr-$mode")
    if($LASTEXITCODE -ne 0){throw "Video OCR $mode failed"}
}
foreach($mode in @('ui','export')) {
    & "$OutputDirectory/TestClassicTts.exe" $mode (Join-Path $OutputDirectory "tts-$mode")
    if($LASTEXITCODE -ne 0){throw "TTS $mode failed"}
}
& "$OutputDirectory/TestClassicBurnInCanvas.exe"
if($LASTEXITCODE -ne 0){throw 'ASS canvas regression failed'}
foreach($name in @('TestClassic53Tts','TestClassic53Ocr','TestClassicTiming')) {
    & "$OutputDirectory/$name.exe" unit (Join-Path $OutputDirectory "$name-unit")
    if($LASTEXITCODE -ne 0){throw "$name regression failed"}
}
& "$OutputDirectory/TestClassicTiming.exe" ui (Join-Path $OutputDirectory 'timing-ui')
if($LASTEXITCODE -ne 0){throw 'Timing UI regression failed'}
& "$OutputDirectory/TestClassicNOcr.exe" (Join-Path $PSScriptRoot '../Ocr/Cyrillic.nocr')
if($LASTEXITCODE -ne 0){throw 'nOCR database/optimizer regression failed'}


if ($FFmpegLocation) {
    if (-not (Test-Path -LiteralPath $FFmpegLocation)) { throw 'FFmpeg executable was not found.' }
    & "$OutputDirectory/TestClassicActors.exe" (Join-Path $OutputDirectory 'actor-dubbing') $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw 'Actor selection, removal scope and audio preservation regression failed.' }
}
