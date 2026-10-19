using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.ClassicEffects;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

namespace Nikse.SubtitleEdit.Forms.Assa
{
    public sealed class ClassicAdvancedEffects : Form
    {
        private readonly Subtitle _source;
        private readonly WavePeakData _wave;
        private readonly string _video;
        private readonly ComboBox _effects=new ComboBox {Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
        private readonly PropertyGrid _properties=new PropertyGrid {Dock=DockStyle.Fill,ToolbarVisible=false};
        private readonly Panel _preview=new Panel {Dock=DockStyle.Fill,BackColor=Color.Black};
        private readonly TextBox _report=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
        private readonly string _folder=Path.Combine(Path.GetTempPath(),"SubtitleEdit-effects-"+Guid.NewGuid().ToString("N"));
        private LibMpvDynamic _player;
        public Subtitle Result {get;private set;}
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public ClassicAdvancedEffects(Subtitle selected,string video,WavePeakData wave)
        {
            _source=new Subtitle(selected,false);_wave=wave;_video=video;
            Text=L("Расширенные ASS-эффекты","Advanced ASS effects");Size=new Size(1050,660);MinimumSize=new Size(760,500);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=2,RowCount=4};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,290));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,35));root.RowStyles.Add(new RowStyle(SizeType.Percent,70));root.RowStyles.Add(new RowStyle(SizeType.Percent,30));root.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
            root.Controls.Add(_effects,0,0);root.Controls.Add(_properties,0,1);root.SetRowSpan(_properties,2);root.Controls.Add(_preview,1,0);root.SetRowSpan(_preview,2);root.Controls.Add(_report,1,2);
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};var apply=new Button{Text=L("Применить","Apply"),AutoSize=true};var play=new Button{Text=L("Предпросмотр","Preview"),AutoSize=true};footer.Controls.Add(cancel);footer.Controls.Add(apply);footer.Controls.Add(play);root.Controls.Add(footer,0,3);root.SetColumnSpan(footer,2);Controls.Add(root);CancelButton=cancel;
            foreach(var effect in AdvancedEffectDisplayFactory.List())_effects.Items.Add(effect);
            _effects.SelectedIndexChanged+=(s,e)=>{_properties.SelectedObject=_effects.SelectedItem;Calculate(false);};_properties.PropertyValueChanged+=(s,e)=>Calculate(false);
            apply.Click+=(s,e)=>{if(Calculate(false)){DialogResult=DialogResult.OK;}};play.Click+=(s,e)=>Calculate(true);_effects.SelectedIndex=0;
            FormClosed+=(s,e)=>{_player?.Dispose();try{if(Directory.Exists(_folder))Directory.Delete(_folder,true);}catch(IOException){}};UiUtil.FixFonts(this);
        }
        private bool Calculate(bool play)
        {
            try
            {
                var effect=(IAdvancedEffectDisplay)_effects.SelectedItem;if(effect==null)return false;
                if(effect.UsesAudio && _wave==null){_report.Text=L("Для этого эффекта сначала создайте осциллограмму.","Generate the waveform before using this audio-driven effect.");return false;}
                var resolution=AdvancedEffectUtil.GetScriptResolution(_source.Header,1280,720);
                var lines=effect.ApplyEffect(_source.Header,_source.Paragraphs.Select(p=>new SubtitleLineViewModel(p)).ToList(),resolution.Width,resolution.Height,_wave);
                if(lines.Count==0)throw new InvalidOperationException(L("Эффект не создал событий; исходные реплики сохранены.","The effect produced no events; source subtitles are retained."));
                if(lines.Count>20000)throw new InvalidOperationException("Effect exceeds 20,000 events.");
                Result=new Subtitle(_source,false);Result.Paragraphs.Clear();Result.Paragraphs.AddRange(lines.Select(l=>l.ToParagraph()));Result.Renumber();
                _report.Text=effect.Description+Environment.NewLine+L("Событий: ","Events: ")+lines.Count+Environment.NewLine+string.Join(Environment.NewLine,lines.Take(12).Select(l=>l.Text));
                if(play && File.Exists(_video))
                {
                    Directory.CreateDirectory(_folder);var path=Path.Combine(_folder,"preview.ass");File.WriteAllText(path,new AdvancedSubStationAlpha().ToText(Result,"Preview"),new UTF8Encoding(false));
                    if(_player==null){_player=new LibMpvDynamic();_player.Initialize(_preview,_video,(s,e)=>{_player.LoadSubtitle(path);_player.CurrentPosition=_source.Paragraphs[0].StartTime.TotalSeconds;_player.Play();},null);}
                    else {_player.LoadSubtitle(path);_player.CurrentPosition=_source.Paragraphs[0].StartTime.TotalSeconds;_player.Play();}
                }
                return true;
            }
            catch(Exception ex){_report.Text=ex.Message;Result=null;return false;}
        }
    }
}
