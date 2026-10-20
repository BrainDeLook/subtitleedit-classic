using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Logic.ClassicBroadcast;
using Nikse.SubtitleEdit.Logic.ClassicBroadcast.Formats;
using AribB24Decoder = Nikse.SubtitleEdit.Logic.ClassicBroadcast.Formats.AribB24Decoder;
using Nikse.SubtitleEdit.Logic.ClassicBroadcast.Transport;
using Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea708;
class TestClassicBroadcast
{
    static int checks;const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Check(bool value,string text){if(!value)throw new Exception(text);checks++;Console.WriteLine("PASS: "+text);}
    static bool Hidden(ListView list,int index){try{return list.GetItemRect(index).Height==0;}catch(ArgumentException){return true;}}
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethods(F).Single(m=>m.Name==n && m.GetParameters().Length==a.Length && m.GetParameters().Select((p,i)=>a[i]==null || p.ParameterType.IsInstanceOfType(a[i])).All(v=>v)).Invoke(o,a);
    static byte[] Join(params byte[][] arrays)=>arrays.SelectMany(a=>a).ToArray();
    static byte[] Text(string text)=>Join(new byte[]{0x0e,0x89},Encoding.ASCII.GetBytes(text));
    static byte[] Group(int group,byte[] body)=>Join(new byte[]{0x80,0xff,0,(byte)(group<<2),0,0,(byte)(body.Length>>8),(byte)body.Length},body,new byte[2]);
    static byte[] Statement(int language,string text)
    {
        var data=Text(text);var unit=Join(new byte[]{0x1f,0x20,0,0,(byte)data.Length},data);
        return Group(language+1,Join(new byte[]{0,0,0,(byte)unit.Length},unit));
    }
    static byte[] Management()=>Group(0,new byte[]{0,2,0,(byte)'j',(byte)'p',(byte)'n',0,32,(byte)'e',(byte)'n',(byte)'g',0});
    static byte[] Pes(byte[] data,long ms)
    {
        long p=ms*90;int length=data.Length+8;
        return Join(new byte[]{0,0,1,0xbd,(byte)(length>>8),(byte)length,0x80,0x80,5,(byte)(0x21|((p>>29)&14)),(byte)(p>>22),(byte)(((p>>14)&254)|1),(byte)(p>>7),(byte)(((p<<1)&254)|1)},data);
    }
    static byte[] Packet(int pid,int counter,bool start,byte[] data)
    {
        var bytes=Enumerable.Repeat((byte)255,188).ToArray();bytes[0]=0x47;bytes[1]=(byte)((pid>>8)|(start?0x40:0));bytes[2]=(byte)pid;
        int pad=183-data.Length;bytes[3]=(byte)(0x30|counter);bytes[4]=(byte)pad;if(pad>0)bytes[5]=0;data.CopyTo(bytes,5+pad);return bytes;
    }
    static void Klv(Stream stream,byte[] key,byte[] data)
    {
        stream.Write(key,0,16);if(data.Length<128)stream.WriteByte((byte)data.Length);else{stream.WriteByte(0x82);stream.WriteByte((byte)(data.Length>>8));stream.WriteByte((byte)data.Length);}stream.Write(data,0,data.Length);
    }
    static byte[] Anc(byte a,byte b,int sequence)
    {
        var smpte=new Smpte291M(sequence,20,new byte[0]);
        smpte.CcDataSectionCcData.CcData[0].Data1=a;smpte.CcDataSectionCcData.CcData[0].Data2=b;
        var packet=smpte.GetBytes();var header=new byte[16];header[1]=1;header[5]=4;header[11]=(byte)packet.Length;header[15]=1;
        return Join(header,packet,new byte[(4-packet.Length%4)%4]);
    }
    private static byte[] MakeAribB36(params (string Start, string End, string Text)[] cues)
    {
        const int blockSize = 256;
        var buffer = new byte[Math.Max(3072, blockSize * (3 + cues.Length) + blockSize)];
        Encoding.ASCII.GetBytes("DCAPTION").CopyTo(buffer, 0);

        for (var i = 0; i < cues.Length; i++)
        {
            var block = blockSize * (3 + i);
            buffer[block + 2] = 0;
            buffer[block + 3] = 250; // block length
            buffer[block + 4] = 0x2a; // page management information

            var index = block + 5;
            const int pageManagementLength = 120;
            buffer[index++] = 0;
            buffer[index++] = pageManagementLength;
            Encoding.ASCII.GetBytes("T").CopyTo(buffer, index + 9); // timing unit: milliseconds
            Encoding.ASCII.GetBytes(cues[i].Start).CopyTo(buffer, index + 10); // HHMMSSmmm
            Encoding.ASCII.GetBytes(cues[i].End).CopyTo(buffer, index + 19);
            index += pageManagementLength;

            buffer[index++] = 0x3a; // caption text page management data
            const int captionTextPageManagementLength = 20;
            buffer[index++] = 0;
            buffer[index++] = captionTextPageManagementLength;
            Encoding.ASCII.GetBytes("eng").CopyTo(buffer, index + 14);
            index += captionTextPageManagementLength;

            buffer[index++] = 0x4a; // caption text data
            var textData = new List<byte> { 0x9b }; // CSI
            textData.AddRange(Encoding.ASCII.GetBytes("170;30 a"));
            textData.Add(0x0e); // LS1: alphanumeric set into GL
            textData.Add(0x89); // MSZ: middle size, so the letters decode half width
            textData.AddRange(Encoding.ASCII.GetBytes(cues[i].Text));
            var unitLength = 5 + textData.Count;
            buffer[index++] = 0;
            buffer[index++] = (byte)(15 + unitLength);
            buffer[index + 14] = (byte)unitLength; // data unit loop length
            var unit = index + 15;
            buffer[unit++] = 0x1f; // unit separator
            buffer[unit++] = 0x20; // statement body (text)
            buffer[unit++] = 0;
            buffer[unit++] = 0;
            buffer[unit++] = (byte)textData.Count;
            textData.ToArray().CopyTo(buffer, unit);
        }

        return buffer;
    }


    static async Task Run(string root)
    {
        var ascii=Text("Hello");Check(AribB24Decoder.AribToString(ascii,0,ascii.Length)=="Hello","ARIB alphanumeric invocation and size decode");
        foreach(var ext in new[]{".1hd",".2hd",".1sd",".2sd"})
        {
            var path=Path.Combine(root,"caption"+ext);File.WriteAllBytes(path,MakeAribB36(("000001000","000003000","Hello"),("000004000","000006000","World")));
            var tracks=await ClassicBroadcastImport.Extract(path,CancellationToken.None);
            Check(tracks.Count==1 && tracks[0].Paragraphs.Count==2 && tracks[0].Paragraphs[0].Text=="Hello" && tracks[0].Paragraphs[1].StartTime.TotalMilliseconds==4000,"ARIB B36 file import "+ext);
        }
        var japanese=new byte[]{0x24,0x22,0x24,0x24};Check(AribB24Decoder.AribToString(japanese,0,4)=="あい","ARIB Japanese JIS kanji set decode");
        var parser=new AribCaptionParser(AribB24Decoder.AribProfile.ProfileA);parser.ParsePesPayload(Management(),1000);parser.ParsePesPayload(Statement(0,"Hello"),2000);parser.ParsePesPayload(Statement(1,"World"),2500);parser.ParsePesPayload(Statement(0,""),4000);parser.Flush();
        Check(parser.LanguageCodes[0]=="jpn" && parser.LanguageCodes[1]=="eng" && parser.ParagraphsByLanguage.Count==2,"ARIB language tracks stay separate");
        Check(parser.ParagraphsByLanguage[0][0].Text=="Hello" && parser.ParagraphsByLanguage[0][0].EndTime.TotalMilliseconds==4000,"ARIB clear statement closes the cue");
        for(int i=0;i<40;i++)parser.ParsePesPayload(new byte[i],0);Check(true,"Truncated ARIB headers do not crash");
        foreach(int stride in new[]{188,192,204})
        {
            var path=Path.Combine(root,"arib-"+stride+".ts");
            using(var output=File.Create(path))
            {
                int counter=0;foreach(var data in new[]{Management(),Statement(0,"Hello"),Statement(1,"World"),Statement(0,"")})
                {
                    if(stride==192)output.Write(new byte[4],0,4);
                    var packet=Packet(300,counter,true,Pes(data,1000+counter*1000));output.Write(packet,0,188);if(stride==204)output.Write(new byte[16],0,16);counter++;
                }
            }
            var tracks=ClassicBroadcastImport.ExtractArib(path,CancellationToken.None);
            Check(tracks.Count==2 && tracks[0].Name.Contains("PID 300") && tracks[0].Name.Contains("jpn") && tracks[1].Name.Contains("eng"),"ARIB TS framing and names "+stride);
            Check(tracks[0].Paragraphs[0].Text=="Hello" && tracks[1].Paragraphs[0].Text=="World","Actual PES extraction "+stride);
        }
        var mxf=Path.Combine(root,"vanc.mxf");
        var clocked=Path.Combine(root,"clocked.ts");
        using(var output=File.Create(clocked))
        {
            var pat=new byte[]{0,0,0xb0,13,0,1,0xc1,0,0,0,1,0xe0,100,0,0,0,0};
            var pmt=new byte[]{0,2,0xb0,27,0,1,0xc1,0,0,0xe1,0,0xf0,0,0x1b,0xe1,0,0xf0,0,6,0xe1,44,0xf0,4,0xfd,2,0,8,0,0,0,0};
            var video=Pes(new byte[]{0},90000);video[3]=0xe0;
            foreach(var packet in new[]{Packet(0,0,true,pat),Packet(100,0,true,pmt),Packet(256,0,true,video),Packet(300,0,true,Pes(Management(),90000)),Packet(300,1,true,Pes(Statement(0,"Clock"),92000)),Packet(300,2,true,Pes(Statement(0,""),94000))})output.Write(packet,0,188);
        }
        var clockedTracks=ClassicBroadcastImport.ExtractArib(clocked,CancellationToken.None);
        Check(clockedTracks.Count==1 && clockedTracks[0].Paragraphs[0].StartTime.TotalMilliseconds==2000,"ARIB PMT ties caption PTS to the program video clock");
        using(var stream=File.Create(mxf))
        {
            Klv(stream,new byte[]{6,14,43,52,2,5,1,1,13,1,2,1,1,2,4,0},new byte[100]);
            Klv(stream,new byte[]{6,14,43,52,2,83,1,1,13,1,1,1,1,1,1,0},new byte[]{0x30,1,0,8,0,0,0,25,0,0,0,1});
            var key=new byte[]{6,14,43,52,1,2,1,1,13,1,3,1,0x17,1,2,1};int count=0;
            foreach(var pair in new[]{new byte[]{0x14,0x20},new byte[]{0x48,0x69},new byte[]{0x14,0x2f},new byte[]{0x14,0x2c}})Klv(stream,key,Anc(pair[0],pair[1],count++));
        }
        var imported=await ClassicBroadcastImport.Extract(mxf,CancellationToken.None);
        Check(imported.Count>0 && imported.Any(t=>t.Paragraphs.Any(p=>p.Text.Contains("Hi"))),"MXF file VANC yields actual CEA text");
        Check(imported[0].Name.Contains("MXF VANC") && imported[0].Paragraphs[0].StartTime.TotalMilliseconds==80,"MXF channel name and metadata frame rate");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await ClassicBroadcastImport.Extract(mxf,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"Import cancellation is observed");}}
        var bad=Path.Combine(root,"bad.mxf");File.WriteAllBytes(bad,File.ReadAllBytes(mxf).Take(140).ToArray());
        try{new Nikse.SubtitleEdit.Logic.ClassicBroadcast.Mxf.MxfParser(bad);throw new Exception("Truncated MXF accepted");}catch(InvalidDataException){Check(true,"Truncated KLV is rejected without looping");}
        using(var main=new Nikse.SubtitleEdit.Forms.Main())
        {
            main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.Show();await Task.Delay(100);
            var sub=new Subtitle{Header=AdvancedSubStationAlpha.DefaultHeader};for(int i=0;i<4;i++)sub.Paragraphs.Add(new Paragraph("Cue "+i,i*2000,i*2000+1000){Actor=i%2==0?"Alice":"Bob",Extra="Default"});sub.Renumber();
            Call(main,"SetCurrentFormat",new AdvancedSubStationAlpha().FriendlyName);Set(main,"_subtitle",sub);Set(main,"_undoIndex",-1);var list=(SubtitleListView)Field(main,"SubtitleListview1");Call(list,"Fill",sub,null);list.Items[0].Selected=true;await Task.Delay(40);
            Call(main,"ApplyClassicAssRowFilter",new Predicate<Paragraph>(p=>p.Actor=="Bob"));await Task.Delay(80);
            Check(list.Items.Count==4 && list.AssVisibleRowCount==2,"Main grid filter preserves stable document indices");
            var bar=(ToolStrip)Field(main,"_classicAssFilterBar");
            Check(bar.Visible && list.Top>=bar.Bottom,"Filter has a visible clear button above the main grid");
            using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"filter.png"));}
            Check(Hidden(list,0) && !Hidden(list,1),"Native main grid actually hides unmatched rows");
            list.SelectIndexAndEnsureVisible(3,true);Check(list.Items[3].Selected,"Native navigation can select a later filtered cue");
            list.SelectIndexAndEnsureVisibleFaster(1);Check(list.Items[1].Selected,"Fast navigation skips hidden rows safely");
            list.Items[1].Selected=true;await Task.Delay(30);var edit=Field(main,"textBoxListViewText");edit.GetType().GetProperty("Text").SetValue(edit,"Edited Bob");
            Check(sub.Paragraphs[1].Text=="Edited Bob" && sub.Paragraphs[0].Text=="Cue 0","Filtered editing changes the correct original cue");
            Call(list,"Fill",sub,null);await Task.Delay(70);Check(list.AssVisibleRowCount==2 && Hidden(list,0),"Filter survives list rebuilds");
            list.SetAssRowFilter(p=>false);Check(list.AssVisibleRowCount==0 && list.Items.Count==4,"No matches keeps the document intact");
            var saved=new AdvancedSubStationAlpha().ToText(sub,"test");Check(saved.Contains("Cue 0") && saved.Contains("Edited Bob"),"Saving keeps filtered-out cues");
            list.SetAssRowFilter(null);await Task.Delay(40);Check(list.GetItemRect(0).Height>0 && !list.HasAssRowFilter,"Clear filter restores original rows");
            Call(main,"ResetSubtitle");main.Close();
        }
    }
    [STAThread]static int Main(string[] args)
    {
        var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory").SetValue(null,root+Path.DirectorySeparatorChar);typeof(Configuration).GetField("SettingsFileName").SetValue(null,Path.Combine(root,"Settings.xml"));Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);int exit=0;
        using(var anchor=new Form{ShowInTaskbar=false,Location=new Point(-30000,-30000),StartPosition=FormStartPosition.Manual}){anchor.Shown+=async(s,e)=>{try{await Run(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}Console.WriteLine("BROADCAST/FILTER CHECKS: "+checks);return exit;
    }
}
