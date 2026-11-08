using System;
using System.IO;
using System.Threading;
using System.Xml;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.ContainerFormats.MaterialExchangeFormat;
using Nikse.SubtitleEdit.Logic.ClassicImageExport;
namespace Classic54Tests
{
 public class ImageCinemaExportTests
 {
  [Theory][InlineData(4)][InlineData(5)] public void ImageCinemaExportHasCompleteResources(int format)
  {
   var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Привет world",1000,2000));sub.Paragraphs.Add(new Paragraph("Second",3000,4000));var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cinema-images-"+Guid.NewGuid().ToString("N"));
   try
   {
    ClassicImageJob.Export(sub,new ClassicImageOptions{Width=640,Height=360,FontSize=32,UseAssStyles=false,FrameRate=24},format,folder,CancellationToken.None,null).GetAwaiter().GetResult();
    if(format==4){var parser=new MxfParser(Path.Combine(folder,"subtitles.mxf"));Assert.True(parser.IsValid);Assert.Equal(2,parser.GetImages().Count);Assert.Single(parser.GetSubtitles());}
    else{Assert.Equal(2,Directory.GetFiles(folder,"*.png").Length);var xml=new XmlDocument();xml.Load(Path.Combine(folder,"index.xml"));Assert.Equal("24 1",xml.DocumentElement.SelectSingleNode("*[local-name()='EditRate']").InnerText);}
   }
   finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
  }
  [Fact] public void ImageRendererUsesCueStyleAndOverrides()
  {
   var sub=new Subtitle{Header="[Script Info]\nScriptType: v4.00+\nPlayResX: 640\nPlayResY: 360\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Sign,Arial,36,&H0000FFFF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,3,1,8,20,20,25,1\n"};
   var cue=new Paragraph("{\\i1}Yellow sign",1000,2000){Extra="Sign"};var options=new ClassicImageOptions{Width=640,Height=360,UseAssStyles=true};var image=ClassicImageJob.Render(sub,cue,options);
   using(image.Bitmap){Assert.Equal(36f,image.FontSize);Assert.True(image.IsBold);Assert.Equal((byte)255,image.FontColor.Red);Assert.Equal((byte)255,image.FontColor.Green);Assert.Equal((byte)0,image.FontColor.Blue);Assert.Equal(ExportAlignment.TopCenter,image.Alignment);Assert.True(image.Bitmap.Width>20);Assert.Contains("<i>",image.Text);}
  }
 }
}
