using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Forms
{
    public static class ClassicAiPrompts
    {
        public static string FileName => Path.Combine(Configuration.DataDirectory,"ClassicAI","prompts.json");
        public static Dictionary<string,string> Load(){try{return JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(FileName))??new Dictionary<string,string>();}catch(IOException){}catch(JsonException){}return new Dictionary<string,string>();}
        public static void Save(string name,string prompt)
        {
            if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(prompt))throw new ArgumentException("Enter a template name and instruction.");
            var templates=Load();templates[name.Trim()]=prompt;Directory.CreateDirectory(Path.GetDirectoryName(FileName));var temp=FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,JsonConvert.SerializeObject(templates,Formatting.Indented));if(File.Exists(FileName))File.Replace(temp,FileName,null);else File.Move(temp,FileName);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    public sealed class ClassicAiPromptForm : ClassicServiceSettingsForm
    {
        public string TemplateName { get; private set; }
        public string Prompt { get; private set; }
        public ClassicAiPromptForm(string name,string prompt):base("AI: template")
        {
            Add("name","Name",name);Add("prompt","Instruction",prompt,multiline:true);Fields["prompt"].Height=300;
            SaveWith(()=>{TemplateName=Fields["name"].Text.Trim();Prompt=Fields["prompt"].Text;ClassicAiPrompts.Save(TemplateName,Prompt);});
        }
    }
}
