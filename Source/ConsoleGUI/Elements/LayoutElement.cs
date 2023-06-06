using System;
using System.Drawing;

namespace ConsoleGUI
{
    internal class LayoutElement : Element, IElement, IElementWithSubelements, IElementWithEvents, IInlineLayoutElement
    {
        public IElement[] Elements { get; set; } = Array.Empty<IElement>();
        public InlineLayout Layout { get; set; } = InlineLayout.Stretchy();

        public override void BeforeDraw()
        {
            base.BeforeDraw();
            Elements.BeforeDraw();
        }

        public override Character DrawContent(int x, int y)
        {
            return Elements.DrawContent(x, y) ?? Character.ErrorChar;
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

            int total = Rect.Height - 1;
            int elementCount = Elements.Length;
            int currentPosition = Rect.Y;

            for (int i = 0; i < Elements.Length; i++)
            {
                IElement element = Elements[i];
                Rectangle rect = element.Rect;

                int currentHeight;

                if (i == elementCount - 1)
                { currentHeight = total - currentPosition + 1; }
                else
                { currentHeight = total / elementCount; }

                if (Elements[i] is IInlineLayoutElement inlineLayoutElement)
                {
                    var layout = inlineLayoutElement.Layout;
                    switch (layout.SizeMode)
                    {
                        case InlineLayoutSizeMode.Fixed:
                            {
                                rect.Height = layout.Value;
                                break;
                            }
                        case InlineLayoutSizeMode.Stretchy:
                            {
                                if (i == elementCount - 1)
                                { rect.Height = currentHeight; }
                                else
                                { rect.Height = (int)Math.Round((float)currentHeight * ((float)layout.Value / 100f)); }
                                break;
                            }
                        default: throw new NotImplementedException();
                    }
                    rect.Y = currentPosition;
                }
                else
                {
                    rect.Height = currentHeight;
                    rect.Y = currentPosition;
                }

                rect.Width = Rect.Width;
                rect.X = Rect.X;

                currentPosition = rect.Bottom + 1;

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

            int total = Rect.Width - 1;
            int elementCount = Elements.Length;
            int currentPosition = Rect.X;

            for (int i = 0; i < Elements.Length; i++)
            {
                IElement element = Elements[i];
                Rectangle rect = element.Rect;

                if (Elements[i] is IInlineLayoutElement inlineLayoutElement)
                {
                    var layout = inlineLayoutElement.Layout;
                    switch (layout.SizeMode)
                    {
                        case InlineLayoutSizeMode.Fixed:
                            {
                                rect.Width = layout.Value;
                                break;
                            }
                        case InlineLayoutSizeMode.Stretchy:
                            {
                                if (i == elementCount - 1)
                                { rect.Width = total - currentPosition + 1; }
                                else
                                { rect.Width = (int)Math.Round((float)total / (float)elementCount * ((float)layout.Value / 100f)); }
                                break;
                            }
                        default: throw new NotImplementedException();
                    }
                    rect.X = currentPosition;
                }
                else
                {
                    if (i == elementCount - 1)
                    { rect.Width = total - currentPosition + 1; }
                    else
                    { rect.Width = total / elementCount; }

                    rect.X = currentPosition;
                }

                rect.Height = Rect.Height;
                rect.Y = Rect.Y;

                if (element is IBorderedElement borderedElement && borderedElement.HasBorder)
                {
                    rect.Width -= 0;
                    rect.Height -= 0;
                    currentPosition = rect.Right + 1;
                }
                else
                {
                    currentPosition = rect.Right + 1;
                }

                element.Rect = rect;

                element.RefreshSize();
            }
        }
    }
}
