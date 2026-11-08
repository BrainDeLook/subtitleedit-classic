using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms
{
 public sealed class ClassicCinemaMxfForm:Form
 {
  private CancellationTokenSource _operation;
  private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
  public ClassicCinemaMxfForm(Subtitle subtitle,string source)
  {
   UiUtil.PreInitialize(this);var snapshot=new Subtitle(subtitle);Text=L("D-Cinema MXF: текстовые субтитры","D-Cinema MXF: text subtitles");Size=new Size(760,330);MinimumSize=new Size(620,330);StartPosition=FormStartPosition.CenterParent;
   var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,RowCount=6};table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   var title=new TextBox{Text=Path.GetFileNameWithoutExtension(source)??"Subtitles",Dock=DockStyle.Fill};var font=new TextBox{Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),Dock=DockStyle.Fill,ReadOnly=true};
   var fontRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,AutoSize=true};fontRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fontRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));var browse=new Button{Text="…",AutoSize=true};fontRow.Controls.Add(font);fontRow.Controls.Add(browse,1,0);
   browse.Click+=(s,e)=>{using(var file=new OpenFileDialog{Filter="OpenType / TrueType|*.otf;*.ttf",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.Fonts)})if(file.ShowDialog(this)==DialogResult.OK)font.Text=file.FileName;};
   var fps=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=120};fps.Items.AddRange(new object[]{24,25,30,48,50,60});fps.SelectedIndex=0;
   var frames=new NumericUpDown{Minimum=0,Maximum=100000000,Width=150};var hint=new Label{Text=L("Длина катушки в кадрах: 0 — до конца последней реплики. Выбранный шрифт встраивается в MXF для всех реплик.","Reel length in frames: 0 ends at the last subtitle. The selected font is embedded in MXF for all lines."),AutoSize=true,MaximumSize=new Size(690,0)};
   var labels=new[]{L("Название","Title"),L("Встроенный шрифт","Embedded font"),L("Частота кадров","Frame rate"),L("Длина катушки, кадров","Reel length, frames")};var controls=new Control[]{title,fontRow,fps,frames};for(int i=0;i<4;i++){table.Controls.Add(new Label{Text=labels[i],AutoSize=true},0,i);table.Controls.Add(controls[i],1,i);}
   table.Controls.Add(hint,0,4);table.SetColumnSpan(hint,2);var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var export=new Button{Text=L("Экспорт MXF…","Export MXF…"),AutoSize=true};var close=new Button{Text=L("Закрыть / отменить","Close / cancel"),AutoSize=true};var status=new Label{AutoSize=true};buttons.Controls.AddRange(new Control[]{export,close,status});table.Controls.Add(buttons,0,5);table.SetColumnSpan(buttons,2);Controls.Add(table);
   close.Click+=(s,e)=>{if(_operation!=null)_operation.Cancel();else Close();};FormClosing+=(s,e)=>{if(_operation!=null){e.Cancel=true;_operation.Cancel();}};
   export.Click+=async(s,e)=>
   {
    using(var save=new SaveFileDialog{Filter="SMPTE ST 429-5 timed text|*.mxf",FileName=(Path.GetFileNameWithoutExtension(source)??"subtitles")+".mxf"})
    {
     if(save.ShowDialog(this)!=DialogResult.OK)return;
     _operation=new CancellationTokenSource();export.Enabled=browse.Enabled=title.Enabled=fps.Enabled=frames.Enabled=false;
     try{if(frames.Value>0 && frames.Value<(decimal)Math.Ceiling(snapshot.Paragraphs.Max(p=>p.EndTime.TotalSeconds)*(int)fps.SelectedItem))throw new ArgumentException(L("Длина катушки меньше конца последней реплики.","The reel is shorter than the last subtitle."));if(!string.IsNullOrEmpty(source))ClassicMediaJobs.CheckOutput(source,save.FileName);var xml=ClassicCinemaMxf.CreateXml(snapshot,title.Text,(int)fps.SelectedItem);await ClassicCinemaMxf.Export(xml,font.Text,save.FileName,(long)frames.Value,_operation.Token);status.Text=L("Готово","Saved");}
     catch(OperationCanceledException){status.Text=L("Отменено","Cancelled");}
     catch(Exception ex)when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException || ex is System.Xml.XmlException){MessageBox.Show(this,ex.Message,Text);}
     finally{_operation.Dispose();_operation=null;export.Enabled=browse.Enabled=title.Enabled=fps.Enabled=frames.Enabled=true;}
    }
   };UiUtil.FixFonts(this);
  }
 }
}
