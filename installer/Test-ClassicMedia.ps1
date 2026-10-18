param(
    [Parameter(Mandatory=$true)][string]$ApplicationDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$FFmpegLocation,
    [string]$AudioSelectionSample,
    [switch]$Unused
)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory=(Resolve-Path $OutputDirectory).Path
$ApplicationDirectory=(Resolve-Path $ApplicationDirectory).Path
Copy-Item "$ApplicationDirectory/SubtitleEdit.exe", "$ApplicationDirectory/SubtitleEdit.exe.config", "$ApplicationDirectory/*.dll" $OutputDirectory -Force
Copy-Item "$ApplicationDirectory/Languages" $OutputDirectory -Recurse -Force
foreach ($architecture in @('x64','x86')) {
    if (Test-Path -LiteralPath (Join-Path $ApplicationDirectory $architecture)) {
        Copy-Item (Join-Path $ApplicationDirectory $architecture) $OutputDirectory -Recurse -Force
    }
}

# MPV is an on-demand application dependency; fresh CI installs do not contain it yet.
if (-not (Test-Path -LiteralPath (Join-Path $OutputDirectory 'libmpv-2.dll'))) {
    $mpvArchive = Join-Path $OutputDirectory 'mpv-test-runtime.zip'
    Invoke-WebRequest -Uri 'https://github.com/SubtitleEdit/support-files/releases/download/libmpv-2025-01-25/libmpv2-64.zip' -OutFile $mpvArchive
    if ((Get-FileHash -LiteralPath $mpvArchive -Algorithm SHA256).Hash -ne 'A735885ECF5854FBE6B228AD6F78C10B944866228E91AAE5AB547243764C8DCA') { throw 'MPV test runtime checksum mismatch.' }
    Expand-Archive -LiteralPath $mpvArchive -DestinationPath $OutputDirectory -Force
    Remove-Item -LiteralPath $mpvArchive
}

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
    TestClassicTimingProfiles='TestClassicTimingProfiles.cs'
    TestClassicCompareComplete='TestClassicCompareComplete.cs'
    TestClassicCompareSync='TestClassicCompareSync.cs'
    TestClassicCompare='TestClassicCompare.cs'
    TestClassicBeautifyPreview='TestClassicBeautifyPreview.cs'
    TestClassicCommandPalette='TestClassicCommandPalette.cs'
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
    TestSpeechWaveform='tts-tests/TestSpeechWaveform.cs'
    TestSelectedSpeech='speech-tests/TestSelectedSpeech.cs'
    TestAudioTrackLabels='TestAudioTrackLabels.cs'
    TestClassicAudioSelection='TestClassicAudioSelection.cs'
    TestTtsModelExtras='tts-tests/TestTtsModelExtras.cs'
    TestVoiceReferenceTools='tts-tests/TestVoiceReferenceTools.cs'
    TestWaveformReference='tts-tests/TestWaveformReference.cs'
    TestPerLineCloning='tts-tests/TestPerLineCloning.cs'
    TestCompatibleAsr='speech-tests/TestCompatibleAsr.cs'
    TestLocalAsrGrowth='speech-tests/TestLocalAsrGrowth.cs'
    TestPriorityIntegration='speech-tests/TestPriorityIntegration.cs'
    TestClassicCompletion='speech-tests/TestClassicCompletion.cs'
    TestAdvancedBatchTranslation='speech-tests/TestAdvancedBatchTranslation.cs'
    TestAiReviewPasses='speech-tests/TestAiReviewPasses.cs'
    TestClassicUpdatesAndTraining='video-ocr-tests/TestClassicUpdatesAndTraining.cs'
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
    & "$OutputDirectory/TestSpeechWaveform.exe" (Join-Path $OutputDirectory "speech-review-waveform") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Speech review waveform, timing, seek and session regression failed." }
    & "$OutputDirectory/TestSelectedSpeech.exe" unit (Join-Path $OutputDirectory "selected-speech") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Selected ASR, partial results, retry and classic layout regression failed." }
    & "$OutputDirectory/TestAudioTrackLabels.exe" (Join-Path $OutputDirectory "audio-track-labels") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Burn-in audio language/title selection and stereo export regression failed." }
    & "$OutputDirectory/TestTtsModelExtras.exe" unit (Join-Path $OutputDirectory "tts-model-extras") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "ElevenLabs protocol/pauses and custom Piper import regression failed." }
    & "$OutputDirectory/TestVoiceReferenceTools.exe" (Join-Path $OutputDirectory "voice-reference-tools") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Voice reference waveform, playback and ASR range regression failed." }
    & "$OutputDirectory/TestWaveformReference.exe" (Join-Path $OutputDirectory "waveform-reference") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Waveform voice reference selection, track mapping and import regression failed." }
    & "$OutputDirectory/TestPerLineCloning.exe" unit (Join-Path $OutputDirectory "per-line-cloning") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Audio-only per-line cloning, reference padding and session-copy regression failed." }
    & "$OutputDirectory/TestCompatibleAsr.exe" (Join-Path $OutputDirectory "compatible-asr") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Compatible ASR multipart, timestamp, fallback, local HTTP and classic settings regression failed." }
    & "$OutputDirectory/TestLocalAsrGrowth.exe" unit (Join-Path $OutputDirectory "local-asr-growth") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Local ASR catalog, multilingual language selection and anime model regression failed." }
    & "$OutputDirectory/TestPriorityIntegration.exe" unit (Join-Path $OutputDirectory "priority-integrations") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Cloud ASR, SSE, AI review, translation, bitmap OCR and classic settings regression failed." }
    & "$OutputDirectory/TestClassicCompletion.exe" unit (Join-Path $OutputDirectory "classic-completion") $FFmpegLocation
    if ($LASTEXITCODE -ne 0) { throw "Local AI, translation, context and ASR recovery regression failed." }
    & "$OutputDirectory/TestAdvancedBatchTranslation.exe" unit (Join-Path $OutputDirectory "advanced-batch-translation")
    if ($LASTEXITCODE -ne 0) { throw "Advanced translation, batch file output, cancellation and classic layout regression failed." }
    & "$OutputDirectory/TestClassicCommandPalette.exe" (Join-Path $OutputDirectory "classic-command-palette")
    if($LASTEXITCODE -ne 0){throw "Classic command palette regression failed"}
    & "$OutputDirectory/TestClassicCompareComplete.exe" (Join-Path $OutputDirectory "classic-compare-complete")
    if($LASTEXITCODE -ne 0){throw "Complete classic comparison workflow regression failed"}
    & "$OutputDirectory/TestClassicCompareSync.exe" (Join-Path $OutputDirectory "classic-compare-sync")
    if($LASTEXITCODE -ne 0){throw "Classic comparison sync and alignment regression failed"}
    & "$OutputDirectory/TestClassicCompare.exe" (Join-Path $OutputDirectory "classic-compare")
    if($LASTEXITCODE -ne 0){throw "Classic comparison merge regression failed"}
    & "$OutputDirectory/TestClassicBeautifyPreview.exe" (Join-Path $OutputDirectory "classic-beautify-preview") $FFmpegLocation
    if($LASTEXITCODE -ne 0){throw "Classic beautify live preview regression failed"}
    & "$OutputDirectory/TestClassicTimingProfiles.exe" (Join-Path $OutputDirectory "classic-timing-profiles")
    if($LASTEXITCODE -ne 0){throw "Classic named timing profiles regression failed"}
    & "$OutputDirectory/TestClassicUpdatesAndTraining.exe" unit (Join-Path $OutputDirectory "classic-updates-training")
    if($LASTEXITCODE -ne 0){throw "Classic updates and nOCR training failed"}
    & "$OutputDirectory/TestAiReviewPasses.exe" unit (Join-Path $OutputDirectory "ai-review-passes")
    if ($LASTEXITCODE -ne 0) { throw "AI review filters, warnings, apply passes, main undo and cancellation regression failed." }
    if ($AudioSelectionSample) {
        $env:CLASSIC_AUDIO_TEST_DATA=Join-Path $OutputDirectory 'editor-audio-selection'
        $env:CLASSIC_AUDIO_TEST_SOURCE=(Resolve-Path -LiteralPath $AudioSelectionSample).Path
        $env:CLASSIC_AUDIO_TEST_FFMPEG=$FFmpegLocation
        & "$OutputDirectory/TestClassicAudioSelection.exe"
        if ($LASTEXITCODE -ne 0) { throw "Native editor audio selection regression failed." }
    }
}
