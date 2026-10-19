using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Xml;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicImageExport;
using Nikse.SubtitleEdit.Logic.ClassicImport;
using SkiaSharp;
class TestClassicFiveBlocksNative
{
    static int checks;
    static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS: "+text);checks++;}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            Configuration.Settings.General.FFmpegLocation=args[0];var token=CancellationToken.None;
            var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("<i>Привет world</i>",1000,2500));sub.Paragraphs.Add(new Paragraph("日本語 العربية",3000,4500));
            var options=new ClassicImageOptions{Width=640,Height=360,FontSize=32,UseAssStyles=false};
            foreach(TextEffectPreset effect in Enum.GetValues(typeof(TextEffectPreset))){options.Effect=effect;var ip=ClassicImageJob.Render(sub,sub.Paragraphs[0],options);using(ip.Bitmap){Check(ip.Bitmap.Width>10 && ip.Bitmap.Height>10,"Native renderer: "+effect);}}
            options.Effect=TextEffectPreset.None;
            for(int format=0;format<4;format++)
            {
                if(format==3){options.Width=720;options.Height=576;options.FrameRate=25;}
                var folder=Path.Combine(root,"images"+format);ClassicImageJob.Export(sub,options,format,folder,token,null).GetAwaiter().GetResult();
                if(format==3){var bytes=File.ReadAllBytes(Path.Combine(folder,"subtitles.sup"));Check(bytes.Length>20 && bytes[0]=='S' && bytes[1]=='P',"DVD SUP stream header");}
                else
                {
                    var png=Directory.GetFiles(folder,"*.png");Check(png.Length==2,"Complete image set: "+format);foreach(var file in png)using(var bitmap=SKBitmap.Decode(file))Check(bitmap!=null && bitmap.Width>10,"Exported PNG decodes: "+format);
                    var doc=new XmlDocument{XmlResolver=null};doc.Load(Path.Combine(folder,format==0?"subtitles.ttml":"index.xml"));Check(doc.DocumentElement!=null,"Exported metadata XML: "+format);
                    if(format==0){Nikse.SubtitleEdit.Logic.ClassicFormats.ClassicFormatRegistry.Register();var reader=Nikse.SubtitleEdit.Logic.ClassicFormats.ClassicFormatRegistry.Other().Single(f=>f.Name=="Timed Text Image");var restored=new Subtitle();var path=Path.Combine(folder,"subtitles.ttml");var lines=File.ReadAllLines(path).ToList();Check(reader.IsMine(lines,path),"IMSC image is detected by the OCR import reader");reader.LoadSubtitle(restored,lines,path);Check(restored.Paragraphs.Count==2 && restored.Paragraphs.All(p=>p.Text.EndsWith(".png")),"IMSC image reimports PNG names and cue timing");}
                    if(format==2){var bytes=File.ReadAllBytes(png[0]);Check(bytes[24]==8 && bytes[25]==3,"BDN uses indexed 8-bit PNG");}
                }
            }
            var csv=ClassicSpreadsheet.Convert(new List<List<string>>{new List<string>{"2500","Hello","4000","Alice"}},new[]{"start","text","end","actor"},false,2);
            Check(csv.Paragraphs.Count==1 && csv.Paragraphs[0].StartTime.TotalMilliseconds==2500 && csv.Paragraphs[0].Actor=="Alice","Custom column mapping and milliseconds");
            var xml=new UnknownFormatImporterXml().AutoGuessImport(new[]{"<root><cue begin='1s' end='2s'>One</cue><cue begin='3s' end='4s'>Two</cue></root>"}.ToList());Check(xml.Paragraphs.Count==2,"Generic XML import");
            foreach(var xlsx in new[]{false,true})
            {
                var path=Path.Combine(root,xlsx?"columns.xlsx":"columns.csv");
                sub.Paragraphs[0].Actor="Alice";sub.Paragraphs[0].Text="First, \"quoted\"\nsecond line";
                ClassicTableExport.Write(sub,new[]{new ClassicTableColumn{Field="text",Header="text"},new ClassicTableColumn{Field="start",Header="startms"},new ClassicTableColumn{Field="end",Header="endms"},new ClassicTableColumn{Field="actor",Header="actor"}},true,2,path,xlsx);
                var rows=ClassicSpreadsheet.Read(path);var restored=ClassicSpreadsheet.Convert(rows,new[]{"text","start","end","actor"},true,2);
                Check(restored.Paragraphs.Count==2 && restored.Paragraphs[0].Text.Replace("\r","")==sub.Paragraphs[0].Text && restored.Paragraphs[0].Actor=="Alice","Custom CSV/XLSX round trip: "+xlsx);
            }
            var decoder=new Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea608.ClosedCaptionDecoder();
            for(int type=0;type<2;type++)
            {
                decoder.AddFrame(0,new[]{new Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea608.CcData(type,0x14,0x20)});
                decoder.AddFrame(100,new[]{new Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea608.CcData(type,0x48,0x69)});
                decoder.AddFrame(1000,new[]{new Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea608.CcData(type,0x14,0x2f)});
                decoder.AddFrame(3000,new[]{new Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea608.CcData(type,0x14,0x2c)});
            }
            var tracks=decoder.Finish(0);Check(tracks.ContainsKey(1) && tracks.ContainsKey(3) && tracks[1][0].Text.Contains("Hi"),"CEA-608 fields remain separate caption tracks");
            var dtv=new Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea708.DtvccServiceDecoder();
            dtv.Add(3,2,0x22,1000);dtv.Add(2,'H','i',1000);
            var services=dtv.Finish(3000);Check(services.ContainsKey(1) && services[1][0].Text.Contains("Hi"),"CEA-708 service decoding");
            var dvdPath=Path.Combine(root,"dvd.sub");
            using(var writer=new Nikse.SubtitleEdit.Logic.ClassicExport.VobSubWriter(dvdPath,720,576,30,30,32,System.Drawing.Color.White,System.Drawing.Color.Black,true,Nikse.SubtitleEdit.Core.Common.DvdSubtitleLanguage.CompliantLanguages.First()))
            using(var bitmap=new System.Drawing.Bitmap(100,40))
            {using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.Clear(System.Drawing.Color.Transparent);g.FillRectangle(System.Drawing.Brushes.White,5,5,90,30);}writer.WriteParagraph(new Paragraph("",1000,3000),bitmap,System.Drawing.ContentAlignment.BottomCenter);writer.WriteIdxFile();}
            var packs=Nikse.SubtitleEdit.Logic.ClassicImport.Dvd.DvdSubtitleRipper.Rip(new[]{dvdPath});Check(packs.Count>0 && Nikse.SubtitleEdit.Logic.ClassicImport.Dvd.DvdSubtitleRipper.CountEncrypted(packs)==0,"DVD subpicture writer and buffered VOB reader round trip");
            var originalData=Configuration.DataDirectory;var fontData=Path.Combine(root,"User's data");Directory.CreateDirectory(Path.Combine(fontData,"Fonts"));
            File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),Path.Combine(fontData,"Fonts","arial.ttf"));
            typeof(Configuration).GetField("DataDirectory").SetValue(null,fontData+Path.DirectorySeparatorChar);
            Check(ClassicFontCollection.CollectedFiles().Any(f=>f.Family=="Arial"),"Collected fonts are available without system installation");
            File.WriteAllText(Path.Combine(root,"test.ass"),Nikse.SubtitleEdit.Core.SubtitleFormats.AdvancedSubStationAlpha.DefaultHeader+"\nDialogue: 0,0:00:00.00,0:00:01.00,Default,,0,0,0,,Test",new System.Text.UTF8Encoding(false));
            var filter=ClassicBurnIn.BuildFilter("test.ass",320,180,null,null,out var extra,out var mapping,out var endArgs);
            var start=new System.Diagnostics.ProcessStartInfo(args[0],"-hide_banner -loglevel debug -f lavfi -i color=s=320x180:d=1 "+filter+" -frames:v 1 -f null -"){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var process=System.Diagnostics.Process.Start(start)){var log=process.StandardError.ReadToEnd();process.StandardOutput.ReadToEnd();process.WaitForExit();File.WriteAllText(Path.Combine(root,"font-filter.log"),log);Check(process.ExitCode==0 && log.Contains("Loading font file") && log.Contains("arial.ttf"),"Hard-subtitle font folder works with spaces and apostrophes");}
            typeof(Configuration).GetField("DataDirectory").SetValue(null,originalData);
            var source=Path.Combine(root,"source.mkv");
            ClassicMediaJobs.Render("-f lavfi -i testsrc2=size=320x180:rate=30:duration=6 -f lavfi -i sine=frequency=440:duration=6 -f lavfi -i sine=frequency=880:duration=6 -map 0:v -map 1:a -map 2:a -c:v libx264 -preset ultrafast -c:a aac -metadata:s:a:0 language=tur -metadata:s:a:1 language=eng -metadata:s:a:1 title=English",source,token).GetAwaiter().GetResult();
            var info=ClassicMediaJobs.Probe(source,token).GetAwaiter().GetResult();Check(info.Streams.Count(s=>s.Type=="audio")==2,"Fixture contains named alternate audio tracks");
            var streams=info.Streams.Select(s=>new ClassicMappedStream{File=source,Stream=s}).ToList();
            var remux=Path.Combine(root,"remux.mkv");ClassicVideoTools.Remux(source,remux,streams,false,token).GetAwaiter().GetResult();var probe=ClassicMediaJobs.Probe(remux,token).GetAwaiter().GetResult();Check(probe.Streams.Count(s=>s.Type=="audio")==2 && probe.Streams.Any(s=>s.Language=="eng"),"Remux keeps separate tracks and language metadata");
            var mix=Path.Combine(root,"mix.mkv");ClassicVideoTools.Remux(source,mix,streams,true,token).GetAwaiter().GetResult();probe=ClassicMediaJobs.Probe(mix,token).GetAwaiter().GetResult();Check(probe.Streams.Count(s=>s.Type=="audio")==1 && probe.Streams.Single(s=>s.Type=="audio").Channels==2,"Explicit audio mix creates exactly one stereo stream");
            var clips=new[]{new ClassicCutSegment{Start=0,End=2},new ClassicCutSegment{Start=3,End=5}};
            foreach(var transition in new[]{"none","fade","fadeblack","wipeleft","slideright"}){var file=Path.Combine(root,"cut-"+transition+".mp4");ClassicVideoTools.Cut(source,file,clips,2,.5,transition,info.Seconds,token).GetAwaiter().GetResult();probe=ClassicMediaJobs.Probe(file,token).GetAwaiter().GetResult();Check(Math.Abs(probe.Seconds-(transition=="none"?4:3.5))<.2 && probe.Streams.Single(s=>s.Type=="audio").Channels==2,"Cut/join duration and audio: "+transition);}
            for(int profile=0;profile<3;profile++){var file=Path.Combine(root,"encode"+profile+".mkv");ClassicVideoTools.Reencode(source,file,new[]{2},profile,token).GetAwaiter().GetResult();probe=ClassicMediaJobs.Probe(file,token).GetAwaiter().GetResult();Check(probe.Streams.Count(s=>s.Type=="audio")==1 && probe.Streams.First(s=>s.Type=="video").Codec==(profile==2?"prores":"h264"),"Editing profile and chosen audio: "+profile);}
            var chapterFile=Path.Combine(root,"chapters.mkv");ClassicVideoTools.Chapters(source,chapterFile,new[]{new ClassicVideoChapter{Start=0,Title="Начало #1"},new ClassicVideoChapter{Start=3,Title="Two = 2"}},info.Seconds,token).GetAwaiter().GetResult();
            var json=ClassicMediaJobs.Capture(Path.Combine(Path.GetDirectoryName(args[0]),"ffprobe.exe"),"-v error -show_chapters -of json "+ClassicMediaJobs.Q(chapterFile),token).GetAwaiter().GetResult();Check(json.Contains("Начало #1") && json.Contains("Two = 2"),"Muxed chapters preserve Unicode and escaped titles");
            var cancelled=Path.Combine(root,"cancelled.mp4");var cts=new CancellationTokenSource();cts.Cancel();try{ClassicVideoTools.Reencode(source,cancelled,new[]{2},0,cts.Token).GetAwaiter().GetResult();throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}Check(!File.Exists(cancelled) && !Directory.GetFiles(root,".se-*").Any(),"Cancellation leaves no output or staging files");
            Console.WriteLine("NATIVE CHECKS: "+checks+"; files: "+root);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
