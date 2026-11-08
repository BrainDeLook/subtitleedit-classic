using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.SpeechToText;
namespace Nikse.SubtitleEdit.Logic.Plugins
{
    public sealed class ClassicBatchPlugin
    {
        public PluginManifest Manifest {get;private set;}
        public string Folder {get;private set;}
        public string LaunchPath {get;private set;}
        public override string ToString()=>Manifest.Name+" · "+Manifest.Version;
        private static readonly JsonSerializerSettings Json=new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver(),Formatting=Formatting.Indented};
        public static ClassicBatchPlugin Load(string file)
        {
            var manifest=JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(file),Json);
            if(manifest==null || manifest.ApiVersion!=PluginConstants.ApiVersion || !manifest.Batch || string.IsNullOrWhiteSpace(manifest.Name))throw new InvalidDataException("The plugin does not support the batch API.");
            var folder=Path.GetDirectoryName(Path.GetFullPath(file));var entry=manifest.Runtime=="dotnet"?manifest.Entry:manifest.Executables?.Windows;
            if(string.IsNullOrWhiteSpace(entry))throw new InvalidDataException("The plugin has no Windows executable.");
            var launch=Path.GetFullPath(Path.Combine(folder,entry));
            if(!launch.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || !File.Exists(launch))throw new InvalidDataException("The plugin executable is missing or outside its folder.");
            return new ClassicBatchPlugin{Manifest=manifest,Folder=folder,LaunchPath=launch};
        }
        public static List<ClassicBatchPlugin> Installed()
        {
            var result=new List<ClassicBatchPlugin>();if(!Directory.Exists(Configuration.PluginsDirectory))return result;
            foreach(var file in Directory.GetFiles(Configuration.PluginsDirectory,"plugin.json",SearchOption.AllDirectories))
            {
                try{result.Add(Load(file));}catch(Exception ex) when(ex is IOException || ex is JsonException || ex is UnauthorizedAccessException){SeLogger.Error(ex,"Read batch plugin manifest");}
            }
            return result;
        }
        private static readonly ConcurrentDictionary<string,SemaphoreSlim> Gates=new ConcurrentDictionary<string,SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        public async Task<Subtitle> Run(Subtitle source,SubtitleFormat format,string fileName,string requestType,CancellationToken cancel)
        {
            var gate=Gates.GetOrAdd(Folder,_=>new SemaphoreSlim(1,1));
            await gate.WaitAsync(cancel).ConfigureAwait(false);
            try{return await RunCore(source,format,fileName,requestType,cancel).ConfigureAwait(false);}
            finally{gate.Release();}
        }
        private async Task<Subtitle> RunCore(Subtitle source,SubtitleFormat format,string fileName,string requestType,CancellationToken cancel)
        {
            var temp=Path.Combine(Path.GetTempPath(),"SubtitleEdit-plugin-"+Guid.NewGuid().ToString("N"));
            var data=Path.Combine(Configuration.DataDirectory,"ClassicPluginData",new DirectoryInfo(Folder).Name);Directory.CreateDirectory(data);
            var settingsPath=Path.Combine(data,"settings.json");
            JObject settings=null;if(File.Exists(settingsPath))settings=JObject.Parse(File.ReadAllText(settingsPath));
            var request=new PluginRequest{RequestType=requestType,ResponseFilePath=Path.Combine(temp,"response.json"),TempDirectory=temp,PluginDataDirectory=data,FrameRate=Configuration.Settings.General.CurrentFrameRate,UiLanguage=Configuration.Settings.General.Language,Theme=Configuration.Settings.General.UseDarkTheme?"Dark":"Light",SeVersion=typeof(ClassicBatchPlugin).Assembly.GetName().Version.ToString(),Settings=settings?["settings"],SettingsVersion=(int?)settings?["version"],Subtitle=new PluginSubtitle{Format=format.Name,FileName=fileName,Native=format.IsTextBased?source.ToText(format):"",SubRip=source.ToText(new SubRip()),Header=source.Header,Paragraphs=source.Paragraphs.Select(p=>new PluginParagraph{StartMs=p.StartTime.TotalMilliseconds,EndMs=p.EndTime.TotalMilliseconds,Text=p.Text,Style=p.Extra,Actor=p.Actor,Language=p.Language,Region=p.Region,Effect=p.Effect,Extra=p.Extra,MarginL=p.MarginL,MarginR=p.MarginR,MarginV=p.MarginV,Layer=p.Layer,IsComment=p.IsComment,Forced=p.Forced,NewSection=p.NewSection,Bookmark=p.Bookmark}).ToList()}};
            var rules=Configuration.Settings.General;
            request.Rules=new PluginRules{SubtitleMinimumDisplayMilliseconds=rules.SubtitleMinimumDisplayMilliseconds,SubtitleMaximumDisplayMilliseconds=rules.SubtitleMaximumDisplayMilliseconds,SubtitleMaximumCharactersPerSeconds=rules.SubtitleMaximumCharactersPerSeconds,SubtitleOptimalCharactersPerSeconds=rules.SubtitleOptimalCharactersPerSeconds,SubtitleLineMaximumLength=rules.SubtitleLineMaximumLength,MaxNumberOfLines=rules.MaxNumberOfLines,MinimumMillisecondsBetweenLines=rules.MinimumMillisecondsBetweenLines,UseFrameMode=rules.UseTimeFormatHHMMSSFF};
            var dark=rules.UseDarkTheme;
            request.ThemeColors=new PluginThemeColors{IsDark=dark,BackgroundColor=dark?"#FF202020":"#FFF0F0F0",ForegroundColor=dark?"#FFF0F0F0":"#FF000000",AccentColor="#FF0078D4",BackgroundColorLighter=dark?"#FF303030":"#FFFFFFFF",BackgroundColorHeader=dark?"#FF383838":"#FFE0E0E0",BookmarkColor="#FFFFFF00"};
            try
            {
                Directory.CreateDirectory(temp);
                var requestPath=Path.Combine(temp,"request.json");File.WriteAllText(requestPath,JsonConvert.SerializeObject(request,Json),new UTF8Encoding(false));
                var dotnet=Manifest.Runtime=="dotnet";var start=new ProcessStartInfo(dotnet?"dotnet":LaunchPath,(dotnet?ClassicSpeechRunner.Quote(LaunchPath)+" ":"")+ClassicSpeechRunner.Quote(requestPath)){WorkingDirectory=Folder};
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel))
                { timeout.CancelAfter(TimeSpan.FromMinutes(10));await ClassicSpeechRunner.RunProcessAsync(start,timeout.Token,null).ConfigureAwait(false); }
                if(!File.Exists(request.ResponseFilePath))throw new InvalidDataException("Plugin produced no response.");
                var response=JsonConvert.DeserializeObject<PluginResponse>(File.ReadAllText(request.ResponseFilePath),Json);
                if(response==null || response.ApiVersion!=PluginConstants.ApiVersion)throw new InvalidDataException("Invalid plugin response version.");
                if(response.Status==PluginConstants.StatusCancelled)return new Subtitle(source,false);
                if(response.Status!=PluginConstants.StatusOk)throw new InvalidDataException(response.Message ?? "Plugin returned an error.");
                var result=new Subtitle(source,false);
                if(requestType!=PluginConstants.RequestTypeConfigure && response.Subtitle!=null)
                {
                    var output=response.Subtitle;
                    if(output.Paragraphs!=null)
                    {
                        result.Paragraphs.Clear();foreach(var p in output.Paragraphs)
                        {
                            if(p==null || double.IsNaN(p.StartMs) || double.IsInfinity(p.StartMs) || double.IsNaN(p.EndMs) || double.IsInfinity(p.EndMs) || p.StartMs<0 || p.EndMs<p.StartMs || p.Text==null)throw new InvalidDataException("Plugin returned an invalid subtitle line.");
                            result.Paragraphs.Add(new Paragraph(p.Text,p.StartMs,p.EndMs){Extra=p.Style ?? p.Extra,Actor=p.Actor,Language=p.Language,Region=p.Region,Effect=p.Effect,MarginL=p.MarginL,MarginR=p.MarginR,MarginV=p.MarginV,Layer=p.Layer,IsComment=p.IsComment,Forced=p.Forced,NewSection=p.NewSection,Bookmark=p.Bookmark});
                        }
                        if(output.Header!=null)result.Header=output.Header;result.Renumber();
                    }
                    else if(!string.IsNullOrEmpty(output.Native))
                    {
                        var outputFormat=SubtitleFormat.FromName(output.Format,null);
                        if(outputFormat==null || !outputFormat.IsTextBased)throw new InvalidDataException("Plugin returned an unsupported text format.");
                        result=new Subtitle();outputFormat.LoadSubtitle(result,output.Native.SplitToLines(),fileName);
                        if(result.Paragraphs.Count==0 && source.Paragraphs.Count>0)throw new InvalidDataException("Plugin returned unreadable subtitles.");
                    }
                }
                cancel.ThrowIfCancellationRequested();
                if(response.Settings!=null)
                {
                    var pending=settingsPath+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try{File.WriteAllText(pending,new JObject{["settings"]=response.Settings,["version"]=response.SettingsVersion}.ToString());if(File.Exists(settingsPath))File.Replace(pending,settingsPath,null);else File.Move(pending,settingsPath);}
                    finally{if(File.Exists(pending))File.Delete(pending);}
                }
                return result;
            }
            finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
        }
    }
}
