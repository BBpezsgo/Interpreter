using System.Drawing;
using Win32;
using Point = System.Drawing.Point;

namespace ConsoleGUI
{
    internal class WindowElement : Element
    {
        public bool IsFocused { get; private set; }
        bool IsDragging;

        Point MouseDragStart = Point.Empty;
        Point MouseDragStartPos = Point.Empty;

        internal bool CanDrag = true;
        internal bool HasBorder = true;
        internal IElement[] Elements = System.Array.Empty<IElement>();

        public override void BeforeDraw()
        {
            base.BeforeDraw();
            Elements.BeforeDraw();
        }

        public override Character DrawContent(int x, int y) => Elements.DrawContent(x, y) ?? DrawBuffer.Clamp(Utils.GetIndex(x, y, Rect.Width), ConsoleGUI.NullCharacter);

        internal void OnMouseEventBase(MouseEvent mouse)
        {
            if (!CanDrag) return;

            if (mouse.ButtonState != MouseButtonState.Left)
            {
                MouseDragStart = Point.Empty;
                MouseDragStartPos = Point.Empty;
                IsFocused = false;
                IsDragging = false;
            }

            IsFocused = true;

            if (IsDragging)
            {
                var offset = new Point(mouse.MousePosition.X - MouseDragStart.X, mouse.MousePosition.Y - MouseDragStart.Y);
                var newRect = Rect;
                newRect.X = MouseDragStartPos.X + offset.X;
                newRect.Y = MouseDragStartPos.Y + offset.Y;
                Rect = newRect;
                return;
            }

            if (mouse.MousePosition.Y != Rect.Top) return;

            MouseDragStartPos = Rect.Location;
            MouseDragStart = new Point(mouse.MousePosition.X, mouse.MousePosition.Y);
            IsDragging = true;
        }
        public override void RefreshSize()
        {
            base.RefreshSize();
            Elements.RefreshSize();
        }
    }
}
