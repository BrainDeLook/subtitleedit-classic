using System;
using System.Windows.Forms;
using System.Runtime.CompilerServices;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic {
 public static class ClassicTextAutoWrap
 {
    private sealed class InputState { public int Length=-1; }
    private static readonly ConditionalWeakTable<Control,InputState> States=new ConditionalWeakTable<Control,InputState>();
    public static void Attach(TextBox box)=>Attach(box,()=>box.SelectionStart,(text,caret)=>{box.Text=text;box.SelectionStart=caret;});
    public static void Attach(SETextBox box)=>Attach(box,()=>box.SelectionStart,(text,caret)=>{box.Text=text;box.SelectionStart=caret;});
    private static void Attach(Control box,Func<int> caret,Action<string,int> update)
    {
        if(States.TryGetValue(box,out _))return;
        var input=new InputState();States.Add(box,input);
        box.KeyPress+=(sender,e)=>input.Length=char.IsControl(e.KeyChar)?-1:HtmlUtil.RemoveHtmlTags(box.Text,true).Length;
        box.KeyUp+=(sender,e)=>input.Length=-1;
        box.TextChanged+=(sender,e)=>
        {
            var before=input.Length;input.Length=-1;var maximum=Configuration.Settings.General.SubtitleLineMaximumLength;
            if(!Configuration.Settings.General.AutoWrapLineWhileTyping || before<0 || before>maximum)return;
            if(TryWrap(box.Text,caret(),maximum,()=>"",out var wrapped,out var position))update(wrapped,position);
        };
    }
    public static bool TryWrap(string text, int caretIndex, int maxLength, Func<string> getLanguage, out string newText, out int newCaretIndex)
    {
        newText = text;
        newCaretIndex = caretIndex;

        if (maxLength <= 0 || text.Contains('\n') || text.Contains('\r'))
        {
            return false;
        }

        var length = HtmlUtil.RemoveHtmlTags(text, true).Length;
        if (length <= maxLength)
        {
            return false;
        }

        string wrapped;
        if (length > maxLength + 30)
        {
            wrapped = Utilities.AutoBreakLine(text, maxLength, 0, getLanguage());
        }
        else
        {
            var lastSpace = LastSpaceOutsideTags(text);
            if (lastSpace <= 0)
            {
                return false;
            }

            wrapped = text.Remove(lastSpace, 1).Insert(lastSpace, Environment.NewLine);
        }

        var breakIndex = wrapped.IndexOf(Environment.NewLine, StringComparison.Ordinal);
        if (breakIndex <= 0 || wrapped == text)
        {
            return false;
        }

        newText = wrapped;
        newCaretIndex = caretIndex >= text.Length
            ? wrapped.Length
            : MapCaret(text, caretIndex, wrapped);

        return true;
    }

    // The last space that is not inside an HTML tag (<font face="Arial">) or an ASSA override
    // block ({\fn Times New Roman}) - breaking there would corrupt the tag. -1 when none.
    private static int LastSpaceOutsideTags(string text)
    {
        var lastSpace = -1;
        var inHtmlTag = false;
        var inAssaTag = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inHtmlTag)
            {
                inHtmlTag = c != '>';
            }
            else if (inAssaTag)
            {
                inAssaTag = c != '}';
            }
            else if (c == '<')
            {
                inHtmlTag = true;
            }
            else if (c == '{')
            {
                inAssaTag = true;
            }
            else if (c == ' ')
            {
                lastSpace = i;
            }
        }

        return lastSpace;
    }

    // Re-breaking can add, remove or move whitespace anywhere in the line, so map the caret by the
    // number of non-whitespace characters in front of it rather than by the length difference.
    private static int MapCaret(string oldText, int caretIndex, string newText)
    {
        var count = 0;
        for (var i = 0; i < caretIndex && i < oldText.Length; i++)
        {
            if (!char.IsWhiteSpace(oldText[i]))
            {
                count++;
            }
        }

        var index = 0;
        var seen = 0;
        while (index < newText.Length && seen < count)
        {
            if (!char.IsWhiteSpace(newText[index]))
            {
                seen++;
            }

            index++;
        }

        // The caret was after whitespace (e.g. the space just typed, now a line break) - keep it
        // after the whitespace in the new text too, so it sits at the start of the next word.
        if (caretIndex > 0 && char.IsWhiteSpace(oldText[caretIndex - 1]))
        {
            while (index < newText.Length && char.IsWhiteSpace(newText[index]))
            {
                index++;
            }
        }

        return Math.Min(index, newText.Length);
    }
}

}
