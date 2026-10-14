using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        private readonly HashSet<string> _silentSpeechRows=new HashSet<string>();
        private bool PrepareSpeechLines()
        {
            var language=LanguageAutoDetect.AutoDetectGoogleLanguage(_subtitle);
            var suggestions=ClassicTtsPreparation.Detect(_subtitle,language);
            if(suggestions.Count==0)return true;
            using(var dialog=new Form{Text=QwenRussian ? "Подготовка реплик к озвучке" : "Prepare speech lines",Size=new Size(850,520),MinimumSize=new Size(650,400),StartPosition=FormStartPosition.CenterParent})
            {
                var grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells};
                grid.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText=QwenRussian ? "Применить" : "Apply",Width=75});
                grid.Columns.Add("action",QwenRussian ? "Действие" : "Action");grid.Columns[1].Width=210;grid.Columns[1].ReadOnly=true;
                grid.Columns.Add("text",QwenRussian ? "Реплики" : "Lines");grid.Columns[2].ReadOnly=true;grid.Columns[2].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;grid.Columns[2].DefaultCellStyle.WrapMode=DataGridViewTriState.True;
                foreach(var item in suggestions)grid.Rows.Add(true,item.Noise ? (QwenRussian ? "Оставить без озвучки" : "Leave silent") : (QwenRussian ? "Объединить продолжение" : "Merge continuation"),item.Preview);
                var note=new Label{Dock=DockStyle.Top,Height=40,Text=QwenRussian ? "Выбери изменения. Шумовые строки останутся в субтитрах. Объединённые реплики применятся к редактору только после OK в окне озвучки." : "Choose changes. Noise annotations stay in the subtitle; merged cues reach the editor only after OK in the speech window."};
                var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=42,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=QwenRussian ? "Отмена" : "Cancel",DialogResult=DialogResult.Cancel};var ok=new Button{Text="OK",DialogResult=DialogResult.OK};bar.Controls.Add(cancel);bar.Controls.Add(ok);dialog.Controls.Add(grid);dialog.Controls.Add(note);dialog.Controls.Add(bar);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
                if(dialog.ShowDialog(this)!=DialogResult.OK)return false;
                grid.EndEdit();var merges=new HashSet<int>();_silentSpeechRows.Clear();
                for(int i=0;i<suggestions.Count;i++)if((bool?)grid.Rows[i].Cells[0].Value==true)
                {var item=suggestions[i];if(item.Noise)_silentSpeechRows.Add(ClassicTtsPreparation.Key(_subtitle.Paragraphs[item.Index]));else merges.Add(item.Index);}
                if(merges.Count>0){var merged=ClassicTtsPreparation.Apply(_subtitle,merges,language);_subtitle.Paragraphs.Clear();_subtitle.Paragraphs.AddRange(merged.Paragraphs);}
                return true;
            }
        }
    }
}
