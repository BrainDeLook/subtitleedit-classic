using System;
using System.IO;
using System.Linq;
using System.Threading;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.ContainerFormats.MaterialExchangeFormat;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
namespace Classic54Tests
{
 public class CinemaMxfExportTests
 {
  [Fact] public void TextMxfRoundTripKeepsCyrillicAndTiming()
  {
   var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Привет, мир!",1000,2500));sub.Paragraphs.Add(new Paragraph("Second line",3000,4500));
   var xml=ClassicCinemaMxf.CreateXml(sub,"Test reel",24);var font=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"text-"+Guid.NewGuid().ToString("N")+".mxf");
   try
   {
    ClassicCinemaMxf.Export(xml,font,path,240,CancellationToken.None).GetAwaiter().GetResult();
    var parser=new MxfParser(path);Assert.True(parser.IsValid);var actualXml=Assert.Single(parser.GetSubtitles());Assert.Equal(xml,actualXml);
    var restored=new Subtitle();new DCinemaSmpte2014().LoadSubtitle(restored,actualXml.SplitToLines(),"test.xml");Assert.Equal(2,restored.Paragraphs.Count);Assert.Contains("Привет, мир!",restored.Paragraphs[0].Text);Assert.Equal(1000,restored.Paragraphs[0].StartTime.TotalMilliseconds,1);
    Assert.Equal("Привет, мир!",sub.Paragraphs[0].Text);Assert.Equal(string.Empty,sub.Header);
   }
   finally{if(File.Exists(path))File.Delete(path);}
  }
  [Fact] public void CancelledExportKeepsExistingDestination()
  {
   var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Hello",1000,2000));var xml=ClassicCinemaMxf.CreateXml(sub,"Test",24);var font=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cancel-"+Guid.NewGuid().ToString("N")+".mxf");File.WriteAllText(path,"existing");
   try{using(var cancel=new CancellationTokenSource()){cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>ClassicCinemaMxf.Export(xml,font,path,0,cancel.Token).GetAwaiter().GetResult());}Assert.Equal("existing",File.ReadAllText(path));}
   finally{File.Delete(path);}
  }
  [Fact] public void ItunesDefaultsSurviveSettingsRoundTrip()
  {
   var settings=Nikse.SubtitleEdit.Core.Settings.Settings.GetSettings();settings.SubtitleSettings.TimedTextItunesDefaultColor="#EBEBEB";settings.SubtitleSettings.TimedTextItunesDefaultStyle="italic";settings.SubtitleSettings.TimedTextItunesDefaultRegion="top";settings.SubtitleSettings.TimedTextItunesHeaderTemplate="<tt xmlns=\"http://www.w3.org/ns/ttml\"><head/></tt>";
   var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"itt-settings-"+Guid.NewGuid().ToString("N")+".xml");
   try{Nikse.SubtitleEdit.Core.Settings.Settings.CustomSerialize(path,settings);var read=Nikse.SubtitleEdit.Core.Settings.Settings.CustomDeserialize(path);Assert.Equal("#EBEBEB",read.SubtitleSettings.TimedTextItunesDefaultColor);Assert.Equal("italic",read.SubtitleSettings.TimedTextItunesDefaultStyle);Assert.Equal("top",read.SubtitleSettings.TimedTextItunesDefaultRegion);Assert.Equal(settings.SubtitleSettings.TimedTextItunesHeaderTemplate,read.SubtitleSettings.TimedTextItunesHeaderTemplate);}
   finally{File.Delete(path);}
  }
 }
}
