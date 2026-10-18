using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicStatRange
    {
        public string Label { get; set; }
        public double Minimum { get; set; }
        public double Average { get; set; }
        public double Maximum { get; set; }
        public int Samples { get; set; }
        public double AverageFraction => Math.Max(0,Math.Min(1,Average/(Maximum>0?Maximum:1)));
    }
    public sealed class ClassicStatCheck
    {
        public string Label { get; set; }
        public int Count { get; set; }
        public int Total { get; set; }
        public bool Critical { get; set; }
        public double Percent => Total>0?100.0*Count/Total:0;
    }
    // Stable 5.3 dashboard metrics; independent from the rendered report and user's subtitle.
    public sealed class ClassicStatisticsData
    {
        public int SubtitleCount { get; private set; }
        public int Words { get; private set; }
        public decimal Characters { get; private set; }
        public double DurationMilliseconds { get; private set; }
        public int[] CpsBins { get; } = new int[15];
        public int CpsSamples { get; private set; }
        public double OptimalCps { get; private set; }
        public double MaximumCps { get; private set; }
        public List<ClassicStatRange> Ranges { get; } = new List<ClassicStatRange>();
        public List<ClassicStatCheck> Checks { get; } = new List<ClassicStatCheck>();
        public string DurationText
        {
            get {var time=TimeSpan.FromMilliseconds(DurationMilliseconds);return ((long)time.TotalHours).ToString("00")+":"+time.Minutes.ToString("00")+":"+time.Seconds.ToString("00")+","+time.Milliseconds.ToString("000");}
        }
        private static string L(string ru,string en)=>ClassicTextTools.L(ru,en);
        private static bool Timed(Paragraph p)=>!p.StartTime.IsMaxTime && !p.EndTime.IsMaxTime && !double.IsNaN(p.StartTime.TotalMilliseconds) && !double.IsInfinity(p.StartTime.TotalMilliseconds) && !double.IsNaN(p.EndTime.TotalMilliseconds) && !double.IsInfinity(p.EndTime.TotalMilliseconds);
        private void Range(string label,List<double> values) {Ranges.Add(new ClassicStatRange {Label=label,Minimum=values.Count==0?0:values.Min(),Average=values.Count==0?0:values.Average(),Maximum=values.Count==0?0:values.Max(),Samples=values.Count});}
        public static ClassicStatisticsData Calculate(Subtitle source)
        {
            var result=new ClassicStatisticsData();var g=Configuration.Settings.General;
            result.SubtitleCount=source.Paragraphs.Count;result.OptimalCps=g.SubtitleOptimalCharactersPerSeconds;result.MaximumCps=g.SubtitleMaximumCharactersPerSeconds;
            var lengths=new List<double>();var durations=new List<double>();var cps=new List<double>();var wpm=new List<double>();var gaps=new List<double>();
            int physical=0,longLines=0,tooMany=0,wide=0,invalid=0;
            for(int i=0;i<source.Paragraphs.Count;i++)
            {
                var p=source.Paragraphs[i];var text=HtmlUtil.RemoveHtmlTags(p.Text??"",true);var lines=text.SplitToLines();var length=text.Replace("\r","").Replace("\n","").CountCharacters(g.CpsLineLengthStrategy,false);
                result.Words+=text.CountWords();result.Characters+=text.CountCharacters(false);lengths.Add(length);
                if(p.NumberOfLines>g.MaxNumberOfLines)tooMany++;
                foreach(var line in lines) {physical++;if(line.CountCharacters(g.CpsLineLengthStrategy,false)>g.SubtitleLineMaximumLength)longLines++;if(Configuration.Settings.Tools.ListViewSyntaxColorWideLines && TextWidth.CalcPixelWidth(line)>g.SubtitleLineMaximumPixelWidth)wide++;}
                if(Timed(p) && !double.IsInfinity(p.DurationTotalMilliseconds) && !double.IsNaN(p.DurationTotalMilliseconds) && p.DurationTotalMilliseconds>=0)
                {
                    durations.Add(p.DurationTotalMilliseconds/1000.0);result.DurationMilliseconds+=p.DurationTotalMilliseconds;
                    if(p.DurationTotalMilliseconds>0)
                    {
                        var speed=p.GetCharactersPerSecond();var wordSpeed=p.WordsPerMinute;
                        if(!double.IsNaN(speed) && !double.IsInfinity(speed) && speed>=0) {cps.Add(speed);result.CpsBins[Math.Max(0,Math.Min(14,(int)Math.Min(14,speed/2)))]++;result.CpsSamples++;}
                        if(!double.IsNaN(wordSpeed) && !double.IsInfinity(wordSpeed) && wordSpeed>=0)wpm.Add(wordSpeed);
                    }
                    else invalid++;
                }
                else invalid++;
                if(i+1<source.Paragraphs.Count && Timed(p) && Timed(source.Paragraphs[i+1]))gaps.Add(source.Paragraphs[i+1].StartTime.TotalMilliseconds-p.EndTime.TotalMilliseconds);
            }
            result.Range(L("Символов в реплике", "Characters per subtitle"),lengths);result.Range(L("Длительность, с", "Duration, s"),durations);result.Range(L("Символов в секунду", "Characters per second"),cps);result.Range(L("Слов в минуту", "Words per minute"),wpm);
            if(source.Paragraphs.Count>1)result.Range(L("Зазор, мс", "Gap, ms"),gaps);
            Action<string,int,int,bool> check=(label,count,total,critical)=>result.Checks.Add(new ClassicStatCheck {Label=label,Count=count,Total=total,Critical=critical});
            check(L("CPS выше максимума", "CPS above maximum")+" ("+result.MaximumCps+")",cps.Count(v=>v>result.MaximumCps),cps.Count,true);
            check(L("CPS выше оптимального", "CPS above optimal")+" ("+result.OptimalCps+")",cps.Count(v=>v>result.OptimalCps),cps.Count,false);
            check(L("WPM выше максимума", "WPM above maximum")+" ("+g.SubtitleMaximumWordsPerMinute+")",wpm.Count(v=>v>g.SubtitleMaximumWordsPerMinute),wpm.Count,false);
            check(L("Длительность ниже минимума", "Duration below minimum")+" ("+(g.SubtitleMinimumDisplayMilliseconds/1000.0)+" s)",durations.Count(v=>v*1000<g.SubtitleMinimumDisplayMilliseconds),durations.Count,true);
            check(L("Длительность выше максимума", "Duration above maximum")+" ("+(g.SubtitleMaximumDisplayMilliseconds/1000.0)+" s)",durations.Count(v=>v*1000>g.SubtitleMaximumDisplayMilliseconds),durations.Count,false);
            check(L("Слишком длинных строк", "Overlong physical lines")+" ("+g.SubtitleLineMaximumLength+")",longLines,physical,false);
            check(L("Слишком много строк в реплике", "Too many lines per subtitle")+" ("+g.MaxNumberOfLines+")",tooMany,result.SubtitleCount,false);
            if(Configuration.Settings.Tools.ListViewSyntaxColorWideLines)check(L("Слишком широких строк", "Overwide physical lines")+" ("+g.SubtitleLineMaximumPixelWidth+" px)",wide,physical,false);
            if(source.Paragraphs.Count>1)check(L("Зазор ниже минимума", "Gap below minimum")+" ("+g.MinimumMillisecondsBetweenLines+" ms)",gaps.Count(v=>v<g.MinimumMillisecondsBetweenLines),gaps.Count,true);
            check(L("Нет корректной длительности", "Missing or invalid duration"),invalid,result.SubtitleCount,true);
            return result;
        }
        public string Report()
        {
            var text=new StringBuilder();text.AppendLine(L("Обзор статистики", "Statistics dashboard"));text.AppendLine(L("Реплик: ", "Subtitles: ")+SubtitleCount);text.AppendLine(L("Слов: ", "Words: ")+Words);text.AppendLine(L("Символов: ", "Characters: ")+Characters);text.AppendLine(L("Суммарная длительность: ", "Total cue duration: ")+DurationText);
            text.AppendLine();text.AppendLine(L("Показатель | Мин. | Сред. | Макс. | Выборка", "Metric | Min | Average | Max | Samples"));foreach(var range in Ranges)text.AppendLine(range.Label+" | "+range.Minimum.ToString("0.##")+" | "+range.Average.ToString("0.##")+" | "+range.Maximum.ToString("0.##")+" | "+range.Samples);
            text.AppendLine();text.AppendLine(L("Проверки", "Checks"));foreach(var check in Checks)text.AppendLine(check.Label+": "+check.Count+" / "+check.Total+" ("+check.Percent.ToString("0.#")+"%)");
            text.AppendLine();text.AppendLine(L("Гистограмма CPS (шаг 2; последний столбец 28+)", "CPS histogram (step 2; final bin 28+ )"));for(int i=0;i<CpsBins.Length;i++)text.AppendLine((i*2).ToString()+(i==14?"+":"–"+(i*2+2))+": "+CpsBins[i]);text.AppendLine(L("Оптимум / максимум: ", "Optimal / maximum: ")+OptimalCps+" / "+MaximumCps);return text.ToString();
        }
    }
}
