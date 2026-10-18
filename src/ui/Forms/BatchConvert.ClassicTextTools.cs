using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.CommandLineConvert;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class BatchConvert
    {
        private NumericUpDown _classicMergeGap, _classicMergeCharacters;
        private ClassicTextToolSettings _classicTextSettings;
        private void InitializeClassicBatchTextTools(List<FixActionItem> items)
        {
            _classicTextSettings=ClassicTextToolSettings.Load();
            var merge=NewClassicTextGroup(ClassicTextTools.L("Объединить незаконченные фразы", "Merge continuation lines"));
            var layout=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(10) }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            _classicMergeGap=new NumericUpDown { Minimum=0,Maximum=10000,Value=_classicTextSettings.MaxGap,Dock=DockStyle.Top }; _classicMergeCharacters=new NumericUpDown { Minimum=20,Maximum=1000,Value=_classicTextSettings.MaxCharacters,Dock=DockStyle.Top };
            layout.Controls.Add(new Label { AutoSize=true,Text=ClassicTextTools.L("Максимальный разрыв, мс", "Maximum gap, ms") },0,0); layout.Controls.Add(_classicMergeGap,1,0);
            layout.Controls.Add(new Label { AutoSize=true,Text=ClassicTextTools.L("Максимум символов", "Maximum characters") },0,1); layout.Controls.Add(_classicMergeCharacters,1,1); merge.Controls.Add(layout);
            var actors=NewClassicTextGroup(LanguageSettings.Current.ConvertActor.Title);
            var actorLayout=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(10) };
            var description=new Label { AutoSize=true,MaximumSize=new Size(350,0) }; Action refresh=()=>description.Text=ActorSettingsDescription(_classicTextSettings.Actors); refresh();
            var configure=new Button { AutoSize=true,Text=ClassicTextTools.L("Настроить преобразование…", "Configure actor conversion…") };
            configure.Click+=(s,e)=> { using(var form=new ConvertActor()) { form.Initialize(new Subtitle(),GetCurrentSubtitleFormat()); if(form.ShowDialog(this)==DialogResult.OK) { _classicTextSettings.Actors=form.Options; refresh(); } } };
            actorLayout.Controls.Add(description); actorLayout.Controls.Add(configure); actors.Controls.Add(actorLayout);
            var controls=NewClassicTextGroup(ClassicTextTools.L("Удалить управляющие Unicode-символы", "Remove Unicode control characters"));
            controls.Controls.Add(new Label { Dock=DockStyle.Fill,Padding=new Padding(10),Text=ClassicTextTools.L("Удаление невидимых управляющих символов направления письма. Буквы, знаки и эмодзи сохраняются.", "Remove invisible direction controls. Letters, punctuation and emoji are preserved.") });
            items.Add(new FixActionItem { Text=merge.Text,Control=merge,Action=CommandLineConverter.BatchAction.MergeContinuationLines,Checked=_classicTextSettings.BatchMerge });
            items.Add(new FixActionItem { Text=actors.Text,Control=actors,Action=CommandLineConverter.BatchAction.ConvertActors,Checked=_classicTextSettings.BatchActors });
            items.Add(new FixActionItem { Text=controls.Text,Control=controls,Action=CommandLineConverter.BatchAction.RemoveUnicodeControlChars,Checked=_classicTextSettings.BatchControls });
        }
        private GroupBox NewClassicTextGroup(string title) { var group=new GroupBox { Text=title,Visible=false }; groupBoxConvertOptions.Controls.Add(group); return group; }
        private static string ActorSettingsDescription(ClassicActorOptions o)
        {
            var types=new[]{"[Name]", "(Name)", "Name:",LanguageSettings.Current.General.Actor};
            return types[o.From]+" → "+types[o.To]+Environment.NewLine+(o.OnlyNames?LanguageSettings.Current.ConvertActor.OnlyNames:ClassicTextTools.L("Все подписи", "All labels"));
        }
        private void CaptureClassicBatchTextTools()
        {
            _classicTextSettings.MaxGap=(int)_classicMergeGap.Value; _classicTextSettings.MaxCharacters=(int)_classicMergeCharacters.Value;
            _classicTextSettings.BatchMerge=IsActionEnabled(CommandLineConverter.BatchAction.MergeContinuationLines); _classicTextSettings.BatchActors=IsActionEnabled(CommandLineConverter.BatchAction.ConvertActors); _classicTextSettings.BatchControls=IsActionEnabled(CommandLineConverter.BatchAction.RemoveUnicodeControlChars);
            // Preserve Unicode mappings edited by another owned dialog while batch settings were open.
            _classicTextSettings.Replacements=ClassicTextToolSettings.Load().Replacements; _classicTextSettings.Save();
        }
        private Subtitle ApplyClassicBatchTextTools(Subtitle source)
        {
            var settings=_classicTextSettings;
            if(IsActionEnabled(CommandLineConverter.BatchAction.ConvertActors))source=ClassicTextTools.ConvertActors(source,GetCurrentSubtitleFormat()??source.OriginalFormat??new SubRip(),settings.Actors);
            if(IsActionEnabled(CommandLineConverter.BatchAction.MergeContinuationLines))
            {
                var language=LanguageAutoDetect.AutoDetectGoogleLanguage(source);
                source=ClassicTextTools.Merge(source,new HashSet<int>(ClassicTextTools.DetectMerges(source,language,settings.MaxGap,settings.MaxCharacters).Select(c=>c.Index)),language);
            }
            if(IsActionEnabled(CommandLineConverter.BatchAction.RemoveUnicodeControlChars)) foreach(var p in source.Paragraphs)p.Text=Utilities.RemoveUnicodeControlChars(p.Text);
            return source;
        }
    }
}
