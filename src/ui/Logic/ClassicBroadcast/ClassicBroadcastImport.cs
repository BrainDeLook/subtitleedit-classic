using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicBroadcast.Transport;
using Nikse.SubtitleEdit.Logic.ClassicBroadcast.Formats;
using Nikse.SubtitleEdit.Logic.ClassicCaptions;
using Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea608;

namespace Nikse.SubtitleEdit.Logic.ClassicBroadcast
{
    public sealed class BroadcastTrack
    {
        public string Name { get; set; }
        public List<Paragraph> Paragraphs { get; set; }
    }

    public static class ClassicBroadcastImport
    {
        public static async Task<List<BroadcastTrack>> Extract(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var ext=Path.GetExtension(path).ToLowerInvariant();
            if(ext==".mxf")return await Task.Run(()=>
            {
                var parser=new Mxf.MxfParser(path,token);
                if(!parser.IsValid)throw new InvalidDataException("Invalid MXF file.");
                return parser.ClosedCaptionTracks.Select(p=>new BroadcastTrack{Name="MXF VANC — "+ClosedCaptionExtractor.GetTrackName(p.Key),Paragraphs=p.Value}).ToList();
            },token);
            if(new[]{".1hd",".2hd",".1sd",".2sd"}.Contains(ext))return await Task.Run(()=>
            {
                var format=new AribB36();var sub=new Subtitle();
                if(!format.IsMine(new List<string>(),path))throw new InvalidDataException("Invalid ARIB B36 file.");
                format.LoadSubtitle(sub,new List<string>(),path);token.ThrowIfCancellationRequested();
                return sub.Paragraphs.Count==0?new List<BroadcastTrack>():new List<BroadcastTrack>{new BroadcastTrack{Name="ARIB B36",Paragraphs=sub.Paragraphs}};
            },token);
            if(new[]{".ts",".m2ts",".mts",".m2t",".rec"}.Contains(ext))
            {
                var arib=await Task.Run(()=>ExtractArib(path,token),token);
                if(arib.Count>0)return arib;
            }
            var cc=await ClassicCaptionImport.Extract(path,token);
            return cc.Select(p=>new BroadcastTrack{Name=ClosedCaptionExtractor.GetTrackName(p.Key),Paragraphs=p.Value}).ToList();
        }

        private sealed class PesState
        {
            public MemoryStream Data=new MemoryStream();
            public int Counter=-1;
            public long LastPts=-1,Wrap;
            public AribCaptionParser Parser;
        }

        // Bounded streaming PES assembly; video and audio essence are never retained.
        public static List<BroadcastTrack> ExtractArib(string path,CancellationToken token)
        {
            var tables=new ProgramMapTableParser();tables.Parse(path);
            var profiles=tables.GetAribDataComponentIds();
            var states=new Dictionary<int,PesState>();var clocks=new Dictionary<int,ulong>();
            var pcrClocks=new Dictionary<int,ulong>();
            var streamTypes=tables.GetStreamTypes();
            var cea=new Dictionary<int,ClosedCaptionExtractor>();
            using(var stream=File.OpenRead(path))
            {
                var head=new byte[820];var n=stream.Read(head,0,head.Length);int stride=0,offset=0;
                foreach(var size in new[]{188,192,204})
                    for(int i=0;i<size && i+size*2<n;i++)
                        if(head[i]==0x47 && head[i+size]==0x47 && head[i+size*2]==0x47){stride=size;offset=i;break;}
                if(stride==0)throw new InvalidDataException("Invalid transport stream packet framing.");
                stream.Position=offset;var bytes=new byte[188];
                while(stream.Position+188<=stream.Length)
                {
                    token.ThrowIfCancellationRequested();
                    if(stream.ReadFully(bytes,0,188)!=188)break;
                    if(bytes[0]!=0x47)throw new InvalidDataException("Lost transport stream synchronization.");
                    var pid=Packet.PeekPacketId(bytes);
                    if(Packet.TryPeekVideoPresentationTimestamp(bytes,out var pts) && !clocks.ContainsKey(pid))clocks.Add(pid,pts);
                    if(!cea.TryGetValue(pid,out var extractor) && streamTypes.TryGetValue(pid,out var type) && ClosedCaptionExtractor.IsVideoStreamType(type,out var codec))
                    {extractor=new ClosedCaptionExtractor(codec);cea.Add(pid,extractor);}
                    extractor?.AddPacket(bytes);
                    if(Packet.TryPeekProgramClockReference(bytes,out var pcr) && !pcrClocks.ContainsKey(pid))pcrClocks.Add(pid,pcr);
                    var start=(bytes[1]&0x40)!=0;var af=(bytes[3]>>4)&3;
                    var payload=af==1?4:af==3?5+bytes[4]:188;
                    if(payload>188)throw new InvalidDataException("Invalid TS adaptation field length.");
                    if(payload<188 && (bytes[1]&0x80)==0 && (bytes[3]&0xc0)==0)
                    {
                        states.TryGetValue(pid,out var state);
                        if(start)
                        {
                            if(state!=null){Feed(state,profiles.ContainsKey(pid)&&profiles[pid]==0x12);state.Data.SetLength(0);}
                            if(payload+4<=188 && bytes[payload]==0 && bytes[payload+1]==0 && bytes[payload+2]==1 && bytes[payload+3]==0xbd)
                            {
                                if(state==null){state=new PesState();states.Add(pid,state);}
                            }
                            else state=null;
                        }
                        if(state!=null)
                        {
                            var counter=bytes[3]&15;
                            if(!start && state.Counter==counter){stream.Position+=stride-188;continue;}
                            if(!start && state.Counter>=0 && counter!=((state.Counter+1)&15))state.Data.SetLength(0);
                            state.Counter=counter;
                            if(start || state.Data.Length>0)
                            {
                                if(state.Data.Length+188-payload>256*1024)state.Data.SetLength(0);
                                else state.Data.Write(bytes,payload,188-payload);
                            }
                        }
                    }
                    stream.Position+=stride-188;
                }
            }
            var result=new List<BroadcastTrack>();
            foreach(var pair in cea)
            {
                token.ThrowIfCancellationRequested();
                var offset=clocks.TryGetValue(pair.Key,out var clock)?(long)(clock/90):0;
                foreach(var track in pair.Value.Finish(offset))
                    if(track.Value.Count>0)result.Add(new BroadcastTrack{Name=ClosedCaptionExtractor.GetTrackName(track.Key)+" — PID "+pair.Key,Paragraphs=track.Value});
            }
            foreach(var pair in states)
            {
                token.ThrowIfCancellationRequested();var state=pair.Value;Feed(state,profiles.ContainsKey(pair.Key)&&profiles[pair.Key]==0x12);
                if(state.Parser==null)continue;state.Parser.Flush();
                var clockPid=tables.GetProgramVideoPacketId(pair.Key);
                var clockRef=tables.GetProgramClockReferencePacketId(pair.Key);
                var offset=clockPid.HasValue && clocks.TryGetValue(clockPid.Value,out var clock)?clock/90.0:
                    clockRef.HasValue && pcrClocks.TryGetValue(clockRef.Value,out var pcr)?pcr/90.0:clocks.Count==1?clocks.Values.First()/90.0:0;
                foreach(var language in state.Parser.ParagraphsByLanguage)
                {
                    foreach(var p in language.Value)
                    {
                        var time=p.StartTime.TotalMilliseconds-offset;
                        var wrap=(1L<<33)/90.0;
                        if(time < -wrap/2)time+=wrap;
                        var duration=p.DurationTotalMilliseconds;
                        p.StartTime.TotalMilliseconds=Math.Max(0,time);p.EndTime.TotalMilliseconds=Math.Max(0,time+duration);
                    }
                    state.Parser.LanguageCodes.TryGetValue(language.Key,out var code);
                    if(language.Value.Count>0)result.Add(new BroadcastTrack{Name="ARIB B24 — PID "+pair.Key+" — "+(code??"language "+(language.Key+1)),Paragraphs=language.Value});
                }
            }
            return result;
        }

        private static void Feed(PesState state,bool profileC)
        {
            var bytes=state.Data.ToArray();if(bytes.Length<14)return;
            var pes=new PacketizedElementaryStream(bytes,0);var begin=9+pes.HeaderDataLength;
            if(pes.Length>0 && pes.Length+6>bytes.Length)return;
            var end=pes.Length>0?Math.Min(bytes.Length,pes.Length+6):bytes.Length;
            if(!pes.PresentationTimestamp.HasValue || begin>=end || begin+2>=bytes.Length || bytes[begin]!=0x80 && bytes[begin]!=0x81 || bytes[begin+1]!=0xff)return;
            if(state.Parser==null)state.Parser=new AribCaptionParser(profileC?AribB24Decoder.AribProfile.ProfileC:AribB24Decoder.AribProfile.ProfileA);
            var raw=(long)pes.PresentationTimestamp.Value;
            if(state.LastPts>=0 && raw<state.LastPts && state.LastPts-raw>(1L<<32))state.Wrap+=1L<<33;
            state.LastPts=raw;
            var data=new byte[end-begin];Array.Copy(bytes,begin,data,0,data.Length);
            state.Parser.ParsePesPayload(data,(raw+state.Wrap)/90.0);
        }
    }
}
