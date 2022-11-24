using System.Drawing;

namespace ConsoleGUI
{
    using ConsoleLib;

    internal class BaseElement
    {
        internal Rectangle Rect;

        internal Character[] DrawBuffer = new Character[0];

        internal virtual Character OnDrawContent(int X, int Y)
        {
            int i = X + (Y * Rect.Width);
            if (i < 0 || i >= DrawBuffer.Length) return ' '.Details();
            return DrawBuffer[i];
        }

        internal void ClearBuffer() => DrawBuffer = new Character[Rect.Width * Rect.Height];

        internal Character OnDrawBorder(int X, int Y) => Rect.GetSide(X, Y) switch
        {
            Side.TopLeft => ('┌').Details(),
            Side.Top => ('─').Details(),
            Side.TopRight => ('┒').Details(),
            Side.Right => '┃'.Details(),
            Side.BottomRight => '┛'.Details(),
            Side.Bottom => '━'.Details(),
            Side.BottomLeft => '┕'.Details(),
            Side.Left => '│'.Details(),
            _ => ' '.Details(),
        };

        public bool Contains(int X, int Y) => Rect.Contains(X, Y) || Rect.Contains(X - 1, Y - 1) || Rect.Contains(X - 1, Y) || Rect.Contains(X, Y - 1);

        internal virtual void BeforeDraw()
        {
            if (DrawBuffer.Length == 0) ClearBuffer();
        }

        internal virtual void OnMouseEvent(MouseInfo mouse) { }

        internal virtual void OnKeyEvent(NativeMethods.KEY_EVENT_RECORD e) { }

        internal virtual void RefreshSize() { this.ClearBuffer(); }
    }
}
