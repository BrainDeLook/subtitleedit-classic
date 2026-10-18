using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class BatchConvert
    {
        private readonly List<ClassicLineError> _classicBatchErrors=new List<ClassicLineError>();
        private int _classicBatchErrorLines;
        private Button _classicListErrorsButton;
        private void InitializeClassicBatchErrors()
        {
            _classicListErrorsButton=new Button {Name="classicBatchListErrors",Text=ClassicErrorReport.Title+"…",Width=145,Height=buttonConvert.Height,Anchor=AnchorStyles.Right|AnchorStyles.Bottom};
            _classicListErrorsButton.SetBounds(buttonConvert.Left-_classicListErrorsButton.Width-8,buttonConvert.Top,_classicListErrorsButton.Width,buttonConvert.Height);Controls.Add(_classicListErrorsButton);
            progressBar1.Width=Math.Max(50,_classicListErrorsButton.Left-progressBar1.Left-10);
            _classicListErrorsButton.Click+=(s,e)=> {using(var form=new ClassicErrorList(_classicBatchErrors,_classicBatchErrorLines,"",true))form.ShowDialog(this);};
        }
        private void BeginClassicBatchErrors() {_classicBatchErrors.Clear();_classicBatchErrorLines=0;_classicListErrorsButton.Enabled=false;}
        private void CompleteClassicBatchErrors() {_classicListErrorsButton.Enabled=true;}
        private void RecordClassicBatchErrors(Subtitle subtitle,SubtitleFormat format,string fileName)
        {
            // Only successful outputs reach here; empty bitmap text cannot be diagnosed as OCR text.
            if(!subtitle.Paragraphs.Any(p=>!string.IsNullOrEmpty(p.Text)))return;
            _classicBatchErrorLines+=subtitle.Paragraphs.Count;_classicBatchErrors.AddRange(ClassicErrorReport.Analyze(subtitle,format,fileName));
        }
    }
}
