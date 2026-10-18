using System;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ToolStripMenuItem _classicMergeContinuation, _classicUnicodeCharacters;
        private void InitializeClassicTextTools()
        {
            if(_classicMergeContinuation==null)
            {
                _classicMergeContinuation=new ToolStripMenuItem { Name="classicMergeContinuation" }; _classicUnicodeCharacters=new ToolStripMenuItem { Name="classicUnicodeCharacters" };
                toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {new ToolStripSeparator(),_classicMergeContinuation,_classicUnicodeCharacters});
                _classicMergeContinuation.Click+=(s,e)=>ShowClassicTextTools(false); _classicUnicodeCharacters.Click+=(s,e)=>ShowClassicTextTools(true);
            }
            _classicMergeContinuation.Text=ClassicTextTools.L("Объединить незаконченные фразы…", "Merge continuation lines…"); _classicUnicodeCharacters.Text=ClassicTextTools.L("Удалить / заменить Unicode-символы…", "Remove / replace Unicode characters…");
        }
        private void ShowClassicTextTools(bool unicode)
        {
            if(!IsSubtitleLoaded){DisplaySubtitleNotLoadedMessage();return;} ReloadFromSourceView(); var baseline=new Subtitle(_subtitle,false);
            using(var form=new ClassicTextToolsForm(baseline,unicode)) if(form.ShowDialog(this)==DialogResult.OK)ApplyClassicTextTools(baseline,form.Result,unicode);
        }
        private bool ApplyClassicTextTools(Subtitle baseline,Subtitle result,bool unicode)
        {
            if(result==null || ClassicCompareContent(_subtitle)!=ClassicCompareContent(baseline))return false;
            if(ClassicCompareContent(result)==ClassicCompareContent(baseline))return true;
            MakeHistoryForUndo(unicode?"Before Unicode character replacement":"Before merging continuation lines"); SaveSubtitleListviewIndices();
            _subtitle.Paragraphs.Clear(); _subtitle.Paragraphs.AddRange(result.Paragraphs.Select(p=>new Paragraph(p,false))); SubtitleListview1.Fill(_subtitle,_subtitleOriginal); RestoreSubtitleListviewIndices();
            audioVisualizer.Invalidate(); UpdateSourceView(); SetTitle(); return true;
        }
    }
}
