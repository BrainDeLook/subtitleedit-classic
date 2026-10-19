using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Styles;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms.Assa
{
    public sealed partial class AssaStyles
    {
        private ClassicStyleAnswer AskClassicStyleConflict(string name,bool multiple)
        {
            using(var form=new ClassicStyleConflictDialog(name,multiple))
                return form.ShowDialog(this)==DialogResult.OK?new ClassicStyleAnswer{Choice=form.Choice,ForAll=form.ForAll}:new ClassicStyleAnswer{Choice=ClassicStyleConflict.Cancel};
        }
        private bool TransferClassicStyles(IEnumerable<SsaStyle> source,bool storage,Func<string,bool,ClassicStyleAnswer> ask)
        {
            LogNameChanges();var list=storage?listViewStorage:listViewStyles;var target=storage?_currentCategory.Styles:_currentFileStyles;
            var plan=ClassicStyleTransfer.Plan(source,target,ask??AskClassicStyleConflict);if(plan==null)return false;
            target.Clear();target.AddRange(plan.Styles);RebuildClassicStyleList(list,plan.AffectedNames);
            labelStatus.Text=ClassicAssistedText.L("Обновлены стили: ","Styles updated: ")+string.Join(", ",plan.AffectedNames);timerClearStatus.Start();return true;
        }
        private void RebuildClassicStyleList(ListView list,IEnumerable<string> selected)
        {
            var names=new HashSet<string>(selected,StringComparer.OrdinalIgnoreCase);var styles=list==listViewStorage?_currentCategory.Styles:_currentFileStyles;
            list.SelectedIndexChanged-=listViewStyles_SelectedIndexChanged;list.SelectedIndexChanged-=listViewStorage_SelectedIndexChanged;list.BeginUpdate();_doUpdate=false;
            try{list.Items.Clear();foreach(var style in styles){AddStyle(list,style,_subtitle,_isSubStationAlpha);list.Items[list.Items.Count-1].Selected=names.Contains(style.Name);}}
            finally{list.EndUpdate();if(list==listViewStorage)list.SelectedIndexChanged+=listViewStorage_SelectedIndexChanged;else list.SelectedIndexChanged+=listViewStyles_SelectedIndexChanged;}
            if(list==listViewStorage)listViewStorage_Enter(null,EventArgs.Empty);else listViewStyles_Enter(null,EventArgs.Empty);
            if(list.SelectedItems.Count>0)list.SelectedItems[0].EnsureVisible();
        }
        private void ImportClassicStyles(bool storage)
        {
            openFileDialogImport.Title=LanguageSettings.Current.SubStationAlphaStyles.ImportStyleFromFile;openFileDialogImport.InitialDirectory=Configuration.DataDirectory;
            openFileDialogImport.Filter="ASS/SSA|*.ass;*.ssa|"+LanguageSettings.Current.General.AllFiles+"|*.*";
            if(openFileDialogImport.ShowDialog(this)!=DialogResult.OK)return;
            try
            {
                var source=Subtitle.Parse(openFileDialogImport.FileName);if(source==null || !(source.OriginalFormat is AdvancedSubStationAlpha || source.OriginalFormat is SubStationAlpha))
                {labelStatus.Text=ClassicAssistedText.L("В файле не найдены стили ASS/SSA.","No ASS/SSA styles found in this file.");return;}
                using(var select=new ChooseStyle(source,source.OriginalFormat is SubStationAlpha))
                    if(select.ShowDialog(this)==DialogResult.OK && select.SelectedStyleNames.Count>0)ImportClassicStyleSelection(source,select.SelectedStyleNames,storage,null);
            }
            catch(Exception ex)when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {labelStatus.Text=ex.Message;}
        }
        private bool ImportClassicStyleSelection(Subtitle source,IEnumerable<string> names,bool storage,Func<string,bool,ClassicStyleAnswer> ask)
        {
            return TransferClassicStyles(names.Select(n=>ClassicStyleTransfer.Convert(AdvancedSubStationAlpha.GetSsaStyle(n,source.Header),source.OriginalFormat is SubStationAlpha,_isSubStationAlpha)),storage,ask);
        }
        private void CopyClassicStyles(bool storage)
        {
            var list=storage?listViewStyles:listViewStorage;var source=storage?_currentFileStyles:_currentCategory.Styles;
            var names=list.SelectedItems.Cast<ListViewItem>().Select(i=>i.Text).ToList();if(names.Count==0)return;
            TransferClassicStyles(names.Select(n=>ClassicStyleTransfer.Convert(source.Single(s=>s.Name==n),storage?_isSubStationAlpha:false,storage?false:_isSubStationAlpha)),storage,null);
        }
        private void MoveClassicStyles(ListView list,string move)
        {
            if(list.SelectedItems.Count==0)return;LogNameChanges();var styles=list==listViewStorage?_currentCategory.Styles:_currentFileStyles;
            var selected=list.SelectedIndices.Cast<int>().ToList();var names=selected.Select(i=>styles[i].Name).ToList();var old=styles.ToList();
            var order=ClassicStyleTransfer.Order(styles.Count,selected,move);styles.Clear();styles.AddRange(order.Select(i=>old[i]));RebuildClassicStyleList(list,names);
        }
    }
}
