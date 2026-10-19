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
        private readonly ListBox _objects=new ListBox {Dock=DockStyle.Fill};
        private readonly ComboBox _library=new ComboBox {Width=210,DropDownStyle=ComboBoxStyle.DropDownList};
        private readonly PropertyGrid _properties=new PropertyGrid {Dock=DockStyle.Fill,ToolbarVisible=false};
        private readonly Stack<List<DrawShape>> _undo=new Stack<List<DrawShape>>(),_redo=new Stack<List<DrawShape>>();
        private readonly Paragraph _cue;
        private Button _apply;
        private bool _refresh; private List<DrawShape> _propertySnapshot;
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public List<Paragraph> Result {get;private set;}
        public ClassicAssaDraw(Paragraph cue,int width,int height,Subtitle document=null)
        {
            _canvas.RenderFonts=document==null?null:ClassicFontCollection.Scan(document,System.Threading.CancellationToken.None);
            _cue=new Paragraph(cue,false);Text=L("Редактор ASS Draw","ASS Draw editor");Size=new Size(1150,760);MinimumSize=new Size(800,550);StartPosition=FormStartPosition.CenterParent;
            _canvas.ScriptWidth=width;_canvas.ScriptHeight=height;_canvas.Shapes=DrawShape.ParseAssa(cue.Text,cue.Layer,Color.White,false);
            if(AssaDrawTextLine.TryParse(cue.Text,out var parsedText))
            {
                var textShape=new DrawShape {IsTextBox=true,Text=parsedText.Text,FontName=parsedText.FontName,FontSize=parsedText.FontSize,FontBold=parsedText.Bold,ShrinkToFit=parsedText.ShrinkToFit,TextColor=parsedText.Color??DrawColors.Black,OutlineWidth=parsedText.OutlineWidth,OutlineColor=parsedText.OutlineColor,Layer=cue.Layer};
                var box=parsedText.Box;textShape.AddPoint(DrawCoordinateType.Move,box.Left,box.Top,Color.IndianRed);textShape.AddPoint(DrawCoordinateType.Line,box.Right,box.Top,Color.IndianRed);textShape.AddPoint(DrawCoordinateType.Line,box.Right,box.Bottom,Color.IndianRed);textShape.AddPoint(DrawCoordinateType.Line,box.Left,box.Bottom,Color.IndianRed);textShape.TextFrame=new DrawTextFrame((box.Left+box.Right)/2,(box.Top+box.Bottom)/2,box.Right-box.Left,box.Bottom-box.Top,parsedText.Angle);_canvas.Shapes.Add(textShape);
            }
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=2,Padding=new Padding(8)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,280));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            var tools=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
            Add(tools,L("Указатель","Pointer"),()=>_canvas.Tool=0);Add(tools,L("Линия","Line"),()=>_canvas.Tool=1);Add(tools,L("Безье","Bezier"),()=>_canvas.Tool=2);Add(tools,L("Прямоугольник","Rectangle"),()=>_canvas.Tool=3);Add(tools,L("Эллипс","Ellipse"),()=>_canvas.Tool=4);
            Add(tools,L("Отменить","Undo"),Undo);Add(tools,L("Повторить","Redo"),Redo);Add(tools,L("Удалить","Delete"),()=>Edit(()=>{if(_canvas.Active!=null)_canvas.Shapes.Remove(_canvas.Active);_canvas.Active=null;}));
            Add(tools,"↻ 15°",()=>Edit(()=>_canvas.Active?.Rotate(15)));Add(tools,"×1.1",()=>Scale(1.1f));Add(tools,"×0.9",()=>Scale(.9f));Add(tools,"↔",()=>Edit(()=>_canvas.Active?.FlipHorizontal()));Add(tools,"↕",()=>Edit(()=>_canvas.Active?.FlipVertical()));
            Add(tools,L("Цвет…","Color…"),ChooseColor);Add(tools,L("Фон…","Background…"),Background);Add(tools,"SVG…",ImportSvg);Add(tools,L("Сохранить фигуру…","Save shape…"),SaveShape);
            Add(tools,L("Текст", "Text"),()=>Edit(()=>{ var shape = new DrawShape { IsTextBox=true, Text="Text", Layer=NextLayer() }; shape.AddPoint(DrawCoordinateType.Move,100,100,Color.IndianRed); shape.AddPoint(DrawCoordinateType.Line,width-100,100,Color.IndianRed);shape.AddPoint(DrawCoordinateType.Line,width-100,250,Color.IndianRed);shape.AddPoint(DrawCoordinateType.Line,100,250,Color.IndianRed);_canvas.Shapes.Add(shape);_canvas.Active=shape;}));
            Add(tools,L("Вычитание", "Eraser"),()=>Edit(()=>{if(_canvas.Active!=null)_canvas.Active.IsEraser=!_canvas.Active.IsEraser;}));
            Add(tools,L("Цвет текста…", "Text color…"),()=>{if(_canvas.Active==null)return;using(var d=new ColorDialog{Color=_canvas.Active.TextColor})if(d.ShowDialog(this)==DialogResult.OK)Edit(()=>_canvas.Active.TextColor=d.Color);});
            Add(tools,L("Цвет контура…", "Outline color…"),()=>{if(_canvas.Active==null)return;using(var d=new ColorDialog{Color=_canvas.Active.OutlineColor})if(d.ShowDialog(this)==DialogResult.OK)Edit(()=>_canvas.Active.OutlineColor=d.Color);});
            LoadLibrary();tools.Controls.Add(_library);Add(tools,L("Добавить фигуру","Add shape"),()=>{if(_library.SelectedItem is ShapeLibraryItem item)Edit(()=>_canvas.Shapes.AddRange(item.CreateShapes(width*.25f,height*.25f,width*.3f,height*.3f,Color.White,NextLayer())));});
            root.Controls.Add(tools,0,0);root.SetColumnSpan(tools,2);root.Controls.Add(_canvas,0,1);
            var side=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2};side.RowStyles.Add(new RowStyle(SizeType.Percent,35));side.RowStyles.Add(new RowStyle(SizeType.Percent,65));side.Controls.Add(_objects,0,0);side.Controls.Add(_properties,0,1);root.Controls.Add(side,1,1);
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};var apply=_apply=new Button{Text=L("Применить","Apply"),AutoSize=true};apply.Click+=(s,e)=>{Result=BuildResult();DialogResult=DialogResult.OK;};footer.Controls.Add(cancel);footer.Controls.Add(apply);root.Controls.Add(footer,0,2);root.SetColumnSpan(footer,2);Controls.Add(root);CancelButton=cancel;
            _canvas.BeforeEdit=Remember;_canvas.Changed=RefreshObjects;_objects.SelectedIndexChanged+=(s,e)=>{if(_refresh)return;_canvas.Active=_objects.SelectedItem as DrawShape;_properties.SelectedObject=_canvas.Active;_propertySnapshot=_canvas.Shapes.Select(s=>s.Clone()).ToList();_canvas.Invalidate();};_properties.PropertyValueChanged+=(s,e)=>{if(_propertySnapshot!=null){_undo.Push(_propertySnapshot);_redo.Clear();}RefreshObjects();};_properties.SelectedGridItemChanged+=(s,e)=>{};
            KeyPreview=true;KeyDown+=(s,e)=>{if(e.Control && e.KeyCode==Keys.Z){Undo();e.SuppressKeyPress=true;}else if(e.Control && e.KeyCode==Keys.Y){Redo();e.SuppressKeyPress=true;}};FormClosed+=(s,e)=>_canvas.BackgroundImage?.Dispose();RefreshObjects();UiUtil.FixFonts(this);
        }
        private static void Add(FlowLayoutPanel parent,string text,Action action){var b=new Button{Text=text,AutoSize=true};b.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(parent.FindForm(),ex.Message);}};parent.Controls.Add(b);}
        private int NextLayer()=>_canvas.Shapes.Count==0?_cue.Layer:_canvas.Shapes.Max(s=>s.Layer)+1;
        private void Remember(){_undo.Push(_canvas.Shapes.Select(s=>s.Clone()).ToList());_redo.Clear();}
        private void Edit(Action action){Remember();action();RefreshObjects();}
        private void Undo(){if(_undo.Count==0)return;_redo.Push(_canvas.Shapes.Select(s=>s.Clone()).ToList());_canvas.Shapes=_undo.Pop();_canvas.Active=null;RefreshObjects();}
        private void Redo(){if(_redo.Count==0)return;_undo.Push(_canvas.Shapes.Select(s=>s.Clone()).ToList());_canvas.Shapes=_redo.Pop();_canvas.Active=null;RefreshObjects();}
        private void RefreshObjects(){if(_apply!=null)_apply.Enabled=_canvas.Shapes.Any(s=>!s.Hidden && !s.IsEraser && s.Points.Count>0);_refresh=true;_objects.BeginUpdate();_objects.Items.Clear();foreach(var shape in _canvas.Shapes)_objects.Items.Add(shape);_objects.DisplayMember="Layer";_objects.SelectedItem=_canvas.Active;_objects.EndUpdate();_refresh=false;_properties.SelectedObject=_canvas.Active;_propertySnapshot=_canvas.Shapes.Select(s=>s.Clone()).ToList();_canvas.Invalidate();}
        private void Scale(float factor){if(_canvas.Active==null)return;Edit(()=>{var shape=_canvas.Active;var b=shape.GetBounds();foreach(var p in shape.Points){p.X=b.Left+(p.X-b.Left)*factor;p.Y=b.Top+(p.Y-b.Top)*factor;}shape.TransformText((x,y)=>(b.Left+(x-b.Left)*factor,b.Top+(y-b.Top)*factor),false);});}
        private void ChooseColor(){if(_canvas.Active==null)return;using(var dialog=new ColorDialog{Color=_canvas.Active.ForeColor})if(dialog.ShowDialog(this)==DialogResult.OK)Edit(()=>_canvas.Active.ForeColor=dialog.Color);}
        private void Background(){using(var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK){using(var image=Image.FromFile(dialog.FileName)){_canvas.BackgroundImage?.Dispose();_canvas.BackgroundImage=new Bitmap(image);}_canvas.Invalidate();}}
        private void ImportSvg(){using(var dialog=new OpenFileDialog{Filter="SVG|*.svg"})if(dialog.ShowDialog(this)==DialogResult.OK){var result=SvgImporter.Import(File.ReadAllText(dialog.FileName),_canvas.ScriptWidth,_canvas.ScriptHeight,NextLayer());Edit(()=>_canvas.Shapes.AddRange(result.Shapes));}}
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
            using(var fontScope=Nikse.SubtitleEdit.Logic.ClassicImageExport.FontFaces.UseFonts(_canvas.RenderFonts))
            {
            var result = new List<Paragraph>();
            foreach (var layer in _canvas.Shapes.Where(s => !s.Hidden).GroupBy(s => s.Layer).OrderBy(g => g.Key))
            {
                var erase = string.Join(" ", layer.Where(s => s.IsEraser && !s.IsTextBox).Select(s => s.ToAssa()));
                foreach (var shape in layer.Where(s => !s.IsEraser && !s.IsTextBox && s.Points.Count > 0))
                {
                    var tags = new System.Text.StringBuilder("{\\an7\\pos(0,0)\\shad0\\1c" + AssaDrawTextLine.ToAssaColor(shape.ForeColor) + "\\1a" + AssaDrawTextLine.ToAssaAlpha(shape.ForeColor));
                    AssaDrawTextLine.AppendOutline(tags, shape.OutlineWidth, shape.OutlineColor);
                    if (erase.Length > 0) tags.Append("\\iclip(").Append(erase).Append(')');
                    tags.Append("\\p1}").Append(shape.ToAssa()).Append("{\\p0}");
                    result.Add(new Paragraph(_cue, true) { Layer = layer.Key, Extra = "ClassicDraw", Text = tags.ToString() });
                }
                foreach (var shape in layer.Where(s => s.HasText && s.Points.Count > 0))
                    result.Add(new Paragraph(_cue, true) { Layer = layer.Key, Extra = "ClassicDrawText", Text = AssaDrawTextLine.Format(shape) });
            }
            return result;
            }
        }
        private sealed class DrawCanvas : Control
        {
            public List<ClassicFontFile> RenderFonts;
            public List<DrawShape> Shapes=new List<DrawShape>();public DrawShape Active;public int ScriptWidth=1280,ScriptHeight=720,Tool;public Action BeforeEdit,Changed;private PointF _start;private DrawCoordinate _point;private bool _drag;private float ScaleFactor=>Math.Max(.01f,Math.Min(Width/(float)ScriptWidth,Height/(float)ScriptHeight));
            public DrawCanvas(){DoubleBuffered=true;BackColor=Color.FromArgb(35,35,35);TabStop=true;}
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(ScaleFactor,ScaleFactor);
                if(BackgroundImage!=null)g.DrawImage(BackgroundImage,0,0,ScriptWidth,ScriptHeight);using(var pen=new Pen(Color.DimGray,1/ScaleFactor)){for(int x=0;x<ScriptWidth;x+=100)g.DrawLine(pen,x,0,x,ScriptHeight);for(int y=0;y<ScriptHeight;y+=100)g.DrawLine(pen,0,y,ScriptWidth,y);}
                foreach(var shape in Shapes.OrderBy(s=>s.Layer).Where(s=>!s.Hidden))
                {
                    using(var path=PathFor(shape))using(var brush=new SolidBrush(shape.ForeColor))using(var outline=new Pen(shape==Active?Color.Gold:(Color)shape.OutlineColor,Math.Max(1/ScaleFactor,shape.OutlineWidth))){if(!shape.IsTextBox){if(shape.Points.Count>2 && !shape.IsEraser)g.FillPath(brush,path);if(shape.IsEraser)outline.DashStyle=DashStyle.Dash;g.DrawPath(outline,path);}}
                    if(shape.HasText)
                    {
                        var b=shape.GetTextBounds();var frame=shape.GetTextFrame();var layout=AssaTextLayout.Layout(shape.Text,shape.FontName,shape.FontSize,shape.FontBold,b.Right-b.Left,b.Bottom-b.Top,shape.ShrinkToFit);
                        var state=g.Save();g.TranslateTransform(frame.CenterX,frame.CenterY);g.RotateTransform(frame.Angle);
                        using(var font=new Font(shape.FontName,layout.EmSize,shape.FontBold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(shape.TextColor))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap})
                            g.DrawString(string.Join(Environment.NewLine,layout.Lines),font,brush,new RectangleF(-frame.Width/2,-frame.Height/2,frame.Width,frame.Height),format);
                        g.Restore(state);
                    }
                    if(shape==Active)foreach(var p in shape.Points)using(var brush=new SolidBrush(p.PointColor))g.FillEllipse(brush,p.X-3/ScaleFactor,p.Y-3/ScaleFactor,6/ScaleFactor,6/ScaleFactor);
                }
            }
            private static GraphicsPath PathFor(DrawShape shape)
            {
                var path=new GraphicsPath(FillMode.Winding);if(shape.Points.Count==0)return path;var previous=shape.Points[0];
                for(int i=1;i<shape.Points.Count;i++){var p=shape.Points[i];if(p.DrawType==DrawCoordinateType.Move){path.StartFigure();previous=p;continue;}if(p.DrawType==DrawCoordinateType.BezierCurveSupport1 && i+2<shape.Points.Count){var p2=shape.Points[i+1];var end=shape.Points[i+2];path.AddBezier(previous.X,previous.Y,p.X,p.Y,p2.X,p2.Y,end.X,end.Y);previous=end;i+=2;}else{path.AddLine(previous.X,previous.Y,p.X,p.Y);previous=p;}}path.CloseFigure();return path;
            }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);Focus();var x=e.X/ScaleFactor;var y=e.Y/ScaleFactor;_start=new PointF(x,y);BeforeEdit?.Invoke();
                if(Tool==0){Active=Shapes.LastOrDefault(s=>!s.Hidden && s.HitTest(x,y,10/ScaleFactor));_point=Active?.Points.FirstOrDefault(p=>Math.Abs(p.X-x)<8/ScaleFactor && Math.Abs(p.Y-y)<8/ScaleFactor);}
                else if(Tool<3){if(Active==null){Active=new DrawShape();Shapes.Add(Active);}if(Active.Points.Count==0)Active.AddPoint(DrawCoordinateType.Move,x,y,Color.IndianRed);else if(Tool==2){var last=Active.Points.Last();Active.AddPoint(DrawCoordinateType.BezierCurveSupport1,last.X+(x-last.X)/3,last.Y,Color.LightGreen);Active.AddPoint(DrawCoordinateType.BezierCurveSupport2,last.X+2*(x-last.X)/3,y,Color.LightGreen);Active.AddPoint(DrawCoordinateType.BezierCurve,x,y,Color.IndianRed);}else Active.AddPoint(DrawCoordinateType.Line,x,y,Color.IndianRed);}
                else {Active=new DrawShape();Shapes.Add(Active);} _drag=true;Capture=true;Changed?.Invoke();
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);if(!_drag || Active==null)return;var x=e.X/ScaleFactor;var y=e.Y/ScaleFactor;
                if(Tool==0){if(_point!=null){_point.X=x;_point.Y=y;}else{Active.Offset(x-_start.X,y-_start.Y);_start=new PointF(x,y);}}
                else if(Tool==3){Active.Points.Clear();Active.AddPoint(DrawCoordinateType.Move,_start.X,_start.Y,Color.IndianRed);Active.AddPoint(DrawCoordinateType.Line,x,_start.Y,Color.IndianRed);Active.AddPoint(DrawCoordinateType.Line,x,y,Color.IndianRed);Active.AddPoint(DrawCoordinateType.Line,_start.X,y,Color.IndianRed);}
                else if(Tool==4){var oval=CircleBezier.MakeCircle((_start.X+x)/2,(_start.Y+y)/2,Math.Abs(x-_start.X)/2,Active.Layer,Active.ForeColor);var radius=Math.Max(.001f,Math.Abs(x-_start.X));foreach(var p in oval.Points)p.Y=(_start.Y+y)/2+(p.Y-(_start.Y+y)/2)*Math.Abs(y-_start.Y)/radius;Active.Points=oval.Points;foreach(var p in Active.Points)p.DrawShape=Active;}Invalidate();
            }
            protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);_drag=false;Capture=false;_point=null;Changed?.Invoke();}
        }
    }
}

