using System;
using System.IO;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicEffects;
using Nikse.SubtitleEdit.Logic.ClassicDraw;
using Nikse.SubtitleEdit.Logic.ClassicFormats;
class TestClassicFiveBlocks
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    [STAThread] static int Main(string[] args)
    {
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Error));
        try
        {
            var subtitle=new Subtitle {Header=AdvancedSubStationAlpha.DefaultHeader};subtitle.Paragraphs.Add(new Paragraph(@"{\fnArial}Hello Привет 日本語 العربية",1000,4000));
            Check(AssaFontEmbedder.GetUsedFontNames(subtitle).Contains("Arial"),"Inline font names are collected");
            var bytes=File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"));
            var trimmed=FontTrimmer.Trim(bytes,AssaFontEmbedder.GetUsedTextLines(subtitle));
            Console.WriteLine("Trim result: "+trimmed.SkipReason+" "+trimmed.KeptGlyphs);
            Check(trimmed.Trimmed && trimmed.Bytes.Length<bytes.Length && trimmed.KeptGlyphs>1,"Real HarfBuzz-based font trimming retains mixed-script glyphs");
            var again=FontTrimmer.Trim(trimmed.Bytes,AssaFontEmbedder.GetUsedTextLines(subtitle));Check(again.Bytes.Length<=trimmed.Bytes.Length,"Trimmed font remains parseable");
            var footer=AssaFontEmbedder.AddFontToFooter("[Graphics]\nfilename: logo.png\n!\n","Arial.ttf",trimmed.Bytes);
            Check(AssaFontEmbedder.GetEmbeddedFonts(footer).Single().Bytes.SequenceEqual(trimmed.Bytes),"Embedded font bytes round-trip through ASS encoding");
            Check(footer.Contains("[Graphics]") && AssaFontEmbedder.AddFontToFooter(footer,"Arial.ttf",trimmed.Bytes)==footer,"Embedding preserves graphics and avoids duplicate files");
            Check(!FontTrimmer.Trim(new byte[]{1,2,3},new[]{"Test"}).Trimmed,"Malformed fonts retain their original data");
            for(int size=1;size<100;size++){var input=Enumerable.Range(0,size).Select(i=>(byte)(i*11)).ToArray();Check(ClassicUUEncoding.UUDecode(ClassicUUEncoding.UUEncode(input)).SequenceEqual(input),"ASS attachment length remainder "+size);}
            foreach(var effect in AdvancedEffectDisplayFactory.List())
            {
                if(effect.UsesAudio)continue;
                var generated=effect.ApplyEffect(subtitle.Header,subtitle.Paragraphs.Select(p=>new SubtitleLineViewModel(p)).ToList(),1280,720,null);
                Check(generated.Count>0 && generated.All(p=>p.EndTime>=p.StartTime && p.Text!=null),"Stable effect generates valid events: "+effect.Name);
            }
            var svg=SvgImporter.Import("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><path fill='#ff0000' d='M0 0 L100 0 L100 100 Z'/></svg>",1280,720,2);
            Check(svg.Shapes.Count>0 && svg.Shapes.All(s=>s.Layer>=2 && s.Points.Count>=3),"Real SVG becomes layered ASS drawing geometry");
            var shape=svg.Shapes[0];var drawing=shape.ToAssa();Check(DrawShape.ParseAssa(drawing,2,DrawColors.White,false).Count>0,"Drawing geometry round-trips through ASS commands");
            var before=shape.GetBounds();shape.Offset(20,10);shape.Rotate(15);Check(shape.GetBounds()!=before,"Drawing translation and rotation change geometry");
            foreach(var item in ShapeLibrary.BuiltIn){Check(item.CreateShapes(10,10,200,100,DrawColors.White,0).Count>0,"Stable shape library item: "+item.Name);}
            var originalFirstFormat=SubtitleFormat.AllSubtitleFormats.First().Name;ClassicFormatRegistry.Register();Check(ClassicFormatRegistry.Added.Count>=25,"New stable format readers/writers are registered");
            Check(SubtitleFormat.AllSubtitleFormats.First().Name==originalFirstFormat,"Format registration preserves the classic default output format");
            var count=SubtitleFormat.AllSubtitleFormats.Count();ClassicFormatRegistry.Register();Check(SubtitleFormat.AllSubtitleFormats.Count()==count,"Format registration is idempotent");
            foreach(var format in ClassicFormatRegistry.Added.Where(f=>new[]{"Csv Excel","EBU-TT","EBU-TT-D","Timed Text IMSC Rosetta","Wistia Json","Final Cut Pro Xml Captions"}.Contains(f.Name)))
            {
                var encoded=format.ToText(subtitle,"Test");var decoded=new Subtitle();format.LoadSubtitle(decoded,encoded.SplitToLines(),"test"+format.Extension);Check(decoded.Paragraphs.Count>0,"Stable format round trip: "+format.Name);
            }
            foreach(var name in new[]{"CsvExcel","EbuTt","EbuTtD","TimedTextImscRosetta","WistiaJson","JsonType24","YouTubeChapters","MatroskaChaptersXml","FfmpegMetadataChapters","Spc","SonicDvdProducer","CheetahCaptionAsc","Grid608","AdobePremiereMarkersCsv","DaVinciResolveMarkerEdl","FinalCutProXmlCaptions","FinalCutProXml112","FinalCutProXml113","FinalCutProXml114"})
            {
                var format=ClassicFormatRegistry.Added.Single(f=>f.GetType().Name==name);var text=format.ToText(subtitle,"Test");var parsed=new Subtitle();format.LoadSubtitle(parsed,text.SplitToLines(),"test"+format.Extension);Check(parsed.Paragraphs.Count>0,"Explicit stable format round trip: "+name);
            }
            Console.WriteLine("FEATURE CHECKS: "+checks);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}


