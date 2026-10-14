using System;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
class TestClassicBurnInCanvas {
 static void Main() {
  var options=new ClassicBurnInOptions{Effects="ScrollUp"};
  var ass=new Subtitle{Header="[Script Info]\nPlayResX: 384\nPlayResY: 288\n[V4+ Styles]\n"};
  ass.Paragraphs.Add(new Paragraph("Test",0,2000));
  ClassicBurnIn.ApplyEffects(ass,options,1920,1080,43);
  if(!ass.Paragraphs[0].Text.StartsWith(@"{\move(192,228,192,208)}")) throw new Exception("Motion ignored ASS script canvas");
  var plain=new Subtitle(); plain.Paragraphs.Add(new Paragraph("Test",0,2000));
  ClassicBurnIn.ApplyEffects(plain,options,1920,1080,43);
  if(!plain.Paragraphs[0].Text.StartsWith(@"{\move(960,1020,960,1000)}")) throw new Exception("Output canvas fallback changed");
  Console.WriteLine("PASS: ASS script canvas and missing-canvas fallback");
 }
}
