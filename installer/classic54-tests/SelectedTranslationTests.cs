using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
namespace Classic54Tests
{
 public class SelectedTranslationTests
 {
  private static Subtitle Source(){var sub=new Subtitle();for(int i=0;i<5;i++)sub.Paragraphs.Add(new Paragraph("English cue "+i+".",i*2000,i*2000+1000){Actor="Actor"+i,Extra="Style"+i,Layer=i,Forced=i==3,Number=i+1});return sub;}
  private sealed class Handler:HttpMessageHandler
  {
   public int Calls;public JObject Payload;public bool Invalid;public CancellationTokenSource CancelOnSecond;
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
   {
    Calls++;var data=JObject.Parse(await request.Content.ReadAsStringAsync());Payload=JObject.Parse((string)data["messages"].First(m=>(string)m["role"]=="user")["content"]);
    if(Calls==2 && CancelOnSecond!=null)CancelOnSecond.Cancel();token.ThrowIfCancellationRequested();
    var output=new JObject();foreach(var line in (JArray)Payload["lines"])output[((int)line["n"]).ToString()]="Русская реплика "+((string)line["text"]).Split(' ')[2].TrimEnd('.')+".";
    var reply=Invalid?"{\"wrong\":\"invalid\"}":output.ToString(Newtonsoft.Json.Formatting.None);
    return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(new JObject{["choices"]=new JArray(new JObject{["message"]=new JObject{["content"]=reply}})}.ToString())};
   }
  }
  private static ClassicNewTranslator Translator(Handler handler,int batch=8)
  {
   var translator=new ClassicNewTranslator("lmstudio-advanced",handler);var settings=new ClassicTranslationSettings{BatchSize=batch,UseLocalLlama=false};settings.Api.Url="https://translation.invalid/v1/chat/completions";settings.Api.Model="offline-test";translator.Initialize(settings);return translator;
  }
  [Fact]public void OnlySelectedRowsAreTranslatedWithReadOnlySurroundings()
  {
   var source=Source();var target=Source();foreach(var p in target.Paragraphs)p.Text="Old "+p.Number;
   using(var handler=new Handler())using(var translator=Translator(handler))
   {
    var result=ClassicSelectedTranslation.Translate(source,target,new[]{3,1},translator,"en","ru",CancellationToken.None).GetAwaiter().GetResult();Assert.Equal(1,handler.Calls);Assert.Equal(2,((JArray)handler.Payload["lines"]).Count);Assert.Equal(3,((JArray)handler.Payload["surrounding_source"]).Count);
    foreach(var i in new[]{0,2,4})Assert.Equal(target.Paragraphs[i].Text,result.Paragraphs[i].Text);
    foreach(var i in new[]{1,3}){Assert.Contains("реплика "+i,result.Paragraphs[i].Text);Assert.Equal(target.Paragraphs[i].Actor,result.Paragraphs[i].Actor);Assert.Equal(target.Paragraphs[i].Extra,result.Paragraphs[i].Extra);Assert.Equal(target.Paragraphs[i].Layer,result.Paragraphs[i].Layer);Assert.Equal(target.Paragraphs[i].Forced,result.Paragraphs[i].Forced);Assert.Equal(target.Paragraphs[i].StartTime.TotalMilliseconds,result.Paragraphs[i].StartTime.TotalMilliseconds);}
    Assert.Equal("Old 2",target.Paragraphs[1].Text);
   }
  }
  [Fact]public void InvalidResponseDoesNotApplyPartialChanges()
  {
   var source=Source();var target=Source();using(var handler=new Handler{Invalid=true})using(var translator=Translator(handler)){Assert.Throws<InvalidDataException>(()=>ClassicSelectedTranslation.Translate(source,target,new[]{1,3},translator,"en","ru",CancellationToken.None).GetAwaiter().GetResult());Assert.Equal(2,handler.Calls);Assert.All(target.Paragraphs,p=>Assert.StartsWith("English cue",p.Text));}
  }
  [Fact]public void CancellingLaterBatchKeepsAllOldTranslations()
  {
   var source=Source();var target=Source();using(var cancel=new CancellationTokenSource())using(var handler=new Handler{CancelOnSecond=cancel})using(var translator=Translator(handler,1)){Assert.Throws<OperationCanceledException>(()=>ClassicSelectedTranslation.Translate(source,target,new[]{0,1,2},translator,"en","ru",cancel.Token).GetAwaiter().GetResult());Assert.All(target.Paragraphs,p=>Assert.StartsWith("English cue",p.Text));}
  }
 }
}
