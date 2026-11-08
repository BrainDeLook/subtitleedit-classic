using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;
class BatchPluginFixture
{
 static void Main(string[] args)
 {
    var request=JObject.Parse(File.ReadAllText(args[0]));
    var subtitle=(JObject)request["subtitle"];var lines=(JArray)subtitle["paragraphs"];
    var mode=lines.Count==0?"":(string)lines[0]["text"];
    if(mode=="slow")Thread.Sleep(10000);
    var response=new JObject{["apiVersion"]=1,["status"]=mode=="cancel"?"cancelled":"ok"};
    if(mode=="invalid")lines[0]["startMs"]=-5;else foreach(JObject line in lines)line["text"]=(string)line["text"]+" plugin";
    var settings=request["settings"] as JObject;
    response["subtitle"]=subtitle;response["settings"]=new JObject{["count"]=((int?)settings?["count"]??0)+1};response["settingsVersion"]=2;
    if(request["rules"]==null || request["themeColors"]==null)throw new Exception("Missing plugin context");
    File.WriteAllText((string)request["responseFilePath"],response.ToString());
 }
}
