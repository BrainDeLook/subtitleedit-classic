using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using SkiaSharp;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicDraw;
using Nikse.SubtitleEdit.Logic.ClassicTiming;
using Nikse.SubtitleEdit.Logic.Plugins;
using Nikse.SubtitleEdit.Logic.SpellCheck;
namespace Classic54Tests
{
 public class Complete54IntegrationTests
 {
    [Theory][InlineData(true)][InlineData(false)]
    public void EmbeddedOcrBaseRulesPreserveUserOverridesAndExplicitCustomFiles(bool standardFolder)
    {
        var saved=Configuration.DictionariesDirectory;
        var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ocr-base-"+Guid.NewGuid().ToString("N"));
        var custom=Path.Combine(root,"custom");Directory.CreateDirectory(custom);
        var dictionaryField=typeof(Configuration).GetField("DictionariesDirectory");dictionaryField.SetValue(null,root+Path.DirectorySeparatorChar);
        var file=Path.Combine(standardFolder?root:custom,"eng_OCRFixReplaceList.xml");
        const string oldBase="<ReplaceList><WholeWords><Word from=\"Teh\" to=\"OLD\" /></WholeWords></ReplaceList>";
        try
        {
            File.WriteAllText(file,oldBase);
            File.WriteAllText(Nikse.SubtitleEdit.Core.Dictionaries.OcrFixReplaceList.GetUserFileName(file),"<ReplaceList><WholeWords><Word from=\"Mispelt\" to=\"User override\" /></WholeWords></ReplaceList>");
            var list=new Nikse.SubtitleEdit.Core.Dictionaries.OcrFixReplaceList(file);
            Assert.Equal("User override",list.FixCommonWordErrors("Mispelt"));
            Assert.Equal(standardFolder?"gasps":"gasPs",list.FixCommonWordErrors("gasPs"));
            Assert.Equal(oldBase,File.ReadAllText(file));
            if(!standardFolder)Assert.Equal("OLD",list.FixCommonWordErrors("Teh"));
        }
        finally{dictionaryField.SetValue(null,saved);Directory.Delete(root,true);}
    }
    [Fact] public void QwenOldEngineMarkerIsRemovedBeforePhraseAssembly()
    {
        var result=Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.ParseWordJson("{\"words\":[{\"word\":\"<asr_text>hello\",\"start\":0,\"end\":1},{\"word\":\"world.\",\"start\":1,\"end\":2}]}");
        Assert.Equal("hello world.",result.Paragraphs[0].Text);
    }
    [Fact] public void TypingWrapSkipsPastesAndPreExistingLongLines()
    {
        var old=Configuration.Settings.General.AutoWrapLineWhileTyping;var max=Configuration.Settings.General.SubtitleLineMaximumLength;
        try
        {
            Configuration.Settings.General.AutoWrapLineWhileTyping=true;Configuration.Settings.General.SubtitleLineMaximumLength=30;
            using(var box=new System.Windows.Forms.TextBox{Multiline=true})
            {
                ClassicTextAutoWrap.Attach(box);box.Text=new string('a',14)+" "+new string('b',15);box.SelectionStart=box.Text.Length;
                var key=typeof(System.Windows.Forms.Control).GetMethod("OnKeyPress",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                key.Invoke(box,new object[]{new System.Windows.Forms.KeyPressEventArgs('c')});box.SelectedText="c";
                Assert.Equal(new string('a',14)+Environment.NewLine+new string('b',15)+"c",box.Text);
                box.Text="An existing line longer than thirty characters";box.SelectionStart=3;key.Invoke(box,new object[]{new System.Windows.Forms.KeyPressEventArgs('x')});box.SelectedText="x";Assert.DoesNotContain(Environment.NewLine,box.Text);
                box.Text="A newly pasted line longer than thirty characters";Assert.DoesNotContain(Environment.NewLine,box.Text);
            }
        }
        finally{Configuration.Settings.General.AutoWrapLineWhileTyping=old;Configuration.Settings.General.SubtitleLineMaximumLength=max;}
    }
    [Theory][InlineData("iFuera!")][InlineData("eBay is ready.")][InlineData("iPhone works.")]
    public void SentenceCasingPreservesLowercasePrefixBeforeCapital(string text)
    {
        var method=typeof(Nikse.SubtitleEdit.Core.Forms.FixCommonErrors.FixStartWithUppercaseLetterAfterParagraph).GetMethod("DoFix",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var result=(string)method.Invoke(null,new object[]{new Paragraph(text,1000,2000),new Paragraph("Previous.",0,900),Encoding.UTF8,"es"});Assert.Equal(text,result);
    }
    [Fact] public void GapRangeUsesFramesAndMilliseconds()
    {
        var fps=Configuration.Settings.General.CurrentFrameRate;Configuration.Settings.General.CurrentFrameRate=25;
        try{ClassicSyntaxGap.Configure(new ClassicEditorPreferences{ColorGapRange=true,GapRangeFrames=true,GapRangeFrom=2,GapRangeTo=3});Assert.True(ClassicSyntaxGap.Contains(80));Assert.True(ClassicSyntaxGap.Contains(120));Assert.False(ClassicSyntaxGap.Contains(160));}
        finally{ClassicSyntaxGap.Configure(new ClassicEditorPreferences());Configuration.Settings.General.CurrentFrameRate=fps;}
    }
    [Fact] public void DrawingGroupsAnimationAlphaAndHiddenRoundTrip()
    {
        var shape=DrawShape.ParseAssa("m 20 30 l 120 30 120 90 20 90",2,DrawColor.FromArgb(128,20,40,60),false).Single();
        shape.GroupId=7;shape.Animation=new DrawAnimation{In=DrawAnimationEffect.Pop,Out=DrawAnimationEffect.Fade,Loop=DrawLoopEffect.Pulse};
        shape.OutlineWidth=3;var hidden=shape.Clone();hidden.Hidden=true;hidden.Layer=3;hidden.Offset(150,0);
        var cue=new Paragraph("",1000,3000);
        var lines=ClassicDrawDocument.Write(new[]{shape,hidden},cue,640,360,true);
        Assert.Equal(2,lines.Count);Assert.Contains("\\t(",lines[0].Text);Assert.Contains("SeGroups:7",lines[0].Effect);
        var back=ClassicDrawDocument.Read(lines);Assert.Equal(2,back.Count);Assert.Equal(7,back[0].GroupId);Assert.Equal(shape.ForeColor,back[0].ForeColor);
        Assert.Equal(DrawAnimationEffect.Pop,back[0].Animation.In);Assert.True(back[1].Hidden);
        Assert.Equal(shape.GetBounds(),back[0].GetBounds());Assert.Equal(1,ClassicDrawDocument.Write(back,cue,640,360).Count);
    }
    [Fact] public void ClipArtCatalogProducesRoundTripShapes()
    {
        Assert.True(ShapeLibrary.BuiltIn.Count()>40);
        foreach(var item in ShapeLibrary.BuiltIn)
        {
            var shapes=item.CreateShapes(30,30,200,150,Color.White,0);Assert.True(shapes.Count>0,"shape template: "+item.Name);
            var lines=ClassicDrawDocument.Write(shapes,new Paragraph("",0,2000),640,360,true);Assert.True(lines.Count>0,"written: "+item.Name);
            Assert.True(ClassicDrawDocument.Read(lines).Count>0,"read back: "+item.Name+" "+lines[0].Text);
        }
    }
    [Fact] public void DrawingTextAndMasksRoundTrip()
    {
        var shape=DrawShape.ParseAssa("m 20 30 l 220 30 220 120 20 120",0,DrawColors.White,false).Single();shape.Text="Two\nlines";
        var mask=DrawShape.ParseAssa("m 30 40 l 40 40 40 60 30 60",0,DrawColors.White,true).Single();
        var lines=ClassicDrawDocument.Write(new[]{shape,mask},new Paragraph("",0,2000),640,360,true);
        var back=ClassicDrawDocument.Read(lines);Assert.Single(back.Where(s=>s.IsEraser));Assert.Single(back.Where(s=>s.HasText));Assert.Equal("Two\nlines",back.Single(s=>s.HasText).Text);
    }
    [Fact] public void SameTimeStackMovesOnlyMatchingCues()
    {
        var s=new Subtitle();s.Paragraphs.AddRange(new[]{new Paragraph("a",100,1000),new Paragraph("b",100,1000),new Paragraph("c",100,1001)});
        var before=new Paragraph(s.Paragraphs[0]);s.Paragraphs[0].StartTime.TotalMilliseconds=150;s.Paragraphs[0].EndTime.TotalMilliseconds=1100;
        Assert.Equal(new[]{1},EditorTiming54.MoveSameTimeStack(s,before,s.Paragraphs[0]));Assert.Equal(150d,s.Paragraphs[1].StartTime.TotalMilliseconds);Assert.Equal(1001d,s.Paragraphs[2].EndTime.TotalMilliseconds);
    }
    [Fact] public void MoveLinesClampsWholeSelectionAndPreservesDurations()
    {
        var s=new Subtitle();s.Paragraphs.AddRange(new[]{new Paragraph("a",20,100),new Paragraph("b",200,400)});
        Assert.True(EditorTiming54.MoveLines(s,new[]{0,1},-40));Assert.Equal(0d,s.Paragraphs[0].StartTime.TotalMilliseconds);Assert.Equal(180d,s.Paragraphs[1].StartTime.TotalMilliseconds);Assert.Equal(200d,s.Paragraphs[1].DurationTotalMilliseconds);
    }
    [Fact] public void PushNeighborPreservesMinimumAndLeavesBlockedCueUntouched()
    {
        var s=new Subtitle();s.Paragraphs.AddRange(new[]{new Paragraph("a",0,1000),new Paragraph("b",1020,2000)});
        Assert.True(EditorTiming54.PushBoundary(s,1,true,100,20,500,10000));Assert.Equal(920d,s.Paragraphs[1].StartTime.TotalMilliseconds);Assert.Equal(900d,s.Paragraphs[0].EndTime.TotalMilliseconds);
        Assert.False(EditorTiming54.PushBoundary(s,1,true,1000,20,1000,10000));Assert.Equal(900d,s.Paragraphs[0].EndTime.TotalMilliseconds);
    }
    [Fact] public void SupWriterKeepsTwoObjectsDuringOverlapAndClearsAtEnd()
    {
        using(var stream=new MemoryStream())using(var bitmap=new SKBitmap(50,20))
        {
            bitmap.Erase(SKColors.White);using(var writer=new ClassicSupCompositionWriter(stream,640,360,25,0)){writer.Add(1000,3000,bitmap,20,20,SKColors.White,false);writer.Add(2000,4000,bitmap,20,200,SKColors.White,true);writer.Flush();}
            stream.Position=0;var frames=BluRaySupParser.ParseBluRaySup(stream,new StringBuilder(),false,new Dictionary<int,List<PaletteInfo>>(),new Dictionary<int,List<BluRaySupParser.OdsData>>());
            Assert.Contains(frames,f=>f.PcsObjects.Count==2);Assert.Contains(frames,f=>f.IsForced);Assert.Equal(4000d,frames.Last().EndTime/90d);
        }
    }
    [Fact] public void SupWriterFlattensIntersectingWindows()
    {
        using(var stream=new MemoryStream())using(var bitmap=new SKBitmap(50,20))
        {
            bitmap.Erase(SKColors.Red);using(var writer=new ClassicSupCompositionWriter(stream,640,360,25,203)){writer.Add(1000,2000,bitmap,20,20,SKColors.Red,false);writer.Add(1000,2000,bitmap,25,25,SKColors.Red,false);writer.Flush();}
            stream.Position=0;var frames=BluRaySupParser.ParseBluRaySup(stream,new StringBuilder(),false,new Dictionary<int,List<PaletteInfo>>(),new Dictionary<int,List<BluRaySupParser.OdsData>>());Assert.Single(frames);Assert.Single(frames[0].PcsObjects);using(var image=frames[0].GetBitmap()){Assert.Equal(55,image.Width);Assert.Equal(25,image.Height);}
        }
    }
    [Fact] public void RemovedWordsOverlayPreservesAcceptedWordsAndRefreshes()
    {
        var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dictionary-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var dictionary=Path.Combine(folder,"ru_RU");
        try
        {
            File.WriteAllText(dictionary+"_se.xml","<words><removed><word>shipped</word></removed></words>");
            File.WriteAllText(dictionary+"_user.xml","<words><word>accepted</word></words>");
            ClassicRemovedDictionaryWords.Save(dictionary,new[]{"ошибочное","ошибочное"});Assert.True(ClassicRemovedDictionaryWords.Contains(dictionary,"ошибочное"));Assert.Contains("accepted",File.ReadAllText(dictionary+"_user.xml"));
            ClassicRemovedDictionaryWords.Save(dictionary,new string[0]);Assert.False(ClassicRemovedDictionaryWords.Contains(dictionary,"ошибочное"));Assert.True(ClassicRemovedDictionaryWords.Contains(dictionary,"shipped"));
        }
        finally{Directory.Delete(folder,true);}
    }
    [Theory][InlineData("fade:0")][InlineData("rotate-in:4")][InlineData("unknown")]
    public void BurnInRejectsInvalidEffectsBeforeWriting(string effect)=>Assert.Throws<FormatException>(()=>ClassicBurnInRunner.ParseEffects(effect));
    [Fact] public void BurnInEffectsUseCueStyleAndScriptResolution()
    {
        var s=new Subtitle();s.Paragraphs.Add(new Paragraph("Hello",0,1000));s.Header=new AdvancedSubStationAlpha().ToText(new Subtitle(),"test");
        ClassicBurnInRunner.ApplyEffects(s,ClassicBurnInRunner.ParseEffects("fade:120, scroll-up, rotate-in"));Assert.Contains("\\fad(120,120)",s.Paragraphs[0].Text);Assert.Contains("\\move(",s.Paragraphs[0].Text);Assert.Contains("\\frx-30",s.Paragraphs[0].Text);
        Assert.DoesNotContain("-movflags",ClassicBurnInRunner.BuildArguments("input.webm","output.webm",null));Assert.Contains("libvpx-vp9",ClassicBurnInRunner.BuildArguments("input.webm","output.webm",null));
    }
 }
}
