using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Ocr;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Ocr
{
    public sealed partial class VobSubNOcrEdit
    {
        private CancellationTokenSource _optimizerCancellation;
        private Button _optimizeButton;
        private NOcrDb _beforeOptimization;
        private static string OL(string ru,string en)=>Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase) ? ru : en;
        private void InitializeClassicOptimizer()
        {
            _optimizeButton=new Button{Name="buttonOptimizeNOcr",Text=OL("Оптимизировать…","Optimize database…"),Size=new Size(145,buttonOK.Height),Location=new Point(buttonOK.Left-153,buttonOK.Top),Anchor=AnchorStyles.Bottom|AnchorStyles.Right};
            _optimizeButton.Click+=async(s,e)=>await OptimizeDatabase();Controls.Add(_optimizeButton);
            FormClosing+=(s,e)=>
            {
                if(_optimizerCancellation!=null){_optimizerCancellation.Cancel();e.Cancel=true;return;}
                if(DialogResult!=DialogResult.OK && _beforeOptimization!=null){_nOcrDb.OcrCharacters=_beforeOptimization.OcrCharacters;_nOcrDb.OcrCharactersExpanded=_beforeOptimization.OcrCharactersExpanded;}
            };
        }
        private static NOcrDb CopyDatabase(NOcrDb db)
        {
            var copy=new NOcrDb(db,db.FileName);copy.OcrCharacters=db.OcrCharacters.Select(c=>new NOcrChar(c)).ToList();copy.OcrCharactersExpanded=db.OcrCharactersExpanded.Select(c=>new NOcrChar(c)).ToList();return copy;
        }
        private async Task OptimizeDatabase()
        {
            if(_optimizerCancellation!=null){_optimizerCancellation.Cancel();return;}
            var candidate=CopyDatabase(_nOcrDb);_optimizerCancellation=new CancellationTokenSource();
            buttonOK.Enabled=buttonImport.Enabled=comboBoxTexts.Enabled=listBoxFileNames.Enabled=groupBoxCurrentCompareImage.Enabled=false;
            _optimizeButton.Text=OL("Отменить…","Cancel…");
            try
            {
                IProgress<Tuple<int,int>> progress=new Progress<Tuple<int,int>>(p=>labelInfo.Text=OL("Оптимизация: ","Optimizing: ")+p.Item1+" / "+p.Item2);
                var result=await Task.Run(()=>NOcrDbOptimizer.Optimize(candidate,new NOcrDbOptimizerSettings(),()=>_optimizerCancellation.IsCancellationRequested,(done,total)=>progress.Report(Tuple.Create(done,total))));
                if(result==null || _optimizerCancellation.IsCancellationRequested)return;
                var message=OL("Линий в базе: ","Database lines: ")+result.LinesBefore+" → "+result.LinesAfter+Environment.NewLine+OL("Проверочных образов: ","Validation glyphs: ")+result.ValidationGlyphs+Environment.NewLine+OL("Применить? Исходный файл базы будет сохранён в отдельную резервную копию.","Apply? The original database file will be backed up separately.");
                if(MessageBox.Show(this,message,Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                if(File.Exists(_nOcrDb.FileName))File.Copy(_nOcrDb.FileName,NOcrDbOptimizer.GetFreeBackupFileName(_nOcrDb.FileName),false);
                if(_beforeOptimization==null)_beforeOptimization=CopyDatabase(_nOcrDb);
                var selected=comboBoxTexts.Text;_nOcrDb.OcrCharacters=candidate.OcrCharacters;_nOcrDb.OcrCharactersExpanded=candidate.OcrCharactersExpanded;Changed=true;FillComboBox();comboBoxTexts.Text=selected;
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
            finally
            {
                _optimizerCancellation.Dispose();_optimizerCancellation=null;buttonOK.Enabled=buttonImport.Enabled=comboBoxTexts.Enabled=listBoxFileNames.Enabled=groupBoxCurrentCompareImage.Enabled=true;
                _optimizeButton.Text=OL("Оптимизировать…","Optimize database…");labelInfo.Text=OL("Элементов в базе: ","Database entries: ")+(_nOcrDb.OcrCharacters.Count+_nOcrDb.OcrCharactersExpanded.Count);
            }
        }
    }
}
