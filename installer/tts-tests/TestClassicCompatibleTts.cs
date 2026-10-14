using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicCompatibleTts
{
    [STAThread] static void Main(string[] args)
    {
        var folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,folder+Path.DirectorySeparatorChar);
        Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[1];
        Nikse.SubtitleEdit.Logic.LanguageSettings.Current=Nikse.SubtitleEdit.Logic.Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
        var json=JObject.Parse(ClassicCompatibleTts.BuildRequestJson("Say \"hi\"","gpt-4o-mini-tts","alloy","mp3"));Check((string)json["input"]=="Say \"hi\"" && json["speed"]==null && json["instructions"]==null,"Default request shape");
        var tts1=JObject.Parse(ClassicCompatibleTts.BuildRequestJson("Test","tts-1-hd","alloy","pcm",1.25,"Calm"));Check(tts1["instructions"]==null && (double)tts1["speed"]==1.25,"Classic model parameter compatibility");
        Check(ClassicCompatibleTts.Voices("OpenAI","tts-1").Length==9 && ClassicCompatibleTts.Voices("OpenAI","gpt-4o-mini-tts").Length==13 && ClassicCompatibleTts.Models("OpenRouter").Length>0,"Stable provider catalog");
        var settings=new ClassicCompatibleProvider{Url="http://127.0.0.1:1/v1/audio/speech",Model="fixture-pcm-only",Voice="alloy",ApiKey="fixture-key"};
        Check(!settings.ProtectedApiKey.Contains("fixture-key") && settings.ApiKey=="fixture-key","Protected API key round trip");
        using(var handler=new Mock())using(var engine=new ClassicCompatibleTts(settings,handler))
        {
            engine.SpeakAsync("Hello","alloy",Path.Combine(folder,"speech.wav"),CancellationToken.None).GetAwaiter().GetResult();
            engine.SpeakAsync("Second line","alloy",Path.Combine(folder,"speech2.wav"),CancellationToken.None).GetAwaiter().GetResult();
            Check(handler.Calls==3 && new FileInfo(Path.Combine(folder,"speech.wav")).Length>48000,"PCM fallback/normalization and cache");
        }
        using(var engine=new ClassicCompatibleTts(settings,new CancelMock()))using(var cancel=new CancellationTokenSource())
        {cancel.CancelAfter(50);try{engine.SpeakAsync("Cancelled","alloy",Path.Combine(folder,"cancelled.wav"),cancel.Token).GetAwaiter().GetResult();throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}Check(!File.Exists(Path.Combine(folder,"cancelled.wav")),"Cancellation published output");}
        var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Test",1000,3000));var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        using(var form=new TextToSpeech(sub,new SubRip(),null,null))
        {
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
            var combo=typeof(TextToSpeech).GetField("nikseComboBoxEngine",flags).GetValue(form);combo.GetType().GetProperty("Text").SetValue(combo,"OpenAI-compatible (API)");Application.DoEvents();
            var key=(TextBox)typeof(TextToSpeech).GetField("nikseTextBoxApiKey",flags).GetValue(form);Check(key.UseSystemPasswordChar && key.Visible,"API key UI visibility");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"api-ui.png"));}
            combo.GetType().GetProperty("Text").SetValue(combo,"Supertonic-3 (CrispASR / local)");Application.DoEvents();Check(!key.UseSystemPasswordChar,"Password mode leaked into speed field");form.Close();
        }
        Console.WriteLine("PASS: stable API request shapes, provider catalogs, protected settings, PCM fallback/cache, WAV conversion, cancellation and classic UI switching");
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    sealed class Mock : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)
        {
            Calls++;Check(request.Headers.Authorization?.Parameter=="fixture-key","Bearer authorization missing");
            var body=JObject.Parse(await request.Content.ReadAsStringAsync());
            if(Calls==1)return new HttpResponseMessage(HttpStatusCode.BadRequest){Content=new StringContent("Only response_format=pcm is supported")};
            Check((string)body["response_format"]=="pcm","PCM fallback not retained");
            var data=new byte[48000];for(int i=0;i<24000;i++){short sample=(short)(Math.Sin(i*2*Math.PI*440/24000)*5000);data[i*2]=(byte)sample;data[i*2+1]=(byte)(sample>>8);}
            var content=new ByteArrayContent(data);content.Headers.ContentType=System.Net.Http.Headers.MediaTypeHeaderValue.Parse("audio/pcm; rate=24000; channels=1");return new HttpResponseMessage(HttpStatusCode.OK){Content=content};
        }
    }
    sealed class CancelMock : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){await Task.Delay(5000,cancel);return new HttpResponseMessage(HttpStatusCode.OK);}
    }
}
