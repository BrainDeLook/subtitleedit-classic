param(
    [Parameter(Mandatory=$true)][string]$ApplicationDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$FFmpegLocation
)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory=(Resolve-Path $OutputDirectory).Path
$repository=Split-Path $PSScriptRoot -Parent
$sdk=Get-ChildItem 'C:/Program Files/dotnet/sdk' -Directory | Sort-Object {[version]$_.Name} | Select-Object -Last 1
$compiler=Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll'
$framework="$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319"
$references=@('mscorlib','System','System.Core','System.Numerics','System.Drawing','System.Windows.Forms','System.Data','System.Xml','System.Xml.Linq','System.Runtime.Serialization','System.Configuration','System.Web','System.Web.Services','System.Management','System.IO.Compression','System.IO.Compression.FileSystem','System.Net.Http','Microsoft.CSharp','System.Speech','System.Security','netstandard','System.Runtime') | ForEach-Object {
    $reference=Join-Path $framework ($_+'.dll')
    if (!(Test-Path $reference)) { $reference=Get-ChildItem "$env:WINDIR/Microsoft.NET/assembly/GAC_MSIL/$_" -Filter ($_.ToString()+'.dll') -Recurse -ErrorAction SilentlyContinue | Select-Object -Last 1 -ExpandProperty FullName }
    if ($reference) { $reference }
}
$references+=Join-Path $ApplicationDirectory 'SubtitleEdit.exe'
foreach($library in Get-ChildItem $ApplicationDirectory -Filter '*.dll') {
    try {[Reflection.AssemblyName]::GetAssemblyName($library.FullName) | Out-Null; $references+=$library.FullName} catch [BadImageFormatException] { }
}
function Compile-ReleaseTest([string]$name) {
    $target=Join-Path $ApplicationDirectory ($name+'.exe')
    $response=Join-Path $OutputDirectory ($name+'.rsp')
    $arguments=@('/nologo','/target:exe','/platform:x64','/langversion:latest',('/out:"'+$target+'"'))
    $arguments+=$references | ForEach-Object {'/reference:"'+$_+'"'}
    $arguments+='"'+(Join-Path $PSScriptRoot ($name+'.cs'))+'"'
    [IO.File]::WriteAllLines($response,$arguments)
    & dotnet $compiler "@$response" | ForEach-Object { Write-Host $_ }
    if($LASTEXITCODE -ne 0){throw "Compilation failed: $name"}
    Copy-Item (Join-Path $ApplicationDirectory 'SubtitleEdit.exe.config') ($target+'.config')
    return $target
}
function Run-ReleaseTest([string]$test,[string[]]$parameters) {
    & $test @parameters
    if($LASTEXITCODE -ne 0){throw "Release test failed: $test"}
}
$fixture=Join-Path $OutputDirectory 'source.mkv'
& $FFmpegLocation -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=320x180:rate=25' -f lavfi -i 'sine=frequency=400:sample_rate=48000' -f lavfi -i 'sine=frequency=800:sample_rate=48000' -map 0:v -map 1:a -map 2:a -metadata:s:a:0 language=tur -metadata:s:a:1 language=eng -c:v libx264 -preset ultrafast -ac 2 -c:a aac -t 4 -y $fixture
if($LASTEXITCODE -ne 0){throw 'Fixture generation failed'}
Run-ReleaseTest (Compile-ReleaseTest 'LocalNineTests') @((Join-Path $OutputDirectory 'native'),$FFmpegLocation,$fixture)
Run-ReleaseTest (Compile-ReleaseTest 'LocalNineBatchTests') @('unit',(Join-Path $OutputDirectory 'batch'))
Run-ReleaseTest (Compile-ReleaseTest 'TestClassicNavigation') @((Join-Path $OutputDirectory 'navigation'),(Join-Path $PSScriptRoot 'ClassicMenuBaseline.txt'))
Run-ReleaseTest (Compile-ReleaseTest 'TestShotMarkerVisibility') @((Join-Path $OutputDirectory 'markers'))
$audio=Join-Path $OutputDirectory 'source.wav'
& $FFmpegLocation -hide_banner -loglevel error -f lavfi -i 'sine=frequency=440:sample_rate=16000' -t 120 -ac 1 -c:a pcm_s16le -y $audio
if($LASTEXITCODE -ne 0){throw 'Audio fixture generation failed'}
Run-ReleaseTest (Compile-ReleaseTest 'TestSpectrogramViewport') @($audio,(Join-Path $OutputDirectory 'source.peak'))
& python (Join-Path $PSScriptRoot 'LocalNineCliTests.py') (Join-Path $ApplicationDirectory 'seconv.exe') (Join-Path $OutputDirectory 'cli')
if($LASTEXITCODE -ne 0){throw 'Installed seconv tests failed'}
Write-Output 'Classic 5.3 integration and navigation release checks passed; no paid API calls.'
