using ConsoleGUI.ConsoleLib;

using System;
using System.Drawing;

namespace ConsoleGUI
{
    internal class LayoutElement : Element, IElement, IElementWithSubelements, IElementWithEvents
    {
        public IElement[] Elements { get; set; } = Array.Empty<IElement>();

        public override void BeforeDraw()
        {
            base.BeforeDraw();
            Elements.BeforeDraw();
        }

        public override Character DrawContent(int x, int y)
        {
            return Elements.DrawContent(x, y) ?? base.DrawContent(x, y);
        }

        public override void OnMouseEvent(MouseEvent e)
        {
            base.OnMouseEvent(e);
            Elements.OnMouseEvent(e);
        }

        public override void OnKeyEvent(KeyEvent e)
        {
            base.OnKeyEvent(e);
            Elements.OnKeyEvent(e);
        }
    }

    internal class VerticalLayoutElement : LayoutElement
    {
        public override void RefreshSize()
        {
            base.RefreshSize();

            int total = Rect.Height;
            int elementCount = Elements.Length;
            int currentPosition = Rect.Y;

            for (int i = 0; i < Elements.Length; i++)
            {
                IElement element = Elements[i];
                Rectangle rect = element.Rect;

                rect.Width = Rect.Width;
                rect.X = Rect.X;

                rect.Height = total / elementCount;
                rect.Y = currentPosition;

                if (element is IBorderedElement borderedElement && borderedElement.HasBorder)
                {
                    rect.Width -= 2;
                    rect.Height -= 2;
                    currentPosition = rect.Bottom + 1;
                }
                else
                {
                    currentPosition = rect.Bottom + 1;
                }

                element.Rect = rect;

                element.RefreshSize();
            }
        }
    }

    internal class HorizontalLayoutElement : LayoutElement
    {
        public override void RefreshSize()
        {
            base.RefreshSize();

            int total = Rect.Width;
            int elementCount = Elements.Length;
            int currentPosition = Rect.X;

            for (int i = 0; i < Elements.Length; i++)
            {
                IElement element = Elements[i];
                Rectangle rect = element.Rect;

                rect.Height = Rect.Height;
                rect.Y = Rect.Y;

                rect.Width = total / elementCount;
                rect.X = currentPosition;

                if (element is IBorderedElement borderedElement && borderedElement.HasBorder)
                {
                    rect.Width -= 2;
                    rect.Height -= 2;
                    currentPosition = rect.Bottom + 1;
                }
                else
                {
                    currentPosition = rect.Bottom + 1;
                }

                element.Rect = rect;

                element.RefreshSize();
            }
        }
    }
}
