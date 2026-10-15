using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestFinalTts
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static object Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
    [STAThread] static int Main(string[] args)
    {
        try {
            var data=Path.GetFullPath(args[1]);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
            typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,data+Path.DirectorySeparatorChar);
            Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
            if(args[0]=="install-edge"){var settings=ClassicOnlineTtsSettings.Load("edge");if(!File.Exists(ClassicOnlineTts.EdgeExecutable(settings)))ClassicOnlineTts.InstallEdgeAsync(settings,CancellationToken.None,new Log()).GetAwaiter().GetResult();}
            else if(args[0]=="edge-cast")EdgeCast(data);
            else if(args[0]=="edge")Edge(data).GetAwaiter().GetResult();else if(args[0]=="zonos")Zonos(data).GetAwaiter().GetResult();else { Unit(data).GetAwaiter().GetResult(); Ui(data); }
            return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static async Task Unit(string data)
    {
        Check(ClassicOnlineTts.Voices("mistral").Length>0 && ClassicOnlineTts.Voices("google").Length>100 && ClassicOnlineTts.Voices("edge").Any(v=>v.Id=="ru-RU-DmitryNeural"),"complete bundled voices and Russian Edge fallback");
        Check(ClassicOnlineTts.LanguageCode("cmn-CN-Chirp3-HD-Achernar")=="cmn-CN" && ClassicOnlineTts.LanguageCode("fil-PH-Standard-A")=="fil-PH","Google three-letter language codes");
        var mistral=JObject.Parse(ClassicOnlineTts.BuildRequest("mistral","Привет \"日本語\"","fixture-id","voxtral-mini-tts-2603"));
        Check((string)mistral["voice_id"]=="fixture-id" && mistral["voice"]==null && (string)mistral["input"]=="Привет \"日本語\"","stable Mistral voice_id and Unicode request");
        var google=JObject.Parse(ClassicOnlineTts.BuildRequest("google","Hello","cmn-CN-Chirp3-HD-Achernar",null));Check((string)google["voice"]["languageCode"]=="cmn-CN" && (string)google["audioConfig"]["audioEncoding"]=="MP3","Google REST request matches stable synthesis");
        Check(ClassicOnlineTts.NormalizeProsody("10%","%")=="+10%" && ClassicOnlineTts.NormalizeProsody("-12","Hz")=="-12Hz" && ClassicOnlineTts.NormalizeProsody("garbage","%")=="","Edge prosody normalization");
        var settings=new ClassicOnlineTtsSettings();settings.Credentials.ApiKey="fixture-secret";settings.Voice=ClassicOnlineTts.Voices("mistral")[0].Id;settings.Save("mistral");
        Check(!File.ReadAllText(Path.Combine(ClassicOnlineTtsSettings.Root("mistral"),"settings.json")).Contains("fixture-secret") && ClassicOnlineTtsSettings.Load("mistral").Credentials.ApiKey=="fixture-secret","encrypted Mistral credential roundtrip");
        var wave=Wave();
        using(var mock=new Mock("mistral",wave))using(var engine=new ClassicOnlineTts("mistral",settings,mock)) {
            await engine.SpeakAsync("Hello","fixture-voice",Path.Combine(data,"mistral.wav"),CancellationToken.None);
            Check(ClassicVoiceAssets.WaveSeconds(Path.Combine(data,"mistral.wav"))>0.9,"Mistral JSON base64 audio decoded and normalized");
            var previous=ClassicOnlineTts.Voices("mistral")[0].Id;mock.Fail=true;
            try{await engine.RefreshVoicesAsync(CancellationToken.None);throw new Exception("HTTP failure swallowed");}catch(InvalidOperationException e){Check(e.Message.Contains("403") && !e.Message.Contains("fixture-secret"),"HTTP errors do not expose provider response contents");}
            Check(ClassicOnlineTts.Voices("mistral")[0].Id==previous,"failed voice refresh retains previous catalog");
            mock.Fail=false;await engine.RefreshVoicesAsync(CancellationToken.None);Check(ClassicOnlineTts.Voices("mistral")[0].Id=="refreshed","voice refresh publishes valid response only");
        }
        using(var rsa=new RSACryptoServiceProvider(2048)) {
            rsa.PersistKeyInCsp=false;var pem=Pem(rsa.ExportParameters(true));var jwt=ClassicOnlineTts.CreateGoogleAssertion("test@example.invalid",pem,123456);
            var parts=jwt.Split('.');Check(rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0]+"."+parts[1]),"SHA256",Decode(parts[2])),"Google PKCS8 import and JWT RSA-SHA256 signature");
            var claims=JObject.Parse(Encoding.UTF8.GetString(Decode(parts[1])));Check((long)claims["exp"]-(long)claims["iat"]==3600 && (string)claims["aud"]=="https://oauth2.googleapis.com/token","Google assertion audience and expiration");
            var keyFile=Path.Combine(data,"fixture-key.json");File.WriteAllText(keyFile,JsonConvert.SerializeObject(new{type="service_account",client_email="test@example.invalid",private_key=pem}));
            using(var mock=new Mock("google",wave))using(var engine=new ClassicOnlineTts("google",new ClassicOnlineTtsSettings{KeyFile=keyFile},mock)) {
                await engine.SpeakAsync("Hello","en-US-Standard-A",Path.Combine(data,"google.wav"),CancellationToken.None);
                await engine.SpeakAsync("Second","en-US-Standard-A",Path.Combine(data,"google2.wav"),CancellationToken.None);
                Check(mock.Tokens==1 && ClassicVoiceAssets.WaveSeconds(Path.Combine(data,"google.wav"))>0.9,"Google OAuth token cached and audioContent decoded");
            }
        }
        using(var engine=new ClassicOnlineTts("mistral",settings,new Slow()))using(var cancel=new CancellationTokenSource(50)) {
            var output=Path.Combine(data,"cancel.wav");File.WriteAllText(output,"existing");try{await engine.SpeakAsync("Cancel","fixture",output,cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            Check(File.ReadAllText(output)=="existing","cancelled request does not overwrite existing output");
        }
        var z=ClassicLocalTts.Catalog.First(e=>e.Id=="zonos");var zs=ClassicLocalTtsSettings.Load("zonos");zs.Language="ja";
        Check(!z.SupportsCloning && !z.SupportsSpeed && z.Presets.SequenceEqual(new[]{"Default"}) && z.Languages.Any(l=>l.StartsWith("ru|")),"Zonos fixed voice capabilities and stable language list");
        var payload=ClassicExtendedTts.Payload(z,zs,"日本語","Default",false);var args=ClassicExtendedTts.ServerArguments(z,zs,"Default","",data,8123);
        Check((string)payload["language"]=="ja" && !payload.ContainsKey("voice") && !payload.ContainsKey("consent_attestation") && !args.Contains("--voice ") && args.Contains("-l \"ja\""),"Zonos startup phonemizer language and no false cloning flags");
        Check(z.Models.Single().Files.All(f=>f.Hash.Length==64 && f.Bytes>100000000),"Zonos full bundle pinned by size and SHA256");
    }
    static void Ui(string data)
    {
        Application.EnableVisualStyles();var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Hello",1000,3000));
        using(var form=new TextToSpeech(sub,new SubRip(),null,null)) {
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
            var engines=(System.Collections.IEnumerable)Field(form,"_engines");var ids=engines.Cast<TextToSpeech.TextToSpeechEngine>().Select(e=>e.Id.ToString()).ToArray();
            var stable=new[]{"Piper","EdgeTts","AllTalk","ElevenLabs","AzureTextToSpeech","MistralSpeech","OpenAiCompatible","Murf","GoogleSpeech","KokoroLocal","SupertonicCrisp","KugelAudio","ZonosCrisp","OmniStandalone","Qwen3Crisp","VibeCrisp","IndexCrisp","DotsCrisp","ConfuciusCrisp","CosyVoice3","PocketCrisp","IndexTts25","HiggsAudio","FishAudio","BreezeTts","FireRedTts3","VoxCpm2","MossCrisp","OmniCrisp","Chatterbox"};
            Check(stable.Length==30 && stable.All(ids.Contains) && ids.Distinct().Count()==ids.Length,"ALL 30 enabled stable v5.3 Windows TTS implementations present without duplicates");
            var combo=Field(form,"nikseComboBoxEngine");var prop=combo.GetType().GetProperty("Text");
            foreach(var engine in new[]{"Edge TTS (online)","Mistral Speech (API)","Google Cloud Speech (API)","Zonos TTS (CrispASR / local)"}) {
                prop.SetValue(combo,engine);Application.DoEvents();Check(((System.Collections.IList)Field(form,"nikseComboBoxVoice").GetType().GetProperty("Items").GetValue(Field(form,"nikseComboBoxVoice"))).Count>0,"voices in classic UI "+engine);
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(data,engine.Split(' ')[0]+"-ui.png"));}
                if(!engine.StartsWith("Zonos")) using(var timer=new System.Windows.Forms.Timer { Interval=100 }) {
                    timer.Tick+=(sender,eventArgs)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=form && f.Owner==form);if(dialog==null)return;timer.Stop();var table=(TableLayoutPanel)dialog.Controls[0];Check(table.Controls.Cast<Control>().All(c=>c.Top>=table.Padding.Top && c.Bottom<=table.ClientSize.Height-table.Padding.Bottom),"online settings rows fit "+engine);using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(data,engine.Split(' ')[0]+"-settings.png"));}dialog.DialogResult=DialogResult.Cancel;dialog.Close();};
                    timer.Start();typeof(TextToSpeech).GetMethod("EditFinalOnlineSettings",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
                }
            }
            form.Close();
        }
    }
    static void EdgeCast(string data)
    {
        var output=Path.Combine(data,"actor-output");Directory.CreateDirectory(output);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("First actor speaks in English.",0,3000){Actor="A"});sub.Paragraphs.Add(new Paragraph("Второй актёр говорит по-русски.",4000,7000){Actor="B"});sub.Paragraphs.Add(new Paragraph("Do not synthesize this unselected actor.",8000,11000){Actor="C"});
        var cast=new Nikse.SubtitleEdit.Logic.ClassicCast.ClassicCastSettings();
        cast.Rows.Add(new Nikse.SubtitleEdit.Logic.ClassicCast.ClassicCastRow{Actor="A",Engine="Edge TTS (online)",Voice="en-US-GuyNeural",Selected=true});cast.Rows.Add(new Nikse.SubtitleEdit.Logic.ClassicCast.ClassicCastRow{Actor="B",Engine="Edge TTS (online)",Voice="ru-RU-DmitryNeural",Selected=true});cast.Rows.Add(new Nikse.SubtitleEdit.Logic.ClassicCast.ClassicCastRow{Actor="C",Selected=false});
        using(var form=new TextToSpeech(sub,new SubRip(),"",null)) {
            typeof(TextToSpeech).GetField("_castSettings",flags).SetValue(form,cast);typeof(TextToSpeech).GetField("_waveFolder",flags).SetValue(form,output);
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
            var combo=Field(form,"nikseComboBoxEngine");combo.GetType().GetProperty("Text").SetValue(combo,"Edge TTS (online)");
            foreach(ListViewItem item in ((ListView)Field(form,"listViewActors")).Items)item.Checked=item.Text=="A" || item.Text=="B";
            Check((bool)typeof(TextToSpeech).GetMethod("CaptureSpeechActors",flags).Invoke(form,null),"online cast selection captured");
            var task=(Task<bool>)typeof(TextToSpeech).GetMethod("GenerateParagraphAudioAsync",flags).Invoke(form,new object[]{sub,true,null});while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(10);}
            Check(task.GetAwaiter().GetResult(),"real Edge actor generation through classic form");Check(ClassicVoiceAssets.WaveSeconds(Path.Combine(output,"0.wav"))>1 && ClassicVoiceAssets.WaveSeconds(Path.Combine(output,"1.wav"))>1 && !File.Exists(Path.Combine(output,"2.wav")),"only checked online actors produce audio");form.Close();
        }
    }
    static async Task Edge(string data)
    {
        var settings=ClassicOnlineTtsSettings.Load("edge");await ClassicOnlineTts.InstallEdgeAsync(settings,CancellationToken.None,new Log());
        using(var engine=new ClassicOnlineTts("edge",settings)) {
            var voices=await engine.RefreshVoicesAsync(CancellationToken.None);Check(voices.Length>100 && voices.Any(v=>v.Id=="ru-RU-DmitryNeural"),"live Edge voice discovery");
            await engine.SpeakAsync("Today we are testing clear English speech.","en-US-GuyNeural",Path.Combine(data,"edge-english.wav"),CancellationToken.None);
            await engine.SpeakAsync("Сегодня мы проверяем озвучку русских субтитров.","ru-RU-DmitryNeural",Path.Combine(data,"edge-russian.wav"),CancellationToken.None);
            Check(ClassicVoiceAssets.WaveSeconds(Path.Combine(data,"edge-english.wav"))>1 && ClassicVoiceAssets.WaveSeconds(Path.Combine(data,"edge-russian.wav"))>1,"real Edge English and Russian audio");
            using(var cancel=new CancellationTokenSource(20)) {try{await engine.SpeakAsync("Cancelled","en-US-GuyNeural",Path.Combine(data,"edge-cancel.wav"),cancel.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){}Check(!File.Exists(Path.Combine(data,"edge-cancel.wav")),"Edge cancellation leaves no output");}
        }
    }
    static async Task Zonos(string data)
    {
        var settings=ClassicLocalTtsSettings.Load("zonos");settings.Variant="cuda13";settings.Language="en-us";settings.Voice="Default";settings.Save("zonos");
        using(var engine=new ClassicLocalTts("zonos",settings))using(var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10))) {
            await engine.SpeakAsync("Today we are testing clear English speech.","Default",Path.Combine(data,"zonos-english.wav"),timeout.Token,new Log());
            settings.Language="ja";await engine.SpeakAsync("こんにちは。今日は音声のテストです。","Default",Path.Combine(data,"zonos-japanese.wav"),timeout.Token,new Log());
            Check(ClassicVoiceAssets.WaveSeconds(Path.Combine(data,"zonos-english.wav"))>1 && ClassicVoiceAssets.WaveSeconds(Path.Combine(data,"zonos-japanese.wav"))>1,"Zonos real synthesis and startup language switch");
            using(var cancel=new CancellationTokenSource(20)){try{await engine.SpeakAsync("Cancelled","Default",Path.Combine(data,"zonos-cancel.wav"),cancel.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){}Check(!File.Exists(Path.Combine(data,"zonos-cancel.wav")),"Zonos cancellation leaves no output");}
        }
    }
    sealed class Log:IProgress<SpeechProgress>{public void Report(SpeechProgress p){Console.WriteLine(p.Message);}}
    sealed class Mock:HttpMessageHandler
    {
        readonly string Id;readonly byte[] Audio;public bool Fail;public int Tokens;
        public Mock(string id,byte[] audio){Id=id;Audio=audio;}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel) {
            if(Fail)return new HttpResponseMessage(HttpStatusCode.Forbidden){Content=new StringContent("fixture-secret should never be shown")};
            if(request.RequestUri.Host=="oauth2.googleapis.com"){Tokens++;var assertion=await request.Content.ReadAsStringAsync();Check(assertion.Contains("assertion="),"Google OAuth assertion sent");return Json("{\"access_token\":\"fixture-token\",\"expires_in\":3600}");}
            Check(request.Headers.Authorization?.Parameter==(Id=="google"?"fixture-token":"fixture-secret"),"provider authentication header");
            if(request.Method==HttpMethod.Get)return Json("{\"items\":[{\"id\":\"refreshed\",\"name\":\"Refreshed voice\",\"languages\":[\"en_us\"]}]}");
            var body=JObject.Parse(await request.Content.ReadAsStringAsync());Check(Id=="google"?body["voice"]!=null:body["voice_id"]!=null,"actual provider request protocol");
            return Json(new JObject{[Id=="google"?"audioContent":"audio_data"]=Convert.ToBase64String(Audio)}.ToString());
        }
        static HttpResponseMessage Json(string value)=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(value,Encoding.UTF8,"application/json")};
    }
    sealed class Slow:HttpMessageHandler{protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){await Task.Delay(5000,cancel);throw new Exception("cancel ignored");}}
    static byte[] Wave(){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(48036);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(24000);w.Write(48000);w.Write((short)2);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(48000);for(int i=0;i<24000;i++)w.Write((short)(Math.Sin(i*2*Math.PI*440/24000)*5000));return s.ToArray();}}
    static byte[] Decode(string value){value=value.Replace('-','+').Replace('_','/');return Convert.FromBase64String(value.PadRight((value.Length+3)/4*4,'='));}
    static byte[] Der(int tag,params byte[][] values){var data=values.SelectMany(v=>v).ToArray();var length=data.Length<128?new[]{(byte)data.Length}:data.Length<256?new[]{(byte)0x81,(byte)data.Length}:new[]{(byte)0x82,(byte)(data.Length>>8),(byte)data.Length};return new[]{(byte)tag}.Concat(length).Concat(data).ToArray();}
    static byte[] Int(byte[] value)=>Der(2,(value[0]>=128?new byte[]{0}:new byte[0]).Concat(value).ToArray());
    static string Pem(RSAParameters p){var key=Der(0x30,Der(2,new byte[]{0}),Int(p.Modulus),Int(p.Exponent),Int(p.D),Int(p.P),Int(p.Q),Int(p.DP),Int(p.DQ),Int(p.InverseQ));var pkcs=Der(0x30,Der(2,new byte[]{0}),Der(0x30,Der(6,new byte[]{42,134,72,134,247,13,1,1,1}),Der(5,new byte[0])),Der(4,key));return "-----BEGIN PRIVATE KEY-----\n"+Convert.ToBase64String(pkcs)+"\n-----END PRIVATE KEY-----";}
}
