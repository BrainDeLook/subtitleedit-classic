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
    TestClassicCastFeatures='tts-tests/TestClassicCastFeatures.cs'
    TestClassicLocalTts='tts-tests/TestClassicLocalTts.cs'
    TestClassicSpeechExtensions='speech-tests/TestClassicSpeechExtensions.cs'
    TestClassicNext='tts-tests/TestClassicNext.cs'
    TestModelessSpeech='tts-tests/TestModelessSpeech.cs'
    TestSpeechPlayer='tts-tests/TestSpeechPlayer.cs'
    TestExtendedTts='tts-tests/TestExtendedTts.cs'
    TestFinalTts='tts-tests/TestFinalTts.cs'
    TestSpeechSession='tts-tests/TestSpeechSession.cs'
    TestStableNOcr='video-ocr-tests/TestStableNOcr.cs'
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
    & "$OutputDirectory/TestClassicCastFeatures.exe" unit (Join-Path $OutputDirectory 'cast-features') $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw 'Cast/voice library/layout regression failed.' }
    & "$OutputDirectory/TestClassicLocalTts.exe" unit (Join-Path $OutputDirectory 'local-tts-features') $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw 'Local TTS model/settings/layout regression failed.' }
    & "$OutputDirectory/TestClassicSpeechExtensions.exe" unit (Join-Path $OutputDirectory 'speech-extensions') $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw 'ASR extensions/settings regression failed.' }
    & "$OutputDirectory/TestClassicSpeechExtensions.exe" wave (Join-Path $OutputDirectory 'timing-waveform') $FFmpegLocation (Join-Path $PSScriptRoot 'speech-tests/english-sample.wav')
    if ($LASTEXITCODE -ne 0) { throw 'Timing waveform interaction regression failed.' }
}

& "$OutputDirectory/TestStableNOcr.exe"
if($LASTEXITCODE -ne 0){throw 'Stable nOCR matcher/trainer regression failed.'}
if($FFmpegLocation) {
    & "$OutputDirectory/TestClassicNext.exe" unit (Join-Path $OutputDirectory 'next-tts-ui') $FFmpegLocation
    if($LASTEXITCODE -ne 0){throw 'audio.cpp catalog/protocol/layout regression failed.'}
    & "$OutputDirectory/TestClassicNext.exe" playback (Join-Path $OutputDirectory 'next-playback') $FFmpegLocation
    if($LASTEXITCODE -ne 0){throw 'Speech playback regression failed.'}
    & "$OutputDirectory/TestClassicNext.exe" nocr (Join-Path $OutputDirectory 'next-nocr') $FFmpegLocation (Join-Path $PSScriptRoot '../Ocr/Cyrillic.nocr')
    if($LASTEXITCODE -ne 0){throw 'Classic nOCR Russian/English font training regression failed.'}
}

& "$OutputDirectory/TestModelessSpeech.exe" (Join-Path $OutputDirectory "modeless-speech")
if ($LASTEXITCODE -ne 0) { throw "Modeless speech window regression failed." }

if ($FFmpegLocation) {
    & "$OutputDirectory/TestSpeechPlayer.exe" (Join-Path $OutputDirectory "native-speech-player") (Join-Path $PSScriptRoot "speech-tests/english-sample.wav")
    if ($LASTEXITCODE -ne 0) { throw "Native MPV speech playback regression failed." }
}

if ($FFmpegLocation) {
    & "$OutputDirectory/TestExtendedTts.exe" unit (Join-Path $OutputDirectory "extended-tts") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Extended TTS protocol/settings regression failed." }
}

if ($FFmpegLocation) {
    & "$OutputDirectory/TestFinalTts.exe" unit (Join-Path $OutputDirectory "final-tts") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Complete stable TTS catalog, cloud authentication and classic UI regression failed." }
}

if ($FFmpegLocation) {
    & "$OutputDirectory/TestSpeechSession.exe" (Join-Path $OutputDirectory "speech-session") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Portable speech sessions, history and restored generation regression failed." }
}
