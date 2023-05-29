using System.Drawing;

namespace ConsoleGUI
{
    internal class Element : IElement, IElementWithEvents
    {
        public Rectangle Rect { get; set; } = Rectangle.Empty;

        internal Character[] DrawBuffer = System.Array.Empty<Character>();
        protected MouseEvent LastMouse;

        public virtual Character DrawContent(int x, int y) => DrawBuffer.Clamp(Utils.GetIndex(x, y, Rect.Width), ConsoleGUI.NullCharacter);

        internal void ClearBuffer() => DrawBuffer = new Character[Rect.Width * Rect.Height];

        public virtual void BeforeDraw()
        { if (DrawBuffer.Length == 0) ClearBuffer(); }

        public virtual void OnMouseEvent(MouseEvent e)
        {
            LastMouse = e;
        }
        public virtual void OnKeyEvent(KeyEvent e) { }
        public virtual void OnStart() { }

        public virtual void RefreshSize() => this.ClearBuffer();
    }
}
