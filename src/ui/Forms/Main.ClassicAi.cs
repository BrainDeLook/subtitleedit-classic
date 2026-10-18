using System.Linq;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private void InitializeClassicAiMenu()
        {
            if (toolsToolStripMenuItem.DropDownItems.ContainsKey("classicAiReview")) return;
            var review = new ToolStripMenuItem(VoiceReferenceRu ? "AI-проверка субтитров…" : "AI subtitle review…") { Name = "classicAiReview" };
            var assistant = new ToolStripMenuItem(VoiceReferenceRu ? "AI-помощник текущей строки…" : "Current-line AI assistant…") { Name = "classicAiAssistant" };
            review.Click += (s,e) => OpenClassicAiReview(false);
            assistant.Click += (s,e) => OpenClassicAiReview(true);
            toolsToolStripMenuItem.DropDownItems.Add(review); toolsToolStripMenuItem.DropDownItems.Add(assistant);
            var local=new ToolStripMenuItem(VoiceReferenceRu ? "Локальные AI-модели…" : "Local AI models…") { Name="classicLlamaModels" };
            local.Click+=(s,e)=>{using(var form=new ClassicLlamaForm())form.ShowDialog(this);};toolsToolStripMenuItem.DropDownItems.Add(local);
        }
        private int[] ClassicAiReviewIndices(bool assistant,bool selectedOnly)
        {
            if(assistant)return FirstSelectedIndex>=0?new[]{FirstSelectedIndex}:new int[0];
            return selectedOnly?SubtitleListview1.GetSelectedIndices().Where(i=>i>=0 && i<_subtitle.Paragraphs.Count).ToArray():Enumerable.Range(0,_subtitle.Paragraphs.Count).ToArray();
        }
        private void AddClassicAiSelectedMenu()
        {
            if(SubtitleListview1.SelectedItems.Count==0)return;
            var item=new ToolStripMenuItem(VoiceReferenceRu?"AI-проверка субтитров…":"AI subtitle review…"){Name="classicAiReviewSelected",Tag="(REMOVE)"};
            item.Click+=(s,e)=>OpenClassicAiReview(false,true);toolStripMenuItemSelectedLines.DropDownItems.Insert(0,item);
        }
        private void OpenClassicAiReview(bool assistant,bool selectedOnly=false)
        {
            if (_subtitle.Paragraphs.Count == 0) return;
            var index = FirstSelectedIndex;
            if (assistant && index < 0) return;
            var indices=ClassicAiReviewIndices(assistant,selectedOnly);if(indices.Length==0)return;
            var previewSerial=0;var previewActive=false;
            Action stopPlayback=()=>{previewSerial++;if(previewActive && !IsDisposed && mediaPlayer.VideoPlayer!=null)mediaPlayer.Pause();previewActive=false;};
            Action<int> play=at=>
            {
                if(mediaPlayer.VideoPlayer==null || at<0 || at>=_subtitle.Paragraphs.Count)return;
                var serial=++previewSerial;var cue=_subtitle.Paragraphs[at];previewActive=true;mediaPlayer.Pause();mediaPlayer.CurrentPosition=cue.StartTime.TotalSeconds;mediaPlayer.Play();
                TaskDelayHelper.RunDelayed(TimeSpan.FromSeconds(Math.Max(.1,cue.Duration.TotalSeconds)),()=>{if(!IsDisposed && serial==previewSerial){if(mediaPlayer.VideoPlayer!=null)mediaPlayer.Pause();previewActive=false;}});
            };
            using(var form=new ClassicAiReviewForm(_subtitle,indices,assistant,ApplyClassicAiPass,mediaPlayer.VideoPlayer==null?null:play,stopPlayback))
                form.ShowDialog(this);
        }
        private bool ApplyClassicAiPass(IReadOnlyList<ClassicAiChange> changes)
        {
            if(changes.Count==0 || changes.Select(c=>c.Number).Distinct().Count()!=changes.Count || changes.Any(c=>c.Number<1 || c.Number>_subtitle.Paragraphs.Count || _subtitle.Paragraphs[c.Number-1].Text!=c.Original || !ClassicAiReview.PreservesFormatting(c.Original,c.Text)))return false;
            var index=FirstSelectedIndex;MakeHistoryForUndo("Before AI review pass");
            foreach(var change in changes)_subtitle.Paragraphs[change.Number-1].Text=change.Text;
            SubtitleListview1.Fill(_subtitle,_subtitleOriginal);_subtitleListViewIndex=-1;
            if(index>=0)SubtitleListview1.SelectIndexAndEnsureVisibleFaster(index);
            audioVisualizer.Invalidate();UpdateSourceView();SetTitle();return true;
        }
    }
}
