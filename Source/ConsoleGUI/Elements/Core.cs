using System.Collections.Generic;
using System.Drawing;

namespace ConsoleGUI
{
    public interface IElementWithSubelements : IElement
    {
        IElement[] Elements { get; }
    }

    public interface IElementWithEvents : IElement
    {
        void OnMouseEvent(MouseEvent mouse);
        void OnKeyEvent(KeyEvent e);
        void OnStart();
    }

    public interface IElementWithTitle
    {
        public string Title { get; }
    }

    public interface IElement
    {
        Rectangle Rect { get; set; }
        void RefreshSize();
        void BeforeDraw();
        Character DrawContent(int x, int y);
    }

    public interface IBorderedElement : IElement
    {
        bool HasBorder { get; }
    }

    public enum Side
    {
        None,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left,
    }

    public static class Extensions
    {
        public static Character DrawBorder(this IElement element, int x, int y)
        {
            if (!element.Contains(x, y)) return ConsoleGUI.NullCharacter;
            Side side = element.Rect.GetSide(x, y);
            if (element is IElementWithTitle elementWithTitle && !string.IsNullOrEmpty(elementWithTitle.Title))
            {
                string title = elementWithTitle.Title;
                int titleOffset = 2;
                int relativeX = x - element.Rect.X;
                if (side == Side.Top)
                {
                    if (relativeX == titleOffset - 1)
                    { return '┤'.Details(); }
                    if (relativeX == title.Length + titleOffset)
                    { return '├'.Details(); }
                    if (relativeX >= titleOffset && relativeX < title.Length + titleOffset)
                    { return title[relativeX - titleOffset].Details(); }
                }
            }
            return side switch
            {
                Side.TopLeft => ('┌').Details(),
                Side.Top => ('─').Details(),
                Side.TopRight => ('┒').Details(),
                Side.Right => '┃'.Details(),
                Side.BottomRight => '┛'.Details(),
                Side.Bottom => '━'.Details(),
                Side.BottomLeft => '┕'.Details(),
                Side.Left => '│'.Details(),
                _ => ConsoleGUI.NullCharacter,
            };
        }

        public static bool Contains(this IElement element, int x, int y)
            =>
            element.Rect.Contains(x, y) ||
            element.Rect.Contains(x - 1, y) ||
            element.Rect.Contains(x, y - 1) ||
            element.Rect.Contains(x - 1, y - 1);
        public static bool Contains(this IElement element, Position position)
            => element.Contains(position.X, position.Y);

        public static void OnMouseEvent(this IElement[] elements, MouseEvent e)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] is not IElementWithEvents element) continue;
                if (elements[i].Rect.IsEmpty) continue;
                if (!elements[i].Contains(e.X, e.Y)) continue;
                element.OnMouseEvent(e);
            }
        }

        public static void OnKeyEvent(this IElement[] elements, KeyEvent e)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] is not IElementWithEvents element) continue;
                if (elements[i].Rect.IsEmpty) continue;
                element.OnKeyEvent(e);
            }
        }

        public static Character DrawContentWithBorders(this IElement element, int x, int y)
        {
            if (element is IBorderedElement borderedElement && borderedElement.HasBorder)
            {
                if (borderedElement.Rect.Top == y ||
                    borderedElement.Rect.Left == x ||
                    borderedElement.Rect.Bottom == y ||
                    borderedElement.Rect.Right == x)
                { return borderedElement.DrawBorder(x, y); }

                return borderedElement.DrawContent(x - borderedElement.Rect.Left - 1, y - borderedElement.Rect.Top - 1);
            }

            return element.DrawContent(x - element.Rect.Left, y - element.Rect.Top);
        }

        public static Character? DrawContent(this IElement[] elements, int x, int y)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i].Rect.IsEmpty) continue;
                if (!elements[i].Contains(x, y)) continue;
                return elements[i].DrawContentWithBorders(x, y);
            }
            return null;
        }

        public static void BeforeDraw(this IElement[] elements)
        { for (int i = 0; i < elements.Length; i++) elements[i].BeforeDraw(); }

        public static void BeforeDraw(this IEnumerable<IElement> elements)
        { foreach (var element in elements) element.BeforeDraw(); }

        public static void RefreshSize(this IElement[] elements)
        { for (int i = 0; i < elements.Length; i++) elements[i].RefreshSize(); }

        public static void RefreshSize(this IEnumerable<IElement> elements)
        { foreach (var element in elements) element.RefreshSize(); }

        public static bool IsOutside<T>(this T[] v, int i) => (i < 0) || (i >= v.Length);
        public static T Clamp<T>(this T[] v, int i, T @default) => v.IsOutside(i) ? @default : v[i];
        public static T? Clamp<T>(this T[] v, int i) where T : struct => v.IsOutside(i) ? null : v[i];

        internal static Side GetSide(this System.Drawing.Rectangle v, int x, int y)
        {
            if (v.Left == x)
            {
                if (v.Top == y)
                {
                    return Side.TopLeft;
                }
                else if (v.Bottom == y)
                {
                    return Side.BottomLeft;
                }
                return Side.Left;
            }
            else if (v.Right == x)
            {
                if (v.Top == y)
                {
                    return Side.TopRight;
                }
                else if (v.Bottom == y)
                {
                    return Side.BottomRight;
                }
                return Side.Right;
            }
            else if (v.Bottom == y)
            {
                return Side.Bottom;
            }
            else if (v.Top == y)
            {
                return Side.Top;
            }
            return Side.None;
        }

        internal static Character Details(this char v) => new()
        {
            Char = v,
            Color = CharColors.FgDefault,
        };

        internal static string Escape(this string v) => v
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\0", "\\0");
    }

    internal static class Utils
    {
        public static int GetIndex(int x, int y, int width) => x + (y * width);
        public static int GetIndex(Position position, int width) => position.X + (position.Y * width);
    }
}
