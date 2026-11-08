using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Translate;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicTiming;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Nikse.SubtitleEdit.Forms.Translate;
namespace Classic54Tests
{
 public class TranslationAsr54CompletionTests
 {
  private sealed class Handler:HttpMessageHandler
  {
   public int Calls; public Func<HttpRequestMessage,int,Task<HttpResponseMessage>> Respond;
   protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){cancel.ThrowIfCancellationRequested();return Respond(request,++Calls);}
  }
  private static HttpResponseMessage Reply(string text,HttpStatusCode status=HttpStatusCode.OK)=>new HttpResponseMessage(status){Content=new StringContent(text,Encoding.UTF8,"application/json")};
  private static string Completion(string content,string reasoning=null)=>new JObject{["choices"]=new JArray(new JObject{["message"]=new JObject{["content"]=content,["reasoning_content"]=reasoning}})}.ToString();
  [Theory][InlineData("♪ Singing",true)][InlineData("- ♪ First\n— ♪ Second",true)][InlineData("<i>🎵 Song 🎶</i>",true)][InlineData("♬ First\nSecond ♬",true)][InlineData("♪ Singing\nSpoken words",false)][InlineData("ordinary speech",false)][InlineData("",false)]
  public void MusicLayoutsRecognizedWithoutSkippingMixedSpeech(string text,bool expected)=>Assert.Equal(expected,TranslationMusic.IsMusicLine(text.Replace("\\n","\n")));
  [Fact]public async Task OrdinaryAndSelectedMusicRowsNeverReachEngine()
  {
   var old=Configuration.Settings.Tools.AutoTranslateKeepMusicLines;Configuration.Settings.Tools.AutoTranslateKeepMusicLines=true;
   try
   {
    var s=new Subtitle();s.Paragraphs.Add(new Paragraph("<i>♪ Keep me ♪</i>",0,1000));var target=new Subtitle(s);target.Paragraphs[0].Text="old";
    using(var h=new Handler{Respond=(request,n)=>throw new Exception("Lyric was sent")})using(var engine=new ClassicNewTranslator("compatible",h))
    {
     Assert.Equal(1,await MergeAndSplitHelper.MergeAndTranslateIfPossible(s,target,new TranslationPair("English","en"),new TranslationPair("Russian","ru"),0,engine,true,CancellationToken.None));
     Assert.Equal(s.Paragraphs[0].Text,target.Paragraphs[0].Text);
     var selected=await ClassicSelectedTranslation.Translate(s,target,new[]{0},engine,"en","ru",CancellationToken.None);Assert.Equal(s.Paragraphs[0].Text,selected.Paragraphs[0].Text);Assert.Equal(0,h.Calls);
    }
   }finally{Configuration.Settings.Tools.AutoTranslateKeepMusicLines=old;}
  }
  [Fact]public async Task AdvancedMixedBatchKeepsLyricsAndNumbersOnlySpeech()
  {
   var old=Configuration.Settings.Tools.AutoTranslateKeepMusicLines;Configuration.Settings.Tools.AutoTranslateKeepMusicLines=true;
   try
   {
    using(var h=new Handler{Respond=async(request,n)=>{var payload=JObject.Parse(await request.Content.ReadAsStringAsync());var data=JObject.Parse((string)payload["messages"][1]["content"]);Assert.Equal(2,data["lines"].Count());Assert.DoesNotContain("Singing",data["lines"].ToString());return Reply(Completion("{\"1\":\"Привет.\",\"2\":\"До свидания.\"}"));}})
    using(var translator=new ClassicNewTranslator("lmstudio-advanced",h))
    {
     var settings=new ClassicTranslationSettings();settings.Api.Url="http://offline.invalid/v1";translator.Initialize(settings);
     var result=await translator.TranslateBatchAsync(new[]{"Hello.","<i>♪ Singing ♪</i>","Goodbye."},"en","ru",CancellationToken.None);
     Assert.Equal("<i>♪ Singing ♪</i>",result[1]);Assert.Equal("До свидания.",result[2]);Assert.Equal(1,h.Calls);
    }
   }finally{Configuration.Settings.Tools.AutoTranslateKeepMusicLines=old;}
  }
  [Fact]public void LanguageVariantExactMatchBeatsBroadIso()
  {
   using(var box=new NikseComboBox())
   {
    box.Items.Add(new TranslationPair("Brazil","pt-BR","pt"));box.Items.Add(new TranslationPair("Portugal","pt-PT","pt"));
    AutoTranslate.SelectLanguageCode(box,"pt-PT");Assert.Equal("pt-PT",((TranslationPair)box.SelectedItem).Code);
   }
  }
  [Theory][InlineData("localhost:11434","http://localhost:11434/v1/chat/completions")][InlineData("http://localhost:11434/api/generate","http://localhost:11434/v1/chat/completions")][InlineData("https://api.opper.ai/v3/compat","https://api.opper.ai/v3/compat/chat/completions")][InlineData("http://localhost:8080/v1/chat/completions/","http://localhost:8080/v1/chat/completions")]
  public void ChatUrlNormalizesOllamaAndKeepsCustomApiBase(string input,string expected)=>Assert.Equal(expected,ClassicAiClient.ChatUrl(input));
  [Fact]public async Task ReasoningOnlyTranslationRetriesWithThinkingDisabledAndRemembersIt()
  {
   using(var h=new Handler{Respond=async(request,n)=>{var payload=JObject.Parse(await request.Content.ReadAsStringAsync());if(n==1){Assert.Null(payload["chat_template_kwargs"]);return Reply(Completion("","I should translate this sentence carefully."));}Assert.Equal(false,(bool)payload["chat_template_kwargs"]["enable_thinking"]);return Reply(Completion("{\"1\":\"Привет.\"}"));}})
   using(var translator=new ClassicNewTranslator("lmstudio-advanced",h))
   {
    var settings=new ClassicTranslationSettings();settings.Api.Url="http://offline.invalid/v1";translator.Initialize(settings);
    Assert.Equal("Привет.",(await translator.TranslateBatchAsync(new[]{"Hello."},"en","ru",CancellationToken.None))[0]);
    await translator.TranslateBatchAsync(new[]{"Hello again."},"en","ru",CancellationToken.None);Assert.Equal(3,h.Calls);
   }
  }
  [Fact]public async Task StrictServerCanRejectThinkingParameterWithoutBreakingRequests()
  {
   using(var h=new Handler{Respond=async(request,n)=>{var payload=JObject.Parse(await request.Content.ReadAsStringAsync());if(n==1)return Reply(Completion("","reasoning"));if(n==2){Assert.NotNull(payload["chat_template_kwargs"]);return Reply("{\"error\":\"unknown chat_template_kwargs\"}",HttpStatusCode.BadRequest);}Assert.Null(payload["chat_template_kwargs"]);return Reply(Completion("answer"));}})
   using(var client=new ClassicAiClient(new ClassicCompatibleAsrSettings{Url="http://offline.invalid/v1"},h))
   {
    await client.ChatAsync(new JArray(),false,CancellationToken.None);Assert.Equal("answer",await client.ChatAsync(new JArray(),false,CancellationToken.None));Assert.Equal(3,h.Calls);
   }
  }
  [Fact]public async Task ModelRefreshUsesAuthenticatedModelsEndpointAndRetainsDistinctIds()
  {
   var settings=new ClassicCompatibleAsrSettings{Url="localhost:11434/api/generate",ApiKey="offline-key"};
   using(var h=new Handler{Respond=(request,n)=>{Assert.Equal(HttpMethod.Get,request.Method);Assert.Equal("http://localhost:11434/v1/models",request.RequestUri.AbsoluteUri);Assert.Equal("offline-key",request.Headers.Authorization.Parameter);return Task.FromResult(Reply("{\"data\":[{\"id\":\"z:model\"},{\"id\":\"a:model\"},{\"id\":\"a:model\"}]}"));}})
   using(var client=new ClassicAiClient(settings,h))Assert.Equal(new[]{"a:model","z:model"},await client.GetModelsAsync(CancellationToken.None));
  }
  [Fact]public async Task DashScopeSerializesBrowserCompatibleMultipartWithoutLeakingKey()
  {
   var file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dash-"+Guid.NewGuid().ToString("N")+".wav");File.WriteAllBytes(file,new byte[]{1,2,3});
   try
   {
    var settings=ClassicCloudAsrSettings.Load("dashscope");settings.Api.ApiKey="offline-key";
    using(var h=new Handler{Respond=async(request,n)=>
    {
     if(n==1)return Reply("{\"data\":{\"upload_dir\":\"temp\",\"upload_host\":\"https://storage.invalid/upload\",\"oss_access_key_id\":\"access\",\"policy\":\"policy\",\"signature\":\"signature\",\"x_oss_object_acl\":\"private\",\"x_oss_forbid_overwrite\":\"true\"}}");
     if(n==2)
     {
      Assert.Null(request.Headers.Authorization);var type=request.Content.Headers.ContentType.ToString();Assert.DoesNotContain("boundary=\"",type);
      var body=await request.Content.ReadAsStringAsync();Assert.Contains("Content-Disposition: form-data; name=\"OSSAccessKeyId\"",body);Assert.Contains("name=\"file\"; filename=\"audio.wav\"",body);Assert.DoesNotContain("filename*",body);Assert.Contains("Content-Type: application/octet-stream",body);return Reply("");
     }
     if(n==3)return Reply("{\"output\":{\"task_id\":\"task\"}}");
     if(n==4)return Reply("{\"output\":{\"task_status\":\"SUCCEEDED\",\"results\":[{\"transcription_url\":\"https://storage.invalid/result\"}]}}");
     Assert.Null(request.Headers.Authorization);return Reply("{\"transcripts\":[{\"sentences\":[{\"begin_time\":0,\"end_time\":1000,\"text\":\"Hello.\"}]}]}");
    }})using(var cloud=new ClassicCloudAsr("dashscope",settings,h)){await cloud.TranscribeChunkAsync(file,"en",CancellationToken.None);Assert.Equal(5,h.Calls);}
   }finally{File.Delete(file);}
  }
  [Fact]public void ActualWhisperDialogShowsPinned195Help()
  {
   using(var dialog=new Nikse.SubtitleEdit.Forms.AudioToText.WhisperAdvanced(Nikse.SubtitleEdit.Core.AudioToText.WhisperChoice.Cpp))
   {
    var box=dialog.Controls.Find("textBoxCpp",true).Single();Assert.StartsWith("Whisper CPP 1.9.5",box.Text);Assert.Contains("--vad-model",box.Text);
   }
  }
  private sealed class Aligner:ClassicAlignerContracts.IRunner,ClassicAlignerContracts.IAudioSource,IDisposable
  {
   public int Calls;public bool DataFailure;public bool FirstSucceeds;
   public double TotalSeconds=>1000;
   private string Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"align-"+Guid.NewGuid().ToString("N"));
   public Aligner(){Directory.CreateDirectory(Folder);}
   public Task<string> ExtractWindowAsync(double start,double duration,CancellationToken cancel)=>Task.FromResult(Path.Combine(Folder,Guid.NewGuid().ToString("N")+".wav"));
   public Task<string> AlignAsync(string audio,string text,CancellationToken cancel)
   {
    Calls++;if(FirstSucceeds && Calls==1)return Task.FromResult("1\n00:00:01,000 --> 00:00:02,000\nFirst.\n");
    throw DataFailure?new ForcedAlignerException("bad transcript"):new ForcedAlignerException("engine crashed","native diagnostic",-1073741819);
   }
   public void Dispose()=>Directory.Delete(Folder,true);
  }
  private static SubtitleRetimer.Line[] Lines()=>Enumerable.Range(0,7).Select(i=>new SubtitleRetimer.Line("Cue "+i,10+i*10,12+i*10)).ToArray();
  [Fact]public async Task RepeatedEngineCrashesStopAfterThreeBeforeAnySuccess()
  {
   using(var aligner=new Aligner())
   {
    ForcedAlignerException error=null;try{await new SubtitleRetimer(aligner,aligner).RetimeAsync(Lines());}catch(ForcedAlignerException ex){error=ex;}
    Assert.NotNull(error);Assert.Equal(3,aligner.Calls);Assert.Equal(-1073741819,error.ExitCode.Value);Assert.Equal("native diagnostic",error.Details);
   }
  }
  [Fact]public async Task DataErrorsDoNotTriggerEngineCrashThreshold()
  {
   using(var aligner=new Aligner{DataFailure=true}){try{await new SubtitleRetimer(aligner,aligner).RetimeAsync(Lines());}catch(ForcedAlignerException){}Assert.Equal(7,aligner.Calls);}
  }
  [Fact]public async Task OneSuccessfulBatchDisablesInitialCrashAbort()
  {
   using(var aligner=new Aligner{FirstSucceeds=true}){var result=await new SubtitleRetimer(aligner,aligner).RetimeAsync(Lines());Assert.Equal(7,aligner.Calls);Assert.Equal(7,result.Count);}
  }
  [Fact]public void MissingModelPlanIncludesAllAssetsBeforeDownload()
  {
   var plan=ClassicRetimingJob.Prepare("cpu-legacy","canary-ctc",true,"en");var names=plan.MissingAssets();Assert.NotNull(plan.SpeechCheck);Assert.Contains(names,name=>name.Contains("canary-ctc"));Assert.Contains(names,name=>name.Contains("parakeet"));
  }
 }
}
