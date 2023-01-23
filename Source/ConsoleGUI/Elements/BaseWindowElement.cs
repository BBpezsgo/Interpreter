using System.Drawing;

namespace ConsoleGUI
{
    internal class BaseWindowElement : BaseElement
    {
        public bool IsFocused { get; private set; }
        bool IsDragging;

        Point MouseDragStart = Point.Empty;
        Point MouseDragStartPos = Point.Empty;

        internal bool CanDrag = true;
        internal bool HasBorder = true;
        internal BaseInlineElement[] Elements = System.Array.Empty<BaseInlineElement>();

        internal override void BeforeDraw()
        {
            base.BeforeDraw();

            foreach (var element in Elements)
            {
                element.BeforeDraw();
            }
        }

        Character DrawElement(BaseInlineElement Element, int X, int Y)
        {
            if (HasBorder)
            {
                if (Element.Rect.Top == Y)
                {
                    return Element.OnDrawBorder(X, Y);
                }
                else if (Element.Rect.Left == X)
                {
                    return Element.OnDrawBorder(X, Y);
                }
                else if (Element.Rect.Bottom == Y)
                {
                    return Element.OnDrawBorder(X, Y);
                }
                else if (Element.Rect.Right == X)
                {
                    return Element.OnDrawBorder(X, Y);
                }

                return Element.OnDrawContent(X - Element.Rect.Left - 1, Y - Element.Rect.Top - 1);
            }

            return Element.OnDrawContent(X - Element.Rect.Left - 1, Y - Element.Rect.Top - 1);
        }

        internal override Character OnDrawContent(int X, int Y)
        {
            int i = X + (Y * Rect.Width);

            for (int j = 0; j < Elements.Length; j++)
            {
                var element = Elements[j];
                if (!element.Contains(X, Y)) continue;
                return DrawElement(element, X, Y);
            }

            if (i < 0 || i >= DrawBuffer.Length) return ConsoleGUI.NullCharacter;

            return DrawBuffer[i];
        }

        internal void OnMouseEventBase(MouseInfo mouse)
        {
            if (!CanDrag) return;

            if (mouse.ButtonState != MouseInfo.ButtonStateEnum.Left)
            {
                MouseDragStart = Point.Empty;
                MouseDragStartPos = Point.Empty;
                IsFocused = false;
                IsDragging = false;
            }

            IsFocused = true;

            if (IsDragging)
            {
                var offset = new Point(mouse.X - MouseDragStart.X, mouse.Y - MouseDragStart.Y);
                Rect.X = MouseDragStartPos.X + offset.X;
                Rect.Y = MouseDragStartPos.Y + offset.Y;
                return;
            }

            if (mouse.Y != Rect.Top) return;

            MouseDragStartPos = Rect.Location;
            MouseDragStart = new Point(mouse.X, mouse.Y);
            IsDragging = true;
        }
        internal override void RefreshSize()
        {
            base.RefreshSize();
            foreach (var element in Elements) element.RefreshSize();
        }
    }
}
