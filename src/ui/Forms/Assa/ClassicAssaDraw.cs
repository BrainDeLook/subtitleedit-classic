using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.ClassicDraw;

namespace Nikse.SubtitleEdit.Forms.Assa
{
    public sealed class ClassicAssaDraw : Form
    {
        private readonly DrawCanvas _canvas=new DrawCanvas {Dock=DockStyle.Fill};
        private readonly ListBox _objects=new ListBox {Dock=DockStyle.Fill,SelectionMode=SelectionMode.MultiExtended};
        private readonly ComboBox _library=new ComboBox {Width=210,DropDownStyle=ComboBoxStyle.DropDownList};
        private readonly PropertyGrid _properties=new PropertyGrid {Dock=DockStyle.Fill,ToolbarVisible=false};
        private readonly Stack<List<DrawShape>> _undo=new Stack<List<DrawShape>>(),_redo=new Stack<List<DrawShape>>();
        private readonly Paragraph _cue;
        private Button _apply;
        private readonly Timer _animationTimer=new Timer{Interval=33};
        private readonly System.Diagnostics.Stopwatch _animationClock=new System.Diagnostics.Stopwatch();
        private readonly string _documentHeader;
        private string _savedDrawing;
        private bool _refresh; private List<DrawShape> _propertySnapshot;
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public List<Paragraph> Result {get;private set;}
        public ClassicAssaDraw(Paragraph cue,int width,int height,Subtitle document=null) : this(cue,width,height,document,new[]{cue}) { }
        public ClassicAssaDraw(Paragraph cue,int width,int height,Subtitle document,IEnumerable<Paragraph> lines)
        {
            _canvas.RenderFonts=document==null?null:ClassicFontCollection.Scan(document,System.Threading.CancellationToken.None);
            _documentHeader=document?.Header;
            _canvas.LineMs=(int)Math.Max(1,cue.Duration.TotalMilliseconds);
            _animationTimer.Tick+=(sender,e)=>{if(_animationClock.ElapsedMilliseconds>=_canvas.LineMs){_animationTimer.Stop();_canvas.AnimationTime=null;}else _canvas.AnimationTime=_animationClock.ElapsedMilliseconds;_canvas.Invalidate();};
            _cue=new Paragraph(cue,false);Text=L("Редактор ASS Draw","ASS Draw editor");Size=new Size(1150,760);MinimumSize=new Size(800,550);StartPosition=FormStartPosition.CenterParent;
            _canvas.ScriptWidth=width;_canvas.ScriptHeight=height;_canvas.Shapes=ClassicDrawDocument.Read(lines);_canvas.Active=_canvas.Shapes.FirstOrDefault();
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=2,Padding=new Padding(8)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,280));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            var tools=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
            Add(tools,L("Указатель","Pointer"),()=>_canvas.Tool=0);Add(tools,L("Линия","Line"),()=>_canvas.Tool=1);Add(tools,L("Безье","Bezier"),()=>_canvas.Tool=2);Add(tools,L("Прямоугольник","Rectangle"),()=>_canvas.Tool=3);Add(tools,L("Эллипс","Ellipse"),()=>_canvas.Tool=4);
            Add(tools,L("Отменить","Undo"),Undo);Add(tools,L("Повторить","Redo"),Redo);Add(tools,L("Удалить","Delete"),()=>Edit(()=>{foreach(var shape in SelectedShapes())_canvas.Shapes.Remove(shape);_canvas.Active=null;}));
            Add(tools,"↻ 15°",()=>Edit(()=>{var shapes=SelectedShapes();var b=DrawShape.GetBounds(shapes);foreach(var shape in shapes)shape.Rotate(15,(b.Left+b.Right)/2,(b.Top+b.Bottom)/2);}));Add(tools,"×1.1",()=>Scale(1.1f));Add(tools,"×0.9",()=>Scale(.9f));Add(tools,"↔",()=>Edit(()=>{var shapes=SelectedShapes();var b=DrawShape.GetBounds(shapes);foreach(var shape in shapes)shape.FlipHorizontal(b.Left,b.Right);}));Add(tools,"↕",()=>Edit(()=>{var shapes=SelectedShapes();var b=DrawShape.GetBounds(shapes);foreach(var shape in shapes)shape.FlipVertical(b.Top,b.Bottom);}));
            Add(tools,L("Цвет…","Color…"),ChooseColor);Add(tools,L("Фон…","Background…"),Background);Add(tools,"SVG…",ImportSvg);Add(tools,L("Сохранить фигуру…","Save shape…"),SaveShape);
            Add(tools,L("Текст", "Text"),()=>Edit(()=>{ var shape = new DrawShape { IsTextBox=true, Text="Text", Layer=NextLayer() }; shape.AddPoint(DrawCoordinateType.Move,100,100,Color.IndianRed); shape.AddPoint(DrawCoordinateType.Line,width-100,100,Color.IndianRed);shape.AddPoint(DrawCoordinateType.Line,width-100,250,Color.IndianRed);shape.AddPoint(DrawCoordinateType.Line,100,250,Color.IndianRed);_canvas.Shapes.Add(shape);_canvas.Active=shape;}));
            Add(tools,L("Вычитание", "Eraser"),()=>Edit(()=>{if(_canvas.Active!=null)_canvas.Active.IsEraser=!_canvas.Active.IsEraser;}));
            Add(tools,L("Цвет текста…", "Text color…"),()=>{if(_canvas.Active==null)return;using(var d=new ColorDialog{Color=_canvas.Active.TextColor})if(d.ShowDialog(this)==DialogResult.OK)Edit(()=>_canvas.Active.TextColor=d.Color);});
            Add(tools,L("Цвет контура…", "Outline color…"),()=>{if(_canvas.Active==null)return;using(var d=new ColorDialog{Color=_canvas.Active.OutlineColor})if(d.ShowDialog(this)==DialogResult.OK)Edit(()=>_canvas.Active.OutlineColor=d.Color);});
            Add(tools,L("Группировать", "Group"),()=>Edit(()=>{var shapes=SelectedShapes();if(shapes.Count<2)return;var id=_canvas.Shapes.Select(x=>x.GroupId).DefaultIfEmpty().Max()+1;foreach(var shape in shapes)shape.GroupId=id;}));
            Add(tools,L("Разгруппировать", "Ungroup"),()=>Edit(()=>{foreach(var shape in SelectedShapes())shape.GroupId=0;}));
            Add(tools,L("Анимация…", "Animation…"),EditAnimation);
            Add(tools,L("Проиграть анимацию", "Play animation"),()=>{_animationClock.Restart();_animationTimer.Start();});
            Add(tools,L("Сохранить рисунок…", "Save drawing…"),SaveDrawing);
            Add(tools,L("Открыть рисунок…", "Open drawing…"),OpenDrawing);
            Add(tools,L("Галерея фигур…","Shape gallery…"),ChooseLibraryShape);LoadLibrary();tools.Controls.Add(_library);Add(tools,L("Добавить фигуру","Add shape"),()=>{if(_library.SelectedItem is ShapeLibraryItem item)Edit(()=>{var shapes=item.CreateShapes(width*.25f,height*.25f,width*.3f,height*.3f,Color.White,NextLayer());var group=_canvas.Shapes.Select(x=>x.GroupId).DefaultIfEmpty().Max()+1;foreach(var shape in shapes){shape.GroupId=group;shape.Animation=!item.AnimatedColor.HasValue || shape.ForeColor==item.AnimatedColor.Value?item.Animation?.Clone():null;}_canvas.Shapes.AddRange(shapes);_canvas.Active=shapes.FirstOrDefault();});});
            root.Controls.Add(tools,0,0);root.SetColumnSpan(tools,2);root.Controls.Add(_canvas,0,1);
            var side=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2};side.RowStyles.Add(new RowStyle(SizeType.Percent,35));side.RowStyles.Add(new RowStyle(SizeType.Percent,65));side.Controls.Add(_objects,0,0);side.Controls.Add(_properties,0,1);root.Controls.Add(side,1,1);
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};var apply=_apply=new Button{Text=L("Применить","Apply"),AutoSize=true};apply.Click+=(s,e)=>{Result=BuildResult();DialogResult=DialogResult.OK;};footer.Controls.Add(cancel);footer.Controls.Add(apply);root.Controls.Add(footer,0,2);root.SetColumnSpan(footer,2);Controls.Add(root);CancelButton=cancel;
            _canvas.BeforeEdit=Remember;_canvas.Changed=RefreshObjects;_objects.SelectedIndexChanged+=(s,e)=>{if(_refresh)return;_canvas.Selected.Clear();foreach(DrawShape shape in _objects.SelectedItems)_canvas.SelectGroup(shape,true);_canvas.Active=_objects.SelectedItem as DrawShape;_canvas.ActivePoint=null;_properties.SelectedObject=_canvas.Active;_propertySnapshot=_canvas.Shapes.Select(s=>s.Clone()).ToList();_canvas.Invalidate();};_properties.PropertyValueChanged+=(s,e)=>{if(_propertySnapshot!=null){_undo.Push(_propertySnapshot);_redo.Clear();}RefreshObjects();};_properties.SelectedGridItemChanged+=(s,e)=>{};
            KeyPreview=true;KeyDown+=(s,e)=>{if(e.Control && e.KeyCode==Keys.Z){Undo();e.SuppressKeyPress=true;}else if(e.Control && e.KeyCode==Keys.Y){Redo();e.SuppressKeyPress=true;}};FormClosing+=(sender,e)=>{if(DialogResult!=DialogResult.OK && DrawingState()!=_savedDrawing)e.Cancel=MessageBox.Show(this,L("Закрыть редактор без применения изменений?","Close without applying drawing changes?"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes;};
            _savedDrawing=DrawingState();FormClosed+=(s,e)=>{_animationTimer.Dispose();_canvas.BackgroundImage?.Dispose();};RefreshObjects();UiUtil.FixFonts(this);
        }
        private static void Add(FlowLayoutPanel parent,string text,Action action){var b=new Button{Text=text,AutoSize=true};b.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(parent.FindForm(),ex.Message);}};parent.Controls.Add(b);}
        private int NextLayer()=>_canvas.Shapes.Count==0?_cue.Layer:_canvas.Shapes.Max(s=>s.Layer)+1;
        private void Remember(){_undo.Push(_canvas.Shapes.Select(s=>s.Clone()).ToList());_redo.Clear();}
        private void Edit(Action action){Remember();action();RefreshObjects();}
        private void Undo(){if(_undo.Count==0)return;_redo.Push(_canvas.Shapes.Select(s=>s.Clone()).ToList());_canvas.Shapes=_undo.Pop();_canvas.Active=null;RefreshObjects();}
        private void Redo(){if(_redo.Count==0)return;_undo.Push(_canvas.Shapes.Select(s=>s.Clone()).ToList());_canvas.Shapes=_redo.Pop();_canvas.Active=null;RefreshObjects();}
        private void RefreshObjects(){if(_apply!=null)_apply.Enabled=_canvas.Shapes.Any(s=>!s.Hidden && !s.IsEraser && s.Points.Count>0);_canvas.Selected.RemoveWhere(s=>!_canvas.Shapes.Contains(s));if(_canvas.Active!=null)_canvas.SelectGroup(_canvas.Active,true);_refresh=true;_objects.BeginUpdate();_objects.Items.Clear();foreach(var shape in _canvas.Shapes){var index=_objects.Items.Add(shape);if(_canvas.Selected.Contains(shape))_objects.SetSelected(index,true);}_objects.EndUpdate();_refresh=false;_properties.SelectedObject=_canvas.Active;_propertySnapshot=_canvas.Shapes.Select(s=>s.Clone()).ToList();_canvas.Invalidate();}
        private List<DrawShape> SelectedShapes()
        {
            var selected=_canvas.Selected.Where(s=>_canvas.Shapes.Contains(s) && !s.Hidden).ToList();
            if(selected.Count==0 && _canvas.Active!=null)selected.Add(_canvas.Active);
            var groups=new HashSet<int>(selected.Where(x=>x.GroupId>0).Select(x=>x.GroupId));
            return _canvas.Shapes.Where(x=>selected.Contains(x) || groups.Contains(x.GroupId)).ToList();
        }
        private void Scale(float factor)
        {
            var shapes=SelectedShapes();if(shapes.Count==0)return;
            Edit(()=>{var b=DrawShape.GetBounds(shapes);foreach(var shape in shapes){foreach(var p in shape.Points){p.X=b.Left+(p.X-b.Left)*factor;p.Y=b.Top+(p.Y-b.Top)*factor;}shape.TransformText((x,y)=>(b.Left+(x-b.Left)*factor,b.Top+(y-b.Top)*factor),false);}});
        }
        private void EditAnimation()
        {
            var selected=SelectedShapes();if(selected.Count==0)return;
            var animation=selected.Select(x=>x.Animation).FirstOrDefault(x=>x!=null)?.Clone() ?? new DrawAnimation();
            using(var dialog=new Form{Text=L("Анимация рисунка", "Drawing animation"),Size=new Size(520,430),MinimumSize=new Size(460,400),StartPosition=FormStartPosition.CenterParent})
            {
                var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,RowCount=8};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
                var effectIn=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DataSource=Enum.GetValues(typeof(DrawAnimationEffect))};effectIn.SelectedItem=animation.In;
                var effectOut=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DataSource=Enum.GetValues(typeof(DrawAnimationEffect))};effectOut.SelectedItem=animation.Out;
                var loop=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DataSource=Enum.GetValues(typeof(DrawLoopEffect))};loop.SelectedItem=animation.Loop;
                var durations=new[]{new NumericUpDown{Minimum=0,Maximum=60000,Value=Math.Max(0,Math.Min(60000,animation.InMs)),Dock=DockStyle.Fill},new NumericUpDown{Minimum=0,Maximum=60000,Value=Math.Max(0,Math.Min(60000,animation.OutMs)),Dock=DockStyle.Fill},new NumericUpDown{Minimum=10,Maximum=60000,Value=Math.Max(10,Math.Min(60000,animation.LoopMs)),Dock=DockStyle.Fill}};
                var labels=new[]{L("Появление", "In effect"),L("Появление, мс", "In duration, ms"),L("Исчезновение", "Out effect"),L("Исчезновение, мс", "Out duration, ms"),L("Цикл", "Loop"),L("Период, мс", "Loop period, ms")};
                var controls=new Control[]{effectIn,durations[0],effectOut,durations[1],loop,durations[2]};
                for(var i=0;i<6;i++){layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.Controls.Add(new Label{Text=labels[i],AutoSize=true,Padding=new Padding(0,6,0,6)},0,i);layout.Controls.Add(controls[i],1,i);}
                var note=new Label{AutoSize=true,MaximumSize=new Size(460,0),Text=L("Анимация применяется ко всем выбранным слоям. Несовместимый с появлением/исчезновением цикл будет отключён.","Animation applies to all selected layers. A loop with an incompatible rotation origin is disabled.")};layout.Controls.Add(note,0,6);layout.SetColumnSpan(note,2);
                var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var ok=new Button{Text=L("Применить", "Apply"),AutoSize=true,DialogResult=DialogResult.OK};var cancel=new Button{Text=L("Отмена", "Cancel"),AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.AddRange(new Control[]{ok,cancel});layout.Controls.Add(buttons,0,7);layout.SetColumnSpan(buttons,2);dialog.Controls.Add(layout);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                animation.In=(DrawAnimationEffect)effectIn.SelectedItem;animation.Out=(DrawAnimationEffect)effectOut.SelectedItem;animation.Loop=(DrawLoopEffect)loop.SelectedItem;animation.InMs=(int)durations[0].Value;animation.OutMs=(int)durations[1].Value;animation.LoopMs=(int)durations[2].Value;
                var layers=new HashSet<int>(selected.Select(x=>x.Layer));Edit(()=>{foreach(var shape in _canvas.Shapes.Where(x=>layers.Contains(x.Layer)))shape.Animation=animation.Clone();});
            }
        }
        private string DrawingState()=>string.Join("\n",ClassicDrawDocument.Write(_canvas.Shapes,_cue,_canvas.ScriptWidth,_canvas.ScriptHeight,true).Select(p=>p.Layer+"|"+p.IsComment+"|"+p.Effect+"|"+p.Text));
        private void SaveDrawing()
        {
            using(var dialog=new SaveFileDialog{Filter="ASS drawing project|*.assadraw",DefaultExt="assadraw"})
                if(dialog.ShowDialog(this)==DialogResult.OK)
                {
                    var sub=new Subtitle{Header=_documentHeader};sub.Paragraphs.AddRange(ClassicDrawDocument.Write(_canvas.Shapes,_cue,_canvas.ScriptWidth,_canvas.ScriptHeight,true));
                    File.WriteAllText(dialog.FileName,new Nikse.SubtitleEdit.Core.SubtitleFormats.AdvancedSubStationAlpha().ToText(sub,"ASS Draw"));_savedDrawing=DrawingState();
                }
        }
        private void OpenDrawing()
        {
            using(var dialog=new OpenFileDialog{Filter="ASS drawing project|*.assadraw;*.ass"})
                if(dialog.ShowDialog(this)==DialogResult.OK)
                {
                    var sub=new Subtitle();new Nikse.SubtitleEdit.Core.SubtitleFormats.AdvancedSubStationAlpha().LoadSubtitle(sub,File.ReadAllLines(dialog.FileName).ToList(),dialog.FileName);
                    var shapes=ClassicDrawDocument.Read(sub.Paragraphs);Edit(()=>{_canvas.Shapes=shapes;_canvas.Active=shapes.FirstOrDefault();});
                }
        }
        private void ChooseColor(){if(_canvas.Active==null)return;using(var dialog=new ColorDialog{Color=_canvas.Active.ForeColor})if(dialog.ShowDialog(this)==DialogResult.OK)Edit(()=>_canvas.Active.ForeColor=dialog.Color);}
        private void Background(){using(var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK){using(var image=Image.FromFile(dialog.FileName)){_canvas.BackgroundImage?.Dispose();_canvas.BackgroundImage=new Bitmap(image);}_canvas.Invalidate();}}
        private void ImportSvg(){using(var dialog=new OpenFileDialog{Filter="SVG|*.svg"})if(dialog.ShowDialog(this)==DialogResult.OK){var result=SvgImporter.Import(File.ReadAllText(dialog.FileName),_canvas.ScriptWidth,_canvas.ScriptHeight,NextLayer());Edit(()=>_canvas.Shapes.AddRange(result.Shapes));}}
        private void ChooseLibraryShape()
        {
            using(var gallery=new Form{Text=L("Галерея фигур","Shape gallery"),Size=new Size(850,630),MinimumSize=new Size(500,400),StartPosition=FormStartPosition.CenterParent})
            {
                var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};gallery.Controls.Add(panel);
                foreach(var item in _library.Items.Cast<ShapeLibraryItem>())
                {
                    var card=new Panel{Size=new Size(180,135),Margin=new Padding(7),BorderStyle=BorderStyle.FixedSingle};
                    var preview=new DrawCanvas{Dock=DockStyle.Fill,ScriptWidth=320,ScriptHeight=180,Shapes=item.CreateShapes(20,15,280,145,Color.White,0)};
                    var name=new Button{Text=item.Name,Dock=DockStyle.Bottom,Height=34,AutoEllipsis=true};
                    Action choose=()=>{_library.SelectedItem=item;gallery.DialogResult=DialogResult.OK;};name.Click+=(sender,e)=>choose();preview.DoubleClick+=(sender,e)=>choose();card.Controls.Add(preview);card.Controls.Add(name);panel.Controls.Add(card);
                }
                gallery.ShowDialog(this);
            }
        }
        private void LoadLibrary(){_library.Items.Clear();foreach(var item in ShapeLibrary.BuiltIn.Concat(ShapeLibrary.GetUserShapes()))_library.Items.Add(item);_library.DisplayMember="Name";if(_library.Items.Count>0)_library.SelectedIndex=0;}
        private void SaveShape()
        {
            if(_canvas.Active==null)return;
            using(var dialog=new Form {Text=L("Имя фигуры в библиотеке", "Library shape name"),Size=new Size(420,150),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false})
            {
                var name=new TextBox {Dock=DockStyle.Top,Text=L("Моя фигура", "My shape")};
                var save=new Button {Text=L("Сохранить", "Save"),Dock=DockStyle.Bottom,DialogResult=DialogResult.OK};dialog.Controls.Add(name);dialog.Controls.Add(save);dialog.AcceptButton=save;
                if(dialog.ShowDialog(this)==DialogResult.OK && !string.IsNullOrWhiteSpace(name.Text)){ShapeLibrary.Save(name.Text.Trim(),new[]{_canvas.Active});LoadLibrary();}
            }
        }
        public List<Paragraph> BuildResult()
        {
            using(var fonts=Nikse.SubtitleEdit.Logic.ClassicImageExport.FontFaces.UseFonts(_canvas.RenderFonts))
                return ClassicDrawDocument.Write(_canvas.Shapes,_cue,_canvas.ScriptWidth,_canvas.ScriptHeight);
        }
        private sealed class DrawCanvas : Control
        {
            public List<ClassicFontFile> RenderFonts;
            public double? AnimationTime; public int LineMs;
            public List<DrawShape> Shapes=new List<DrawShape>();public DrawShape Active;public DrawCoordinate ActivePoint;public readonly HashSet<DrawShape> Selected=new HashSet<DrawShape>();public int ScriptWidth=1280,ScriptHeight=720,Tool;public Action BeforeEdit,Changed;private PointF _start,_last;private DrawCoordinate _point;private bool _drag,_rubberBand,_edited;private HashSet<DrawShape> _selectionBeforeBand;private float ScaleFactor=>Math.Max(.01f,Math.Min(Width/(float)ScriptWidth,Height/(float)ScriptHeight));
            public DrawCanvas(){DoubleBuffered=true;BackColor=Color.FromArgb(35,35,35);TabStop=true;}
            public void SelectGroup(DrawShape shape,bool select)
            {
                foreach(var member in Shapes.Where(s=>!s.Hidden && (s==shape || shape.GroupId>0 && s.GroupId==shape.GroupId)))
                    if(select)Selected.Add(member);else Selected.Remove(member);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(ScaleFactor,ScaleFactor);
                if(BackgroundImage!=null)g.DrawImage(BackgroundImage,0,0,ScriptWidth,ScriptHeight);using(var pen=new Pen(Color.DimGray,1/ScaleFactor)){for(int x=0;x<ScriptWidth;x+=100)g.DrawLine(pen,x,0,x,ScriptHeight);for(int y=0;y<ScriptHeight;y+=100)g.DrawLine(pen,0,y,ScriptWidth,y);}
                foreach(var shape in Shapes.OrderBy(s=>s.Layer).Where(s=>!s.Hidden))
                {
                    var animationState=g.Save();var opacity=1f;
                    if(AnimationTime.HasValue && shape.Animation!=null)
                    {
                        var members=shape.GroupId>0?Shapes.Where(x=>x.GroupId==shape.GroupId):Shapes.Where(x=>x.Layer==shape.Layer);
                        var bounds=DrawShape.GetBounds(members);var state=shape.Animation.GetState(AnimationTime.Value,LineMs,bounds,ScriptWidth,ScriptHeight);
                        opacity=state.Opacity;
                        if(state.Opacity<.02f){g.Restore(animationState);continue;}
                        var x=(bounds.Left+bounds.Right)/2;var y=(bounds.Top+bounds.Bottom)/2;
                        g.TranslateTransform(x+state.Dx,y+state.Dy);g.RotateTransform(state.Rotation);g.ScaleTransform(state.Scale,state.Scale);g.TranslateTransform(-x,-y);
                    }
                    using(var path=PathFor(shape))using(var brush=new SolidBrush(Color.FromArgb((int)(shape.ForeColor.A*opacity),shape.ForeColor)))using(var outline=new Pen(Selected.Contains(shape)||shape==Active?Color.Gold:(Color)shape.OutlineColor,Math.Max(1/ScaleFactor,shape.OutlineWidth))){if(!shape.IsTextBox){if(shape.Points.Count>2 && !shape.IsEraser)g.FillPath(brush,path);if(shape.IsEraser)outline.DashStyle=DashStyle.Dash;g.DrawPath(outline,path);}}
                    if(shape.HasText)
                    {
                        var b=shape.GetTextBounds();var frame=shape.GetTextFrame();var layout=AssaTextLayout.Layout(shape.Text,shape.FontName,shape.FontSize,shape.FontBold,b.Right-b.Left,b.Bottom-b.Top,shape.ShrinkToFit);
                        var state=g.Save();g.TranslateTransform(frame.CenterX,frame.CenterY);g.RotateTransform(frame.Angle);
                        using(var font=new Font(shape.FontName,layout.EmSize,shape.FontBold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(Color.FromArgb((int)(shape.TextColor.A*opacity),shape.TextColor)))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap})
                            g.DrawString(string.Join(Environment.NewLine,layout.Lines),font,brush,new RectangleF(-frame.Width/2,-frame.Height/2,frame.Width,frame.Height),format);
                        g.Restore(state);
                    }
                    if(shape==Active)foreach(var p in shape.Points)using(var brush=new SolidBrush(p.PointColor))g.FillEllipse(brush,p.X-3/ScaleFactor,p.Y-3/ScaleFactor,6/ScaleFactor,6/ScaleFactor);
                    g.Restore(animationState);
                }
                if(_rubberBand){var rect=RectangleF.FromLTRB(Math.Min(_start.X,_last.X),Math.Min(_start.Y,_last.Y),Math.Max(_start.X,_last.X),Math.Max(_start.Y,_last.Y));using(var brush=new SolidBrush(Color.FromArgb(35,Color.DodgerBlue)))using(var pen=new Pen(Color.DodgerBlue,1/ScaleFactor){DashStyle=DashStyle.Dash}){g.FillRectangle(brush,rect);g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);}}
            }
            private static GraphicsPath PathFor(DrawShape shape)
            {
                var path=new GraphicsPath(FillMode.Winding);if(shape.Points.Count==0)return path;var previous=shape.Points[0];
                for(int i=1;i<shape.Points.Count;i++){var p=shape.Points[i];if(p.DrawType==DrawCoordinateType.Move){path.StartFigure();previous=p;continue;}if(p.DrawType==DrawCoordinateType.BezierCurveSupport1 && i+2<shape.Points.Count){var p2=shape.Points[i+1];var end=shape.Points[i+2];path.AddBezier(previous.X,previous.Y,p.X,p.Y,p2.X,p2.Y,end.X,end.Y);previous=end;i+=2;}else{path.AddLine(previous.X,previous.Y,p.X,p.Y);previous=p;}}path.CloseFigure();return path;
            }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();var x=e.X/ScaleFactor;var y=e.Y/ScaleFactor;_start=_last=new PointF(x,y);_edited=false;
                if(Tool==0)
                {
                    var hit=Shapes.OrderBy(s=>s.Layer).LastOrDefault(s=>!s.Hidden && s.HitTest(x,y,10/ScaleFactor));
                    var toggle=(ModifierKeys&Keys.Control)!=0;
                    ActivePoint=null;
                    if(hit==null){_selectionBeforeBand=toggle?new HashSet<DrawShape>(Selected):new HashSet<DrawShape>();Selected.Clear();Selected.UnionWith(_selectionBeforeBand);Active=null;_rubberBand=true;}
                    else
                    {
                        if(toggle){var select=!Selected.Contains(hit);SelectGroup(hit,select);Active=select?hit:Selected.LastOrDefault();}
                        else {if(!Selected.Contains(hit))Selected.Clear();SelectGroup(hit,true);Active=hit;}
                        _point=toggle || Selected.Count>1 || hit.GroupId>0?null:hit.Points.FirstOrDefault(p=>Math.Abs(p.X-x)<8/ScaleFactor && Math.Abs(p.Y-y)<8/ScaleFactor);
                        ActivePoint=_point;
                    }
                }
                else if(Tool<3){BeforeEdit?.Invoke();_edited=true;if(Active==null){Active=new DrawShape();Shapes.Add(Active);}if(Active.Points.Count==0)Active.AddPoint(DrawCoordinateType.Move,x,y,Color.IndianRed);else if(Tool==2){var last=Active.Points.Last();Active.AddPoint(DrawCoordinateType.BezierCurveSupport1,last.X+(x-last.X)/3,last.Y,Color.LightGreen);Active.AddPoint(DrawCoordinateType.BezierCurveSupport2,last.X+2*(x-last.X)/3,y,Color.LightGreen);Active.AddPoint(DrawCoordinateType.BezierCurve,x,y,Color.IndianRed);}else Active.AddPoint(DrawCoordinateType.Line,x,y,Color.IndianRed);Selected.Clear();Selected.Add(Active);}
                else {BeforeEdit?.Invoke();_edited=true;Active=new DrawShape();Shapes.Add(Active);Selected.Clear();Selected.Add(Active);} _drag=true;Capture=true;Changed?.Invoke();
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);if(!_drag)return;var x=e.X/ScaleFactor;var y=e.Y/ScaleFactor;_last=new PointF(x,y);
                if(_rubberBand)
                {
                    Selected.Clear();Selected.UnionWith(_selectionBeforeBand);
                    var rect=RectangleF.FromLTRB(Math.Min(_start.X,x),Math.Min(_start.Y,y),Math.Max(_start.X,x),Math.Max(_start.Y,y));
                    foreach(var shape in Shapes.Where(s=>!s.Hidden)){var b=shape.GetBounds();if(rect.IntersectsWith(RectangleF.FromLTRB(b.Left,b.Top,b.Right,b.Bottom)))SelectGroup(shape,true);}
                    Active=Selected.FirstOrDefault();Invalidate();return;
                }
                if(Active==null || x==_start.X && y==_start.Y)return;
                if(!_edited){BeforeEdit?.Invoke();_edited=true;}
                if(Tool==0){if(_point!=null){_point.X=x;_point.Y=y;}else{foreach(var shape in Selected)shape.Offset(x-_start.X,y-_start.Y);_start=new PointF(x,y);}}
                else if(Tool==3){Active.Points.Clear();Active.AddPoint(DrawCoordinateType.Move,_start.X,_start.Y,Color.IndianRed);Active.AddPoint(DrawCoordinateType.Line,x,_start.Y,Color.IndianRed);Active.AddPoint(DrawCoordinateType.Line,x,y,Color.IndianRed);Active.AddPoint(DrawCoordinateType.Line,_start.X,y,Color.IndianRed);}
                else if(Tool==4){var oval=CircleBezier.MakeCircle((_start.X+x)/2,(_start.Y+y)/2,Math.Abs(x-_start.X)/2,Active.Layer,Active.ForeColor);var radius=Math.Max(.001f,Math.Abs(x-_start.X));foreach(var p in oval.Points)p.Y=(_start.Y+y)/2+(p.Y-(_start.Y+y)/2)*Math.Abs(y-_start.Y)/radius;Active.Points=oval.Points;foreach(var p in Active.Points)p.DrawShape=Active;}Invalidate();
            }
            protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button!=MouseButtons.Left)return;_drag=false;_rubberBand=false;Capture=false;_point=null;Changed?.Invoke();}
            protected override bool IsInputKey(Keys keyData)=> (keyData&Keys.KeyCode)==Keys.Left || (keyData&Keys.KeyCode)==Keys.Right || (keyData&Keys.KeyCode)==Keys.Up || (keyData&Keys.KeyCode)==Keys.Down || base.IsInputKey(keyData);
            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if(e.Control && e.KeyCode==Keys.A){Selected.Clear();Selected.UnionWith(Shapes.Where(s=>!s.Hidden));Active=Selected.FirstOrDefault();ActivePoint=null;Changed?.Invoke();e.SuppressKeyPress=true;return;}
                if(e.KeyCode==Keys.Delete)
                {
                    if(ActivePoint!=null && Active!=null)
                    {if(ActivePoint.DrawType==DrawCoordinateType.BezierCurveSupport1 || ActivePoint.DrawType==DrawCoordinateType.BezierCurveSupport2){e.SuppressKeyPress=true;return;}BeforeEdit?.Invoke();Active.RemovePoint(ActivePoint);ActivePoint=null;if(Active.Points.Count==0){Shapes.Remove(Active);Selected.Remove(Active);Active=Selected.FirstOrDefault();}}
                    else if(Selected.Count>0){BeforeEdit?.Invoke();Shapes.RemoveAll(s=>Selected.Contains(s));Selected.Clear();Active=null;ActivePoint=null;}
                    Changed?.Invoke();e.SuppressKeyPress=true;return;
                }
                if(e.KeyCode==Keys.G && e.Control && Selected.Count>0)
                {BeforeEdit?.Invoke();var group=e.Shift?0:Shapes.Select(s=>s.GroupId).DefaultIfEmpty().Max()+1;foreach(var shape in Selected)shape.GroupId=group;Changed?.Invoke();e.SuppressKeyPress=true;return;}
                if(e.KeyCode!=Keys.Left && e.KeyCode!=Keys.Right && e.KeyCode!=Keys.Up && e.KeyCode!=Keys.Down)return;
                if(Selected.Count==0 || e.Modifiers!=Keys.None && e.Modifiers!=Keys.Shift && e.Modifiers!=Keys.Control && e.Modifiers!=Keys.Alt)return;
                BeforeEdit?.Invoke();var step=e.Shift||e.Control?10f:1f;var dx=e.KeyCode==Keys.Left?-step:e.KeyCode==Keys.Right?step:0;var dy=e.KeyCode==Keys.Up?-step:e.KeyCode==Keys.Down?step:0;
                if(ActivePoint!=null){ActivePoint.X+=dx;ActivePoint.Y+=dy;}else foreach(var shape in Selected)shape.Offset(dx,dy);
                Changed?.Invoke();e.SuppressKeyPress=true;
            }
        }
    }
}

