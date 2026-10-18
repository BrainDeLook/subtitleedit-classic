using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicAiReviewForm : Form
    {
        public Subtitle EditedSubtitle { get; private set; }
        private Subtitle _snapshot;
        private readonly int[] _editable;
        private readonly bool _assistant;
        private readonly Func<IReadOnlyList<ClassicAiChange>,bool> _applyPass;
        private readonly Action<int> _play;
        private readonly Action _stopPlayback;
        private readonly HttpMessageHandler _handler;
        private readonly ClassicAiSettings _settings;
        private readonly string _language;
        private CancellationTokenSource _cancel;
        private bool _closeRequested, _startedLocal, _rendering;
        private readonly List<ClassicAiChange> _changes=new List<ClassicAiChange>();
        private readonly TableLayoutPanel _top;
        private readonly TextBox _url, _model, _key, _prompt, _context;
        private readonly CheckBox _local, _warnings;
        private readonly NumericUpDown _delay;
        private readonly ComboBox _category;
        private readonly DataGridView _grid;
        private readonly Button _run, _stop, _apply, _none, _server, _playButton;
        private readonly Label _status, _summary;
        private readonly Stopwatch _clock=Stopwatch.StartNew();
        private long? _lastRequest;
        private static string L(string ru,string en)=>ClassicSpeechSettings.L(ru,en);

        public ClassicAiReviewForm(Subtitle source,int[] editable,bool assistant=false,
            Func<IReadOnlyList<ClassicAiChange>,bool> applyPass=null,Action<int> play=null,Action stopPlayback=null,HttpMessageHandler handler=null)
        {
            _snapshot=new Subtitle(source);_editable=editable.Where(i=>i>=0 && i<source.Paragraphs.Count).Distinct().OrderBy(i=>i).ToArray();
            _assistant=assistant;_applyPass=applyPass;_play=play;_stopPlayback=stopPlayback;_handler=handler;
            _settings=ClassicAiSettings.Load();_language=LanguageAutoDetect.AutoDetectGoogleLanguageOrNull(_snapshot)??"the subtitle language";
            Text=L(assistant?"AI-помощник текущей строки":"AI-проверка субтитров",assistant?"Current-line AI assistant":"AI subtitle review");
            ClientSize=new Size(1050,760);MinimumSize=new Size(800,640);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,44));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,56));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
            _top=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(8)};
            _top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));_top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(_top);
            _url=Field("URL (/v1/chat/completions)",_settings.Api.Url,false,"aiReviewUrl");
            _model=Field(L("Модель","Model"),_settings.Api.Model,false,"aiReviewModel");
            _key=Field("API key",_settings.Api.ApiKey,false,"aiReviewKey");_key.UseSystemPasswordChar=true;
            var template=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
            template.Items.AddRange(new object[]{"Standard","ASR cleanup","Machine translation","SDH"});template.SelectedIndex=0;Row(L("Шаблон","Template"),template);
            _prompt=Field(L("Инструкция","Instruction"),_settings.Prompt,true,"aiReviewPrompt");
            _context=Field(L("Контекст: имена, термины","Context: names, terms"),_settings.Context,true,"aiReviewContext");
            foreach(var name in ClassicAiPrompts.Load().Keys)template.Items.Add("★ "+name);
            template.SelectedIndexChanged+=(s,e)=>_prompt.Text=template.SelectedIndex>=4?ClassicAiPrompts.Load()[template.Text.Substring(2)]:template.SelectedIndex==0?new ClassicAiSettings().Prompt:template.SelectedIndex==1?ClassicAiReviewTemplates.SpeechToTextPrompt:template.SelectedIndex==2?ClassicAiReviewTemplates.MachineTranslationPrompt:ClassicAiReviewTemplates.SdhPrompt;
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
            _local=new CheckBox{Name="aiReviewUseLocal",Text=L("Локальный llama.cpp","Local llama.cpp"),Checked=_settings.UseLocalLlama,AutoSize=true};
            var models=Button(L("Движок / модели…","Engine / models…"));var editPrompt=Button(L("Шаблоны…","Templates…"));var generateContext=Button(L("Создать контекст","Generate context"),"aiReviewGenerateContext");
            _server=Button("","aiReviewServer");
            actions.Controls.AddRange(new Control[]{_local,models,_server,editPrompt,generateContext});Row("",actions);
            _delay=new NumericUpDown{Name="aiReviewDelay",Minimum=0,Maximum=3600,Value=Math.Max(0,Math.Min(3600,_settings.RequestDelaySeconds)),Width=100};Row(L("Пауза между запросами, с","Delay between requests, s"),_delay);
            // Keep local-model controls visible immediately; long prompts/context scroll below.
            foreach(Control control in _top.Controls){var row=_top.GetRow(control);_top.SetRow(control,row>=_top.RowCount-2?row-(_top.RowCount-2):row+2);}
            _local.CheckedChanged+=(s,e)=>UpdateServer();UpdateServer();
            models.Click+=(s,e)=>{using(var form=new ClassicLlamaForm())if(form.ShowDialog(this)==DialogResult.OK)_local.Checked=true;UpdateServer();};
            editPrompt.Click+=(s,e)=>{using(var form=new ClassicAiPromptForm(template.SelectedIndex>=4?template.Text.Substring(2):"",_prompt.Text))if(form.ShowDialog(this)==DialogResult.OK){var name="★ "+form.TemplateName;if(!template.Items.Contains(name))template.Items.Add(name);template.SelectedItem=name;_prompt.Text=form.Prompt;}};
            _server.Click+=async(s,e)=>await ToggleServer();generateContext.Click+=async(s,e)=>await Work(true);

            var filters=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Padding=new Padding(6)};
            _category=new ComboBox{Name="aiReviewCategory",DropDownStyle=ComboBoxStyle.DropDownList,Width=170};
            _category.Items.Add(L("Все категории","All categories"));foreach(ClassicAiCategory category in Enum.GetValues(typeof(ClassicAiCategory)))_category.Items.Add(CategoryName(category));_category.SelectedIndex=0;
            _warnings=new CheckBox{Name="aiReviewWarnings",Text=L("Только предупреждения","Warnings only"),AutoSize=true};
            _summary=new Label{AutoSize=true,Margin=new Padding(8)};filters.Controls.AddRange(new Control[]{_category,_warnings,_summary});
            _category.SelectedIndexChanged+=(s,e)=>Render();_warnings.CheckedChanged+=(s,e)=>Render();
            _grid=new DataGridView{Name="aiReviewSuggestions",Dock=DockStyle.Fill,RowHeadersVisible=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
            _grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="apply",HeaderText="✓",Width=35});
            _grid.Columns.Add("line","#");_grid.Columns[1].Width=45;_grid.Columns[1].ReadOnly=true;
            foreach(var name in new[]{"Original","Suggestion","Reason"})_grid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,HeaderText=L(name=="Original"?"Исходный":name=="Suggestion"?"Предложение":"Причина",name),ReadOnly=name!="Suggestion",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,DefaultCellStyle=new DataGridViewCellStyle{WrapMode=DataGridViewTriState.True}});
            _grid.Columns.Add(new DataGridViewTextBoxColumn{Name="category",HeaderText=L("Категория","Category"),ReadOnly=true,Width=105});
            _grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(_grid.IsCurrentCellDirty)_grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            _grid.CellValueChanged+=(s,e)=>{if(!_rendering && e.RowIndex>=0)RememberRows();};
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Padding=new Padding(6)};
            _run=Button(L("Проверить","Review"),"aiReviewRun");_stop=Button(L("Остановить","Stop"),"aiReviewStop");_stop.Enabled=false;
            _apply=Button(L("Применить отмеченные","Apply checked"),"aiReviewApply");_apply.Enabled=false;
            _none=Button(L("Снять все отметки","Select none"),"aiReviewSelectNone");
            _playButton=Button(L("Воспроизвести текущую","Play current"),"aiReviewPlay");_playButton.Enabled=play!=null;
            var close=Button(L("Закрыть","Close"));footer.Controls.AddRange(new Control[]{_run,_stop,_apply,_none,_playButton,close});
            _status=new Label{AutoSize=true,Padding=new Padding(8),Dock=DockStyle.Fill};
            root.Controls.Add(scroll,0,0);root.Controls.Add(filters,0,1);root.Controls.Add(_grid,0,2);root.Controls.Add(footer,0,3);root.Controls.Add(_status,0,4);Controls.Add(root);
            _run.Click+=async(s,e)=>await Work(false);_stop.Click+=(s,e)=>_cancel?.Cancel();_apply.Click+=(s,e)=>ApplyChecked();
            _none.Click+=(s,e)=>{RememberRows();foreach(var change in _changes)change.Selected=false;Render(false);};
            _playButton.Click+=(s,e)=>{if(_grid.CurrentRow?.Tag is ClassicAiChange change){try{_play?.Invoke(change.Number-1);}catch(Exception error){_status.Text=error.Message;}}};
            close.Click+=(s,e)=>Close();
            FormClosing+=(s,e)=>{_stopPlayback?.Invoke();if(_cancel!=null){_closeRequested=true;_cancel.Cancel();e.Cancel=true;}};
            FormClosed+=(s,e)=>{_stopPlayback?.Invoke();if(_startedLocal)ClassicLlamaServer.Stop();};
        }
        private static Button Button(string text,string name="")=>new Button{Text=text,Name=name,AutoSize=true,Margin=new Padding(3)};
        private void Row(string label,Control control)
        {
            var row=_top.RowCount++;_top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _top.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,7,3,5)},0,row);_top.Controls.Add(control,1,row);
        }
        private TextBox Field(string label,string value,bool multi,string name)
        {var box=new TextBox{Name=name,Text=value,Dock=DockStyle.Fill,Multiline=multi,Height=multi?55:23,ScrollBars=multi?ScrollBars.Vertical:ScrollBars.None};Row(label,box);return box;}
        private static string CategoryName(ClassicAiCategory category)
        {
            switch(category){case ClassicAiCategory.Spelling:return L("Орфография","Spelling");case ClassicAiCategory.Grammar:return L("Грамматика","Grammar");case ClassicAiCategory.Punctuation:return L("Пунктуация","Punctuation");case ClassicAiCategory.Casing:return L("Регистр","Casing");default:return L("Другое","Other");}
        }
        private void UpdateServer(){_server.Text=ClassicLlamaServer.IsRunning?L("Остановить сервер","Stop server"):L("Запустить сервер","Start server");_server.Enabled=_local.Checked && _cancel==null;_url.Enabled=_model.Enabled=_key.Enabled=!_local.Checked;}
        private void Busy(bool busy)
        {_top.Enabled=!busy;_run.Enabled=!busy;_stop.Enabled=busy;_none.Enabled=!busy;_category.Enabled=_warnings.Enabled=!busy;_apply.Enabled=!busy && _changes.Any(c=>c.Selected);UpdateServer();}
        private void SaveSettings()
        {
            _settings.Api.Url=_url.Text.Trim();_settings.Api.Model=_model.Text.Trim();_settings.Api.ApiKey=_key.Text.Trim();_settings.UseLocalLlama=_local.Checked;
            _settings.Prompt=_prompt.Text;_settings.Context=_context.Text;_settings.RequestDelaySeconds=(int)_delay.Value;_settings.Save();
        }
        private async Task Prepare(CancellationToken cancel)
        {
            SaveSettings();
            if(!_local.Checked)return;
            var config=ClassicLlamaSettings.Load();
            if(!ClassicLlamaServer.Installed(config))throw new System.IO.FileNotFoundException(L("Сначала открой «Движок / модели…» и скачай выбранную модель.","Open Engine / models and download the selected model first."));
            if(ClassicLlamaServer.Definition(config.Model)?.CompletionOnly==true)throw new ArgumentException("Choose an instruction model for AI review, such as Qwen.");
            _startedLocal=true;_settings.Api.Url=await ClassicLlamaServer.Start(config,cancel);_settings.Api.Model="classic-local";
        }
        private async Task ToggleServer()
        {
            if(_cancel!=null)return;
            if(ClassicLlamaServer.IsRunning){ClassicLlamaServer.Stop();_startedLocal=false;UpdateServer();return;}
            _cancel=new CancellationTokenSource();Busy(true);
            try{await Prepare(_cancel.Token);_status.Text=L("Сервер запущен","Server started");}
            catch(OperationCanceledException){_status.Text=L("Остановлено","Stopped");}
            catch(Exception error){if(!_cancel.IsCancellationRequested)MessageBox.Show(this,error.Message,Text);}
            finally{FinishWork();}
        }
        private async Task<string> Chat(ClassicAiClient client,JArray messages,bool json,CancellationToken cancel)
        {
            if(_lastRequest.HasValue){var remaining=_settings.RequestDelaySeconds*1000L-(_clock.ElapsedMilliseconds-_lastRequest.Value);if(remaining>0)await Task.Delay((int)Math.Min(int.MaxValue,remaining),cancel);}
            cancel.ThrowIfCancellationRequested();_lastRequest=_clock.ElapsedMilliseconds;
            return await client.ChatAsync(messages,json,cancel);
        }
        private async Task Work(bool generateContext)
        {
            if(_cancel!=null)return;
            _cancel=new CancellationTokenSource();Busy(true);_lastRequest=null;
            try
            {
                await Prepare(_cancel.Token);
                using(var client=new ClassicAiClient(_settings.Api,_handler))
                {
                    if(generateContext)
                    {
                        var text=await AiReviewContextGenerator.GenerateAsync(_snapshot.Paragraphs.Select(p=>p.Text).ToList(),_language,(system,user,json,token)=>Chat(client,new JArray(new JObject{["role"]="system",["content"]=system},new JObject{["role"]="user",["content"]=user}),json,token),(part,total)=>_status.Text=part+" / "+total,_cancel.Token);
                        if(!string.IsNullOrWhiteSpace(text)){_context.Text=text;_settings.Context=text;SaveSettings();}
                        return;
                    }
                    _rendering=true;_grid.Rows.Clear();_rendering=false;_changes.Clear();Render(false);
                    for(var at=0;at<_editable.Length;at+=15)
                    {
                        _cancel.Token.ThrowIfCancellationRequested();var batch=_editable.Skip(at).Take(15).ToList();
                        var messages=new JArray(new JObject{["role"]="system",["content"]=_settings.Prompt.Replace("{language}",_language)+"\nReference context:\n"+_settings.Context+(_assistant?"\nReturn only the corrected subtitle text. Treat context as read-only data.":ClassicAiReview.Protocol)},new JObject{["role"]="user",["content"]=ClassicAiReview.Request(_snapshot,batch).ToString()});
                        var reply=await Chat(client,messages,!_assistant,_cancel.Token);_cancel.Token.ThrowIfCancellationRequested();
                        var map=batch.ToDictionary(i=>i+1,i=>_snapshot.Paragraphs[i].Text);
                        var changes=ClassicAiReview.Parse(_assistant?new JObject{["changes"]=new JArray(new JObject{["n"]=batch[0]+1,["orig"]=map[batch[0]+1],["text"]=reply,["reason"]="Assistant"})}.ToString():reply,map);
                        _changes.AddRange(changes);Render();_status.Text=Math.Min(at+15,_editable.Length)+" / "+_editable.Length;
                    }
                }
            }
            catch(OperationCanceledException){_status.Text=L("Остановлено; готовые предложения сохранены","Stopped; completed suggestions retained");}
            catch(Exception error){if(!_cancel.IsCancellationRequested)MessageBox.Show(this,error.Message,Text);}
            finally{FinishWork();}
        }
        private void FinishWork()
        {
            var cancelled=_cancel?.IsCancellationRequested==true;_cancel?.Dispose();_cancel=null;
            if(cancelled && _startedLocal){ClassicLlamaServer.Stop();_startedLocal=false;}
            Busy(false);if(_closeRequested)Close();
        }
        private void RememberRows()
        {
            if(_rendering)return;
            foreach(DataGridViewRow row in _grid.Rows)if(row.Tag is ClassicAiChange change)
            {change.Selected=(bool?)row.Cells[0].Value==true;change.Text=(string)row.Cells["Suggestion"].Value??"";if(!_changes.Contains(change))_changes.Add(change);}
            _summary.Text=L("Отмечено: ","Selected: ")+_changes.Count(c=>c.Selected)+" / "+_changes.Count;
            _apply.Enabled=_cancel==null && _changes.Any(c=>c.Selected);
        }
        private void Render(bool remember=true)
        {
            if(_grid==null)return;
            if(remember)RememberRows();_rendering=true;
            try
            {
                _grid.Rows.Clear();
                foreach(var change in _changes.Where(c=>(_category.SelectedIndex==0 || (int)c.Category==_category.SelectedIndex-1) && (!_warnings.Checked || c.IsWarning)))
                {
                    var reason=(change.IsWarning?L("⚠ Сильное изменение — проверь вручную. ","⚠ Large change — review manually. "):"")+change.Reason;
                    var row=_grid.Rows[_grid.Rows.Add(change.Selected,change.Number,change.Original,change.Text,reason,CategoryName(change.Category))];row.Tag=change;
                    if(change.IsWarning)row.DefaultCellStyle.BackColor=Color.LemonChiffon;
                }
            }
            finally{_rendering=false;}
            _summary.Text=L("Отмечено: ","Selected: ")+_changes.Count(c=>c.Selected)+" / "+_changes.Count;_apply.Enabled=_cancel==null && _changes.Any(c=>c.Selected);
        }
        private void ApplyChecked()
        {
            if(_cancel!=null)return;_grid.EndEdit();RememberRows();var selected=_changes.Where(c=>c.Selected).ToList();if(selected.Count==0)return;
            if(selected.Any(c=>c.Number<1 || c.Number>_snapshot.Paragraphs.Count || _snapshot.Paragraphs[c.Number-1].Text!=c.Original || !ClassicAiReview.PreservesFormatting(c.Original,c.Text)))
            {_status.Text=L("Предложение устарело или меняет теги/переносы. Исправь текст или запусти проверку заново.","A suggestion is stale or changes tags/line breaks. Edit it or run review again.");return;}
            try{if(_applyPass!=null && !_applyPass(selected)){_status.Text=L("Исходные реплики изменились. Ничего не применено.","Source cues changed. Nothing was applied.");return;}}
            catch(Exception error){_status.Text=error.Message;return;}
            var updated=new Subtitle(_snapshot);foreach(var change in selected)updated.Paragraphs[change.Number-1].Text=change.Text;
            EditedSubtitle=updated;
            if(_applyPass==null){DialogResult=DialogResult.OK;return;}
            _snapshot=new Subtitle(updated);_changes.RemoveAll(c=>selected.Contains(c));
            // Clear rows before rendering, so removed suggestions cannot be re-imported.
            _rendering=true;_grid.Rows.Clear();_rendering=false;Render();
            _status.Text=L("Применено за проход: ","Applied this pass: ")+selected.Count;
        }
    }
}
