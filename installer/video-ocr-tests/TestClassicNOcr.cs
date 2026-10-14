using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Nikse.SubtitleEdit.Logic.Ocr;
class TestClassicNOcr
{
    static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        var cyrillic=new NOcrDb(args[0]);
        if(cyrillic.OcrCharacters.Count<100 || !cyrillic.OcrCharacters.Any(c=>c.Text=="Ж"))throw new Exception("New Cyrillic database is not compatible with Classic");
        if(NOcrPoint.GetPoints(new Point(4,7),new Point(4,7)).Single()!=new Point(4,7))throw new Exception("Single-point line broken");
        var db=new NOcrDb(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".nocr"));var entry=new NOcrChar{Text="l",Width=10,Height=30};
        for(int i=0;i<200;i++){entry.LinesForeground.Add(new NOcrPoint(new Point(4,0),new Point(4,29)));entry.LinesBackground.Add(new NOcrPoint(new Point(0,0),new Point(0,29)));}db.OcrCharacters.Add(entry);
        var settings=new NOcrDbOptimizerSettings{FontNames=new System.Collections.Generic.List<string>{"Arial"},FontSizes=new System.Collections.Generic.List<float>{40}};
        var aborted=NOcrDbOptimizer.Optimize(db,settings,()=>true);if(aborted!=null || entry.LinesForeground.Count+entry.LinesBackground.Count!=400)throw new Exception("Cancelled optimization mutated database");
        var result=NOcrDbOptimizer.Optimize(db,settings);
        if(result==null || result.ValidationGlyphs<100 || result.LinesBefore!=400 || result.LinesAfter<52 || result.LinesAfter>=400)throw new Exception("Classic optimizer failed to reduce redundant lines");
        Console.WriteLine("PASS: stable Cyrillic database ("+cyrillic.OcrCharacters.Count+" glyphs), single-pixel lines, cancellation without mutation and optimizer "+result.LinesBefore+" → "+result.LinesAfter+" lines / "+result.ValidationGlyphs+" validation glyphs");
    }
}
