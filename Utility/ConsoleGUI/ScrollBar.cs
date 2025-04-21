using Win32.Console;

namespace ConsoleGUI;

[ExcludeFromCodeCoverage]
public class ScrollBar
{
    public readonly Func<Element, LanguageCore.Range<int>> GetRange;
    readonly Element Parent;

    public int Offset { get; set; }

    float ScrollPercent
    {
        get
        {
            LanguageCore.Range<int> range = GetRange.Invoke(Parent);
            Offset -= range.Start;
            int max = range.End - range.Start;
            return (float)Offset / (float)max;
        }
    }

    public ScrollBar(Func<Element, LanguageCore.Range<int>> getRange, Element parent)
    {
        GetRange = getRange;
        Parent = parent;
    }

    public void Draw(DrawBuffer buffer)
    {
        int height = Parent.Rect.Height - 1;
        int x = Parent.Rect.Width - 2;

        float scrollPercent = ScrollPercent;
        int scrollY = (int)MathF.Round(height * scrollPercent);

        for (int y = 0; y < height; y++)
        {
            if (y == scrollY)
            {
                buffer[x, y] = new CLI.AnsiChar(' ', CLI.AnsiColor.Black, CLI.AnsiColor.White);
            }
            else
            {
                buffer[x, y] = new CLI.AnsiChar((char)0x2592, CLI.AnsiColor.Gray, CLI.AnsiColor.Black);
            }
        }
    }

    public void FeedEvent(Element sender, MouseEvent e)
    {
        LanguageCore.Range<int> range = GetRange.Invoke(sender);
        if (e.EventFlags.HasFlag(MouseEventFlags.MouseWheeled))
        {
            Offset = Math.Clamp(Offset - Math.Sign(e.Scroll), range.Start, range.End);
            return;
        }

        var currentPos = e.MousePosition;
        currentPos.X -= (short)sender.Rect.X;
        currentPos.Y -= (short)(sender.Rect.Y + 1);

        var pressedPos = ConsoleMouse.LeftPressedAt;
        pressedPos.X -= (short)sender.Rect.X;
        pressedPos.Y -= (short)(sender.Rect.Y + 1);

        if (pressedPos.X != Parent.Rect.Width - 1)
        { return; }

        if ((e.ButtonState & (uint)MouseButton.Left) != 0)
        {
            int height = Parent.Rect.Height - 1;

            int y = currentPos.Y;
            y = Math.Clamp(y, 0, height);

            float v = y;
            v /= (float)height;

            v *= range.End - range.Start;
            v += range.Start;

            Offset = (int)MathF.Round(v);
        }
    }

    public void FeedEvent(Element sender, KeyEvent e)
    {
        if (!sender.IsFocused) return;

        LanguageCore.Range<int> range = GetRange.Invoke(sender);

        if (e.IsDown == 1 && e.VirtualKeyCode == Win32.VirtualKeyCode.Up)
        {
            Offset = Math.Clamp(Offset - 1, range.Start, range.End);
            return;
        }

        if (e.IsDown == 1 && e.VirtualKeyCode == Win32.VirtualKeyCode.Down)
        {
            Offset = Math.Clamp(Offset + 1, range.Start, range.End);
            return;
        }
    }
}
