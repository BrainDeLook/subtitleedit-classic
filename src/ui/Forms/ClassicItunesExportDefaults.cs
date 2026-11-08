using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms
{
 public sealed class ClassicItunesExportDefaults : Form
 {
  private readonly TextBox _color=new TextBox{Dock=DockStyle.Fill};
  private readonly ComboBox _style=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
  private readonly ComboBox _region=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
  private readonly TextBox _header=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,AcceptsReturn=true,AcceptsTab=true};
  public string ColorValue=>_color.Text.Trim(); public string StyleValue=>_style.Text; public string RegionValue=>_region.Text; public string HeaderValue=>_header.Text.Trim();
  private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
  public ClassicItunesExportDefaults(string color,string style,string region,string header)
  {
   UiUtil.PreInitialize(this);Text=L("iTunes: предустановки экспорта","iTunes export presets");Size=new Size(790,590);MinimumSize=new Size(600,440);StartPosition=FormStartPosition.CenterParent;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,RowCount=6};layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   for(int i=0;i<4;i++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   _style.Items.AddRange(new object[]{"","normal","bold","italic"});_region.Items.AddRange(new object[]{"","top","bottom"});_color.Text=color;_style.SelectedIndex=Math.Max(0,_style.Items.IndexOf(style??""));_region.SelectedIndex=Math.Max(0,_region.Items.IndexOf(region??""));_header.Text=header;
   layout.Controls.Add(new Label{Text=L("Цвет текста (имя или #RRGGBB)","Text color (name or #RRGGBB)"),AutoSize=true},0,0);layout.Controls.Add(_color,1,0);
   layout.Controls.Add(new Label{Text=L("Стиль по умолчанию","Default style"),AutoSize=true},0,1);layout.Controls.Add(_style,1,1);
   layout.Controls.Add(new Label{Text=L("Позиция по умолчанию","Default region"),AutoSize=true},0,2);layout.Controls.Add(_region,1,2);
   var hint=new Label{Text=L("XML-шаблон заменяет заголовок исходника при сохранении ITT. Пустое поле сохраняет заголовок документа.","The XML template replaces the source header when saving ITT. Leave it empty to keep the document header."),AutoSize=true,MaximumSize=new Size(720,0)};layout.Controls.Add(hint,0,3);layout.SetColumnSpan(hint,2);layout.Controls.Add(_header,0,4);layout.SetColumnSpan(_header,2);
   var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var load=new Button{Text=L("Загрузить шаблон…","Load template…"),AutoSize=true};var clear=new Button{Text=L("Очистить шаблон","Clear template"),AutoSize=true};var ok=new Button{Text="OK",AutoSize=true};var cancel=new Button{Text=L("Отмена","Cancel"),AutoSize=true,DialogResult=DialogResult.Cancel};bar.Controls.AddRange(new Control[]{load,clear,ok,cancel});layout.Controls.Add(bar,0,5);layout.SetColumnSpan(bar,2);Controls.Add(layout);CancelButton=cancel;
   clear.Click+=(s,e)=>_header.Clear();
   load.Click+=(s,e)=>{using(var file=new OpenFileDialog{Filter="iTunes / TTML|*.itt;*.ttml;*.xml|All files|*.*"})if(file.ShowDialog(this)==DialogResult.OK){try{_header.Text=File.ReadAllText(file.FileName);}catch(IOException ex){MessageBox.Show(this,ex.Message,Text);}catch(UnauthorizedAccessException ex){MessageBox.Show(this,ex.Message,Text);}}};
   ok.Click+=(s,e)=>
   {
    if(!Regex.IsMatch(ColorValue,@"^(?:[A-Za-z]+|#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?)$")){MessageBox.Show(this,L("Укажите цвет, например white или #FFFFFF.","Enter a color such as white or #FFFFFF."),Text);return;}
    if(HeaderValue.Length>0 && ItunesTimedText.LoadHeaderTemplate(HeaderValue)==null){MessageBox.Show(this,L("Нужен корректный TTML XML с элементом head и пространством имён TTML.","Use valid TTML XML with a head element in the TTML namespace."),Text);return;}
    DialogResult=DialogResult.OK;
   };UiUtil.FixFonts(this);
  }
 }
}
