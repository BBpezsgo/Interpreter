using System.Drawing;
using System.Runtime.Versioning;
using Win32.Console;

namespace ConsoleGUI;

[ExcludeFromCodeCoverage]
public class LayoutElement : Element, IElementWithSubelements, IInlineLayoutElement
{
    public Element[] Elements { get; set; } = Array.Empty<Element>();
    public InlineLayout Layout { get; set; } = InlineLayout.Stretchy();

    public override void BeforeDraw()
    {
        base.BeforeDraw();
        Elements.BeforeDraw();
    }

    public override ConsoleChar DrawContent(int x, int y)
    {
        return Elements.DrawContent(x, y) ?? CharacterBrush.ErrorChar;
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

[ExcludeFromCodeCoverage]
public class VerticalLayoutElement : LayoutElement
{
    public override void RefreshSize()
    {
        base.RefreshSize();

        int total = Rect.Height - 1;
        int elementCount = Elements.Length;
        int currentPosition = Rect.Y;

        for (int i = 0; i < Elements.Length; i++)
        {
            Element element = Elements[i];
            Rectangle rect = element.Rect;

            int currentHeight;

            if (i == elementCount - 1)
            { currentHeight = total - currentPosition + 1; }
            else
            { currentHeight = total / elementCount; }

            if (Elements[i] is IInlineLayoutElement inlineLayoutElement)
            {
                InlineLayout layout = inlineLayoutElement.Layout;
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
                    default: throw new UnreachableException();
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

[ExcludeFromCodeCoverage]
public class HorizontalLayoutElement : LayoutElement
{
    public override void RefreshSize()
    {
        base.RefreshSize();

        int total = Rect.Width - 1;
        int elementCount = Elements.Length;
        int currentPosition = Rect.X;

        for (int i = 0; i < Elements.Length; i++)
        {
            Element element = Elements[i];
            Rectangle rect = element.Rect;

            if (Elements[i] is IInlineLayoutElement inlineLayoutElement)
            {
                InlineLayout layout = inlineLayoutElement.Layout;
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
                    default: throw new UnreachableException();
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

            if (element.HasBorder)
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
