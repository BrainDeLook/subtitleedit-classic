using System;
using System.Drawing;
namespace Classic54Tests {
sealed class SplitterPaint: IDisposable {public Color Color {get;set;} public void Dispose() {}}
sealed class SplitterCanvas: IDisposable {readonly Graphics graphics; public SplitterCanvas(Bitmap bitmap) {graphics=Graphics.FromImage(bitmap);} public void Clear(Color color)=>graphics.Clear(color); public void DrawRect(float x,float y,float w,float h,SplitterPaint paint) {using(var brush=new SolidBrush(paint.Color)) graphics.FillRectangle(brush,x,y,w,h);} public void DrawRect(RectangleF rectangle,SplitterPaint paint)=>DrawRect(rectangle.X,rectangle.Y,rectangle.Width,rectangle.Height,paint); public void Dispose()=>graphics.Dispose();}
}
