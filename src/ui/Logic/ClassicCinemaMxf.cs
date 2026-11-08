using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.ContainerFormats.MaterialExchangeFormat;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
namespace Nikse.SubtitleEdit.Logic
{
 public static class ClassicCinemaMxf
 {
  // The SMPTE formatter uses the application's edit-rate setting while generating frame labels.
  // Generate the XML on the UI thread, then write the immutable snapshot in the background.
  public static string CreateXml(Subtitle subtitle,string title,int fps)
  {
   if(!new[]{24,25,30,48,50,60}.Contains(fps))throw new ArgumentOutOfRangeException(nameof(fps));
   var settings=Configuration.Settings.SubtitleSettings;var originalRate=settings.CurrentDCinemaEditRate;string text;
   try {settings.CurrentDCinemaEditRate=fps+" 1";text=new DCinemaSmpte2014().ToText(new Subtitle(subtitle),title);}
   finally {settings.CurrentDCinemaEditRate=originalRate;}
   var xml=new XmlDocument{XmlResolver=null};xml.LoadXml(text);
   var font="urn:uuid:"+Guid.NewGuid();
   foreach(XmlNode node in xml.DocumentElement.SelectNodes("*[local-name()='LoadFont']"))node.InnerText=font;
   var name=xml.DocumentElement.SelectSingleNode("*[local-name()='ContentTitleText']");name.InnerText=title;
   return xml.OuterXml;
  }
  public static async Task Export(string xml,string fontFile,string output,long reelFrames,CancellationToken token)
  {
   if(reelFrames<0)throw new ArgumentOutOfRangeException(nameof(reelFrames));
   var extension=Path.GetExtension(fontFile);
   if(!extension.Equals(".ttf",StringComparison.OrdinalIgnoreCase) && !extension.Equals(".otf",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Select a TTF or OTF font to embed.");
   var info=new FileInfo(fontFile);if(info.Length>32*1024*1024)throw new InvalidDataException("The selected font exceeds 32 MiB.");
   ClassicMediaJobs.CheckOutput(fontFile,output);
   var font=File.ReadAllBytes(fontFile);var ids=TimedTextMxfWriter.GetLoadFontIds(xml);
   var stage=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)),".se-text-mxf-"+Guid.NewGuid().ToString("N")+".mxf");
   try
   {
    await Task.Run(()=>
    {
     token.ThrowIfCancellationRequested();var writer=new TimedTextMxfWriter{ContainerDuration=reelFrames};
     foreach(var id in ids)writer.Resources.Add(new TimedTextMxfResource(id,TimedTextMxfResource.MimeTypeFont,font));
     writer.Write(stage,xml);token.ThrowIfCancellationRequested();
    },token).ConfigureAwait(false);
    ClassicMediaJobs.Commit(stage,output);
   }
   finally {if(File.Exists(stage))File.Delete(stage);}
  }
 }
}
