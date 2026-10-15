using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicQwenReference
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Consent { get; set; }
        public string Attribution { get; set; }="";
        public override string ToString() => Name;
        public static string Folder => Path.Combine(ClassicQwenTtsSettings.Root,"voices");
        public static List<ClassicQwenReference> List(bool requireTranscript = true)
        {
            var result=new List<ClassicQwenReference>();if(!Directory.Exists(Folder))return result;
            foreach(var file in Directory.GetFiles(Folder,"voice_*.json"))
            {
                try {var voice=JsonConvert.DeserializeObject<ClassicQwenReference>(File.ReadAllText(file));if(voice==null)continue;Validate(voice.Id, requireTranscript);result.Add(voice);}
                catch(IOException){}catch(JsonException){}catch(ArgumentException){}catch(InvalidDataException){}
            }
            return result;
        }
        public static void Validate(string id, bool requireTranscript = true)
        {
            if(id==null || !Regex.IsMatch(id,@"\Avoice_[a-f0-9]{32}\z"))throw new ArgumentException("Select an imported voice reference");
            var root=Path.Combine(Folder,id);
            if(!File.Exists(root+".wav") || !File.Exists(root+".txt") || requireTranscript && string.IsNullOrWhiteSpace(File.ReadAllText(root+".txt")))throw new InvalidDataException("Voice reference needs its recording and transcript");
            if(!File.Exists(root+".json") || JsonConvert.DeserializeObject<ClassicQwenReference>(File.ReadAllText(root+".json"))?.Consent!=true)throw new InvalidDataException("Voice reference permission was not confirmed");
        }
        public static async Task<ClassicQwenReference> ImportAsync(string input,string name,string transcript,double start,double duration,bool consent,CancellationToken cancel, bool requireTranscript = true)
        {
            if(!consent)throw new ArgumentException("Confirm permission to use the reference voice");
            if(string.IsNullOrWhiteSpace(name) || requireTranscript && string.IsNullOrWhiteSpace(transcript))throw new ArgumentException("Enter a voice name and the exact reference transcript");
            if(!File.Exists(input) || start<0 || duration<1 || duration>30)throw new ArgumentException("Select a recording and a 1–30 second reference range");
            Directory.CreateDirectory(Folder);var id="voice_"+Guid.NewGuid().ToString("N");var root=Path.Combine(Folder,id);
            try
            {
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    timeout.CancelAfter(TimeSpan.FromMinutes(2));
                    var args="-nostdin -y -ss "+start.ToString(System.Globalization.CultureInfo.InvariantCulture)+" -i "+ClassicSpeechRunner.Quote(input)+" -t "+duration.ToString(System.Globalization.CultureInfo.InvariantCulture)+" -vn -ar 24000 -ac 1 -c:a pcm_s16le "+ClassicSpeechRunner.Quote(root+".wav");
                    await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,args){WorkingDirectory=Folder},timeout.Token,null).ConfigureAwait(false);
                }
                if(!File.Exists(root+".wav") || new FileInfo(root+".wav").Length<48000)throw new InvalidDataException("Reference recording is empty or too short");
                cancel.ThrowIfCancellationRequested();
                File.WriteAllText(root+".txt",(transcript ?? "").Trim(),new UTF8Encoding(false));
                var reference=new ClassicQwenReference{Id=id,Name=name.Trim(),Consent=true};
                File.WriteAllText(root+".json",JsonConvert.SerializeObject(reference));return reference;
            }
            catch {foreach(var ext in new[]{".wav",".txt",".json"})if(File.Exists(root+ext))File.Delete(root+ext);throw;}
        }
    }
}
