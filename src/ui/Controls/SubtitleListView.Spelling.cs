using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Controls
{
    public sealed partial class SubtitleListView
    {
        public ClassicGridSpelling GridSpelling { get; set; }
        private readonly Dictionary<string,List<ClassicGridSpellHit>> _gridSpellingHits=new Dictionary<string,List<ClassicGridSpellHit>>();
        public void RefreshGridSpelling(){_gridSpellingHits.Clear();Invalidate();}
        public bool SpellingKeyboardMenu { get; private set; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            SpellingKeyboardMenu=e.KeyCode==Keys.Apps || (e.KeyCode==Keys.F10 && e.Shift);
            base.OnKeyDown(e);
        }
        protected override void OnMouseDown(MouseEventArgs e){SpellingKeyboardMenu=false;base.OnMouseDown(e);}
        public ClassicGridSpellHit FirstSpellingHit(int row) => _gridSpellingHits.TryGetValue(row+":"+ColumnIndexText,out var hits)?hits.FirstOrDefault():null;
        public ClassicGridSpellHit SpellingAt(Point point,out int row,out bool original)
        {
            row=-1;original=false;
            var item=HitTest(point);
            if(item.Item==null || item.SubItem==null)return null;
            var column=item.Item.SubItems.IndexOf(item.SubItem);row=item.Item.Index;original=column==ColumnIndexTextOriginal;
            if(column!=ColumnIndexText && !original)return null;
            return _gridSpellingHits.TryGetValue(row+":"+column,out var hits)?hits.FirstOrDefault(h=>h.Bounds.Contains(point)):null;
        }
    }
}
