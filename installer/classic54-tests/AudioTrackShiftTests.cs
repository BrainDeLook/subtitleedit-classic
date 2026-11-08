using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
namespace Classic54Tests
{
 public class AudioTrackShiftTests
 {
  [Theory][InlineData(0.5)][InlineData(-0.5)] public void ShiftOnlySelectedTrackWithoutMixing(double offset)
  {
   var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"media-shift-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   var input=Path.Combine(folder,"input.mkv");var output=Path.Combine(folder,"output.mkv");var cancel=CancellationToken.None;
   try
   {
    ClassicMediaJobs.Render("-f lavfi -i testsrc2=size=160x90:rate=25:duration=3 -f lavfi -i sine=frequency=440:duration=3 -f lavfi -i sine=frequency=880:duration=3 -map 0:v -map 1:a -map 2:a -c:v libx264 -preset ultrafast -c:a aac -metadata:s:a:0 language=tur -metadata:s:a:1 language=eng -metadata:s:a:1 title=English",input,cancel).GetAwaiter().GetResult();
    var media=ClassicMediaJobs.Probe(input,cancel).GetAwaiter().GetResult();
    ClassicVideoTools.ShiftAudioTrack(input,output,media,2,offset,null,cancel).GetAwaiter().GetResult();
    var after=ClassicMediaJobs.Probe(output,cancel).GetAwaiter().GetResult();Assert.Equal(3,after.Streams.Count);Assert.Equal("tur",after.Streams[1].Language);Assert.Equal("eng",after.Streams[2].Language);Assert.Equal("English",after.Streams[2].Title);
    var args="-v error -show_entries stream=index,start_time -of json ";
    var beforeData=JObject.Parse(ClassicMediaJobs.Capture(ClassicMediaJobs.ProbePath,args+ClassicMediaJobs.Q(input),cancel).GetAwaiter().GetResult());
    var afterData=JObject.Parse(ClassicMediaJobs.Capture(ClassicMediaJobs.ProbePath,args+ClassicMediaJobs.Q(output),cancel).GetAwaiter().GetResult());
    for(int i=0;i<3;i++)
    {
     var expected=(double)beforeData["streams"][i]["start_time"]+(i==2?offset:0);
     Assert.Equal(expected,(double)afterData["streams"][i]["start_time"],.025);
     var hashArgs="-v error -i ";var tail=" -map 0:"+i+" -c copy -f hash -hash sha256 -";
     var beforeHash=ClassicMediaJobs.Capture(Configuration.Settings.General.FFmpegLocation,hashArgs+ClassicMediaJobs.Q(input)+tail,cancel).GetAwaiter().GetResult();
     var afterHash=ClassicMediaJobs.Capture(Configuration.Settings.General.FFmpegLocation,hashArgs+ClassicMediaJobs.Q(output)+tail,cancel).GetAwaiter().GetResult();Assert.Equal(beforeHash,afterHash);
    }
   }
   finally {Directory.Delete(folder,true);}
  }
 }
}
