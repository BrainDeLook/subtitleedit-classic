using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Statistics
    {
        private ClassicStatisticsData _dashboardData;
        private TabControl _statisticsTabs;
        private Panel _dashboardPanel;
        private Control _totalsCard, _rangesCard, _checksCard, _histogramCard;
        private bool _fittingDashboard;
        private DataGridView _rangeGrid, _checkGrid;
        private string _dashboardReport;
        private static string StatL(string ru,string en)=>ClassicTextTools.L(ru,en);
        private void InitializeClassicStatistics()
        {
            _dashboardData=ClassicStatisticsData.Calculate(_subtitle);_dashboardReport=_dashboardData.Report();
            MinimumSize=new Size(800,520);ClientSize=new Size(1060,780);
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=1,RowCount=2};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _statisticsTabs=new TabControl {Dock=DockStyle.Fill,Name="classicStatisticsTabs"};var dashboardPage=new TabPage(StatL("Обзор", "Dashboard"));var detailsPage=new TabPage(StatL("Подробности", "Details"));var frequentPage=new TabPage(StatL("Частые слова и реплики", "Most used words and lines"));_statisticsTabs.TabPages.AddRange(new[]{dashboardPage,detailsPage,frequentPage});layout.Controls.Add(_statisticsTabs,0,0);
            _dashboardPanel=new Panel {Dock=DockStyle.Fill,AutoScroll=true,Name="classicStatisticsDashboard"};dashboardPage.Controls.Add(_dashboardPanel);
            _totalsCard=BuildTotals();_rangesCard=BuildRanges();_checksCard=BuildChecks();_histogramCard=new GroupBox {Text=StatL("Символов в секунду: распределение", "Characters per second: distribution")};_histogramCard.Controls.Add(new ClassicStatisticsHistogram(_dashboardData) {Dock=DockStyle.Fill});_dashboardPanel.Controls.AddRange(new[]{_totalsCard,_rangesCard,_checksCard,_histogramCard});
            textBoxGeneral.Parent=detailsPage;textBoxGeneral.Dock=DockStyle.Fill;textBoxGeneral.Text=_dashboardReport+Environment.NewLine+_general;textBoxGeneral.ReadOnly=true;
            var frequent=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(6)};frequent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));frequent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            frequent.Controls.Add(FrequencyPanel(_l.MostUsedWords,textBoxMostUsedWords),0,0);frequent.Controls.Add(FrequencyPanel(_l.MostUsedLines,textBoxMostUsedLines),1,0);frequentPage.Controls.Add(frequent);
            Controls.Remove(groupBoxGeneral);Controls.Remove(groupBoxMostUsed);groupBoxGeneral.Dispose();groupBoxMostUsed.Dispose();
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,8,0,0)};buttonOK.Anchor=AnchorStyles.None;buttonExport.Anchor=AnchorStyles.None;buttonOK.AutoSize=true;buttonExport.AutoSize=true;footer.Controls.Add(buttonOK);footer.Controls.Add(buttonExport);layout.Controls.Add(footer,0,1);Controls.Add(layout);layout.BringToFront();
            _dashboardPanel.Resize+=(s,e)=>FitDashboard();_statisticsTabs.SelectedIndexChanged+=(s,e)=>FitDashboard();Shown+=(s,e)=>FitDashboard();FitDashboard();
        }
        private Control BuildTotals()
        {
            var panel=new TableLayoutPanel {ColumnCount=4,RowCount=1};for(int i=0;i<4;i++)panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            var names=new[]{StatL("Реплики","Subtitles"),StatL("Слова","Words"),StatL("Символы","Characters"),StatL("Суммарная длительность","Total cue duration")};var values=new[]{_dashboardData.SubtitleCount.ToString("N0"),_dashboardData.Words.ToString("N0"),_dashboardData.Characters.ToString("N0"),_dashboardData.DurationText};
            for(int i=0;i<4;i++){var group=new GroupBox {Dock=DockStyle.Fill,Text=names[i]};group.Controls.Add(new Label {Dock=DockStyle.Fill,Text=values[i],TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true,AccessibleName=names[i]+": "+values[i]});panel.Controls.Add(group,i,0);}return panel;
        }
        private Control BuildRanges()
        {
            var box=new GroupBox {Text=StatL("Длительность и темп", "Timing and pacing")};_rangeGrid=StatGrid();foreach(var h in new[]{StatL("Показатель","Metric"),StatL("Мин.","Min"),StatL("Сред.","Avg"),StatL("Макс.","Max"),StatL("Шкала среднего","Average meter")})_rangeGrid.Columns.Add("c"+_rangeGrid.Columns.Count,h);
            _rangeGrid.Columns[0].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;_rangeGrid.Columns[0].MinimumWidth=130;for(int i=1;i<4;i++)_rangeGrid.Columns[i].Width=65;_rangeGrid.Columns[4].Width=95;
            foreach(var range in _dashboardData.Ranges){int i=_rangeGrid.Rows.Add(range.Label,range.Minimum.ToString("0.##"),range.Average.ToString("0.##"),range.Maximum.ToString("0.##"),"");_rangeGrid.Rows[i].Tag=range;}
            _rangeGrid.CellPainting+=(s,e)=> {if(e.RowIndex<0 || e.ColumnIndex!=4 || !(_rangeGrid.Rows[e.RowIndex].Tag is ClassicStatRange range))return;e.Paint(e.ClipBounds,DataGridViewPaintParts.Background|DataGridViewPaintParts.Border);var rect=new Rectangle(e.CellBounds.Left+5,e.CellBounds.Top+e.CellBounds.Height/2-3,Math.Max(1,e.CellBounds.Width-10),6);using(var back=new SolidBrush(SystemColors.ControlDark))e.Graphics.FillRectangle(back,rect);rect.Width=(int)(rect.Width*range.AverageFraction);using(var brush=new SolidBrush(Color.SteelBlue))e.Graphics.FillRectangle(brush,rect);e.Handled=true;};
            box.Controls.Add(_rangeGrid);return box;
        }
        private Control BuildChecks()
        {
            var box=new GroupBox {Text=StatL("Проверки действующих ограничений", "Checks against current limits")};_checkGrid=StatGrid();_checkGrid.Columns.Add("label",StatL("Проверка", "Check"));_checkGrid.Columns.Add("count",StatL("Количество", "Count"));_checkGrid.Columns.Add("percent","%");_checkGrid.Columns[0].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;_checkGrid.Columns[0].MinimumWidth=220;_checkGrid.Columns[1].Width=85;_checkGrid.Columns[2].Width=65;
            foreach(var check in _dashboardData.Checks){int i=_checkGrid.Rows.Add(check.Label,check.Count+" / "+check.Total,check.Percent.ToString("0.#"));_checkGrid.Rows[i].Cells[1].Style.ForeColor=check.Count==0?Color.DarkGreen:check.Critical?Color.Firebrick:Color.SaddleBrown;}
            box.Controls.Add(_checkGrid);return box;
        }
        private static DataGridView StatGrid()=>new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=SystemColors.Window,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize};
        private Control FrequencyPanel(string title,Control text)
        {
            var group=new GroupBox {Dock=DockStyle.Fill,Text=title};var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(5)};panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));var copy=new Button {AutoSize=true,Text=StatL("Копировать в буфер", "Copy to clipboard"),AccessibleName=StatL("Копировать: ","Copy: ")+title};copy.Click+=(s,e)=> {try{if(!string.IsNullOrEmpty(text.Text))Clipboard.SetText(text.Text.Trim());}catch(ExternalException ex){MessageBox.Show(this,ex.Message,Text);}};panel.Controls.Add(copy,0,0);text.Parent=panel;text.Dock=DockStyle.Fill;panel.Controls.Add(text,0,1);group.Controls.Add(panel);return group;
        }
        private void FitDashboard()
        {
            if(_fittingDashboard || _dashboardPanel==null)return;_fittingDashboard=true;
            try
            {
                var scroll=-_dashboardPanel.AutoScrollPosition.Y;_dashboardPanel.AutoScroll=false;var width=Math.Max(300,_dashboardPanel.Width-SystemInformation.VerticalScrollBarWidth-24);int x=10,y=8;_totalsCard.SetBounds(x,y,width,80);y+=90;
                var rangesHeight=Math.Max(180,_rangeGrid.RowTemplate.Height*(_dashboardData.Ranges.Count+1)+65);var checksHeight=Math.Max(210,_checkGrid.RowTemplate.Height*(_dashboardData.Checks.Count+1)+65);
                if(width>=960) {int left=(width-10)/2;var height=Math.Max(rangesHeight,checksHeight);_rangesCard.SetBounds(x,y,left,height);_checksCard.SetBounds(x+left+10,y,width-left-10,height);y+=height+10;}
                else {_rangesCard.SetBounds(x,y,width,rangesHeight);y+=rangesHeight+10;_checksCard.SetBounds(x,y,width,checksHeight);y+=checksHeight+10;}
                _histogramCard.SetBounds(x,y,width,215);y+=225;_dashboardPanel.AutoScrollMinSize=new Size(0,y);_dashboardPanel.AutoScroll=true;_dashboardPanel.AutoScrollPosition=new Point(0,scroll);
            }
            finally {_fittingDashboard=false;}
        }
        public string BuildFullStatisticsReport()=>_dashboardReport+Environment.NewLine+string.Format(WriteFormat,_general,_mostUsedWords,_mostUsedLines);
        public void SaveStatisticsReport(string path)=>File.WriteAllText(path,BuildFullStatisticsReport(),new UTF8Encoding(true));
    }
    public sealed class ClassicStatisticsHistogram : Control
    {
        private readonly ClassicStatisticsData _data;
        public ClassicStatisticsHistogram(ClassicStatisticsData data) {_data=data;DoubleBuffered=true;AccessibleName=ClassicTextTools.L("Распределение скорости чтения. Значения доступны на вкладке Подробности.", "Reading-speed distribution. Values are available on the Details tab.");}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var area=new Rectangle(42,25,Math.Max(1,Width-55),Math.Max(1,Height-70));var max=Math.Max(1,_data.CpsBins.Max());float step=area.Width/15f;
            using(var axis=new Pen(SystemColors.GrayText)){g.DrawLine(axis,area.Left,area.Bottom,area.Right,area.Bottom);g.DrawLine(axis,area.Left,area.Top,area.Left,area.Bottom);}
            TextRenderer.DrawText(g,max.ToString(),Font,new Point(3,area.Top),ForeColor);TextRenderer.DrawText(g,"0",Font,new Point(20,area.Bottom-Font.Height/2),ForeColor);
            for(int i=0;i<15;i++)
            {
                float h=(float)_data.CpsBins[i]/max*area.Height;var color=i*2>=_data.MaximumCps?Color.IndianRed:i*2>=_data.OptimalCps?Color.Goldenrod:Color.SteelBlue;using(var brush=new SolidBrush(color))g.FillRectangle(brush,area.Left+i*step+2,area.Bottom-h,Math.Max(1,step-4),h);
                TextRenderer.DrawText(g,i==14?"28+":(i*2).ToString(),Font,new Rectangle((int)(area.Left+i*step),area.Bottom+3,(int)step,Font.Height+2),ForeColor,TextFormatFlags.HorizontalCenter);
                if(_data.CpsBins[i]>0)TextRenderer.DrawText(g,_data.CpsBins[i].ToString(),Font,new Rectangle((int)(area.Left+i*step),(int)(area.Bottom-h-Font.Height),(int)step,Font.Height+2),ForeColor,TextFormatFlags.HorizontalCenter);
            }
            Action<double,Color> marker=(value,color)=>{float xx=area.Left+(float)Math.Max(0,Math.Min(30,value))/30*area.Width;using(var pen=new Pen(color,2){DashStyle=DashStyle.Dash})g.DrawLine(pen,xx,area.Top,xx,area.Bottom);};marker(_data.OptimalCps,Color.DarkGoldenrod);marker(_data.MaximumCps,Color.Firebrick);
            var legend=ClassicTextTools.L("Оптимум: ","Optimal: ")+_data.OptimalCps+"   "+ClassicTextTools.L("Максимум: ","Maximum: ")+_data.MaximumCps+"   "+ClassicTextTools.L("Реплик с CPS: ","CPS samples: ")+_data.CpsSamples;TextRenderer.DrawText(g,legend,Font,new Rectangle(42,Height-Font.Height-4,Width-50,Font.Height+2),ForeColor,TextFormatFlags.EndEllipsis);
        }
    }
}
