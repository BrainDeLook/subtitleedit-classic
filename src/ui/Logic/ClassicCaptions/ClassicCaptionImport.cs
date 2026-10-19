using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea608;
namespace Nikse.SubtitleEdit.Logic.ClassicCaptions
{
    public static class ClassicCaptionImport
    {
        public static async Task<SortedDictionary<int,List<Paragraph>>> Extract(string input,CancellationToken token)
        {
            var folder=Path.Combine(Path.GetTempPath(),"SubtitleEdit-cc-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var info=await ClassicMediaJobs.Probe(input,token);var video=info.Streams.FirstOrDefault(s=>s.Type=="video");var codec=video?.Codec=="h264"?CcVideoCodec.H264:video?.Codec=="hevc"?CcVideoCodec.H265:video?.Codec=="mpeg2video" || video?.Codec=="mpeg1video"?CcVideoCodec.Mpeg2:CcVideoCodec.Unknown;
                if(codec==CcVideoCodec.Unknown)throw new InvalidDataException("Caption extraction supports MPEG-1/2, H.264 and H.265 video streams.");
                var stream=Path.Combine(folder,"video.ts");await ClassicMediaJobs.Render("-i "+ClassicMediaJobs.Q(input)+" -map 0:"+video.Index+" -c:v copy -an -sn -streamid 0:256 -muxdelay 0 -muxpreload 0 -f mpegts",stream,token);
                var start=await ClassicMediaJobs.Capture(Path.Combine(Path.GetDirectoryName(Configuration.Settings.General.FFmpegLocation),"ffprobe.exe"),"-v error -select_streams v:0 -show_entries stream=start_time -of csv=p=0 "+ClassicMediaJobs.Q(stream),token);
                double.TryParse(start.Trim().Split(new[]{'\r','\n',','},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),NumberStyles.Float,CultureInfo.InvariantCulture,out var first);
                return await Task.Run(()=>
                {
                    var extractor=new ClosedCaptionExtractor(codec);using(var file=File.OpenRead(stream)){var packet=new byte[188];while(true){token.ThrowIfCancellationRequested();var read=0;while(read<188){var n=file.Read(packet,read,188-read);if(n==0)break;read+=n;}if(read==0)break;if(read!=188 || packet[0]!=0x47)throw new InvalidDataException("Invalid transport stream packet.");var pid=((packet[1]&31)<<8)|packet[2];if(pid==256)extractor.AddPacket(packet);}}
                    return extractor.Finish((long)Math.Round(first*1000));
                },token);
            }
            finally{try{Directory.Delete(folder,true);}catch(IOException){}}
        }
    }
}
