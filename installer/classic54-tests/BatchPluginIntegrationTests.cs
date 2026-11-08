using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.Plugins;
namespace Classic54Tests {
 public class BatchPluginIntegrationTests:IDisposable
 {
    private readonly string _folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"plugin-test-"+Guid.NewGuid().ToString("N"));
    private readonly string _settings;
    private readonly ClassicBatchPlugin _plugin;
    public BatchPluginIntegrationTests()
    {
        if(!Path.GetFullPath(Configuration.DataDirectory).StartsWith(AppDomain.CurrentDomain.BaseDirectory,StringComparison.OrdinalIgnoreCase))throw new Exception("Plugin tests require isolated data directory");
        Directory.CreateDirectory(_folder);File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"BatchPluginFixture.exe"),Path.Combine(_folder,"fixture.exe"));File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Newtonsoft.Json.dll"),Path.Combine(_folder,"Newtonsoft.Json.dll"));
        File.WriteAllText(Path.Combine(_folder,"plugin.json"),"{\"apiVersion\":1,\"batch\":true,\"name\":\"Fixture\",\"version\":\"1\",\"runtime\":\"native\",\"executables\":{\"windows\":\"fixture.exe\"}}");
        _plugin=ClassicBatchPlugin.Load(Path.Combine(_folder,"plugin.json"));_settings=Path.Combine(Configuration.DataDirectory,"ClassicPluginData",new DirectoryInfo(_folder).Name);
    }
    private Subtitle Source(string text){var s=new Subtitle();s.Paragraphs.Add(new Paragraph(text,1000,2000));return s;}
    [Fact] public void NativePluginRoundTripsContextAndSerializesSettings()
    {
        var source=Source("hello");var a=_plugin.Run(source,new SubRip(),"a.srt","batch",CancellationToken.None);var b=_plugin.Run(source,new SubRip(),"b.srt","batch",CancellationToken.None);Task.WhenAll(a,b).GetAwaiter().GetResult();
        Assert.Equal("hello plugin",a.Result.Paragraphs[0].Text);Assert.Equal("hello",source.Paragraphs[0].Text);Assert.Equal(2,(int)JObject.Parse(File.ReadAllText(Path.Combine(_settings,"settings.json")))["settings"]["count"]);
    }
    [Fact] public void InvalidResponseDoesNotMutateSourceOrPersistSettings()
    {var source=Source("invalid");Assert.Throws<InvalidDataException>(()=>_plugin.Run(source,new SubRip(),"a.srt","batch",CancellationToken.None).GetAwaiter().GetResult());Assert.Equal(1000d,source.Paragraphs[0].StartTime.TotalMilliseconds);Assert.False(File.Exists(Path.Combine(_settings,"settings.json")));}
    [Fact] public void CancelledResponseKeepsSource()
    {var source=Source("cancel");var result=_plugin.Run(source,new SubRip(),"a.srt","batch",CancellationToken.None).GetAwaiter().GetResult();Assert.Equal("cancel",result.Paragraphs[0].Text);Assert.False(File.Exists(Path.Combine(_settings,"settings.json")));}
    [Fact] public void CancellationStopsRunningPlugin()
    {using(var cancel=new CancellationTokenSource(500)){Assert.Throws<OperationCanceledException>(()=>_plugin.Run(Source("slow"),new SubRip(),"a.srt","batch",cancel.Token).GetAwaiter().GetResult());}}
    [Fact] public void ManifestRejectsEscapingExecutable()
    {File.WriteAllText(Path.Combine(_folder,"plugin.json"),"{\"apiVersion\":1,\"batch\":true,\"name\":\"Fixture\",\"runtime\":\"native\",\"executables\":{\"windows\":\"../BatchPluginFixture.exe\"}}");Assert.Throws<InvalidDataException>(()=>ClassicBatchPlugin.Load(Path.Combine(_folder,"plugin.json")));}
    public void Dispose(){if(Directory.Exists(_folder))Directory.Delete(_folder,true);if(Directory.Exists(_settings))Directory.Delete(_settings,true);}
 }
}
