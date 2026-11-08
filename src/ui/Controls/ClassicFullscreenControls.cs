using System;
using System.Drawing;
using System.Windows.Forms;
namespace Nikse.SubtitleEdit.Controls
{
    // An owned window remains above native video surfaces without resizing their render buffers.
    internal sealed class ClassicFullscreenControls:Form
    {
        private readonly VideoPlayerContainer _player;private readonly Timer _timer=new Timer{Interval=40};
        private readonly Label _clock=new Label{AutoSize=true,ForeColor=Color.White,Margin=new Padding(12,19,8,0)};
        private readonly Button _play;private Point _mouse;private DateTime _lastInteraction=DateTime.UtcNow;
        protected override bool ShowWithoutActivation=>true;
        protected override CreateParams CreateParams{get{var value=base.CreateParams;value.ExStyle|=0x08000000;return value;}}
        public ClassicFullscreenControls(VideoPlayerContainer player,Action exit)
        {
            _player=player;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;BackColor=Color.FromArgb(30,30,30);Opacity=0;AutoScaleMode=AutoScaleMode.Dpi;
            var row=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(10,5,10,5)};Controls.Add(row);
            _play=Button(row,"▶ / Ⅱ",()=>player.TogglePlayPause());Button(row,"−5 s",()=>player.CurrentPosition=Math.Max(0,player.CurrentPosition-5));Button(row,"+5 s",()=>player.CurrentPosition=Math.Min(player.Duration,player.CurrentPosition+5));
            Button(row,"CC",()=>{player.PreviewSubtitlesVisible=!player.PreviewSubtitlesVisible;player.LastParagraph=null;});Button(row,"⛶",exit);row.Controls.Add(_clock);
            _mouse=Cursor.Position;_timer.Tick+=(sender,e)=>TickControls();_timer.Start();
        }
        private Button Button(FlowLayoutPanel row,string text,Action action)
        {var button=new Button{Text=text,Width=76,Height=48,FlatStyle=FlatStyle.Flat,ForeColor=Color.White,BackColor=Color.FromArgb(48,48,48),Margin=new Padding(4),TabStop=false};button.Click+=(sender,e)=>{action();_lastInteraction=DateTime.UtcNow;};row.Controls.Add(button);return button;}
        public void Reveal(){_lastInteraction=DateTime.UtcNow;}
        private void TickControls()
        {
            var owner=_player.FindForm();if(owner==null || owner.IsDisposed || owner.WindowState==FormWindowState.Minimized || !_player.Visible){Hide();return;}
            var bounds=_player.RectangleToScreen(_player.ClientRectangle);var mouse=Cursor.Position;
            if(mouse!=_mouse && bounds.Contains(mouse))_lastInteraction=DateTime.UtcNow;_mouse=mouse;
            var focused=Form.ActiveForm==owner || Form.ActiveForm==this;
            var target=focused && ((DateTime.UtcNow-_lastInteraction).TotalSeconds<2.5 || Bounds.Contains(mouse)) ? .95 : 0;
            var width=Math.Min(bounds.Width-24,760);if(width<240){Hide();return;}
            Bounds=new Rectangle(bounds.Left+(bounds.Width-width)/2,bounds.Bottom-86,width,70);
            if(!Visible && target>0)Show(owner);
            Opacity=Math.Max(0,Math.Min(.95,Opacity+Math.Sign(target-Opacity)*Math.Min(.12,Math.Abs(target-Opacity))));
            if(Opacity==0 && target==0)Hide();
            if(_player.VideoPlayer!=null){_play.Text=_player.IsPaused?"▶":"Ⅱ";_clock.Text=TimeSpan.FromSeconds(Math.Max(0,_player.CurrentPosition)).ToString(@"hh\:mm\:ss");}
        }
        protected override void Dispose(bool disposing){if(disposing)_timer.Dispose();base.Dispose(disposing);}
    }
}
