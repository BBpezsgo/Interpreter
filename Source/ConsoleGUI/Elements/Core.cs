using System;
using System.Collections.Generic;
using System.Drawing;

namespace ConsoleGUI
{
    public class DrawBuffer
    {
        readonly Character[] v;

        int currentIndex;
        CharColors currentBgColor;
        CharColors currentFgColor;

        public DrawBuffer()
        {
            v = System.Array.Empty<Character>();
            currentIndex = 0;
        }

        public DrawBuffer(int size)
        {
            v = new Character[System.Math.Max(size, 0)];
            currentIndex = 0;
        }

        public int Length => v.Length;

        public Character this[int index]
        {
            get => v[index];
            set => v[index] = value;
        }

        public void StepTo(int index) => currentIndex = index;
        public int Step(int steps)
        {
            currentIndex += steps;
            return currentIndex;
        }
        public int Step() => Step(1);

        public int CurrentIndex => currentIndex;

        public CharColors ForegroundColor
        {
            get => currentFgColor;
            set => currentFgColor = value;
        }
        public CharColors BackgroundColor
        {
            get => currentBgColor;
            set => currentBgColor = value;
        }

        public void ResetColor()
        {
            this.ForegroundColor = CharColors.FgDefault;
            this.BackgroundColor = CharColors.BgBlack;
        }

        CharColors CurrentColor
        {
            get => currentFgColor | currentBgColor;
        }

        public bool AddChar(char v)
        {
            if (currentIndex >= this.v.Length) return false;
            if (currentIndex < 0) return false;

            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].Color = CurrentColor;
            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].Char = v;

            currentIndex++;
            if (currentIndex >= this.v.Length) return false;

            return true;
        }

        public bool SetChar(char v, int i)
        {
            if (i >= this.v.Length) return false;
            if (i < 0) return false;

            this.v[i].Color = CurrentColor;
            this.v[i].Char = v;

            return true;
        }
        public void SetText(string v, int from)
        {
            for (int i = 0; i < v.Length; i++)
            { if (!this.SetChar(v[i], i + from)) break; }
        }

        public bool AddChar(char v, CharColors color)
        {
            if (currentIndex >= this.v.Length) return false;

            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].Color = color;
            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].Char = v;

            currentIndex++;
            if (currentIndex >= this.v.Length) return false;

            return true;
        }

        public void AddText(string v)
        {
            for (int i = 0; i < v.Length; i++)
            { if (!this.AddChar(v[i])) break; }
        }

        public void AddSpace(int to, int totalWidth)
        {
            if (totalWidth == 0) return;
            while (this.currentIndex % totalWidth < to)
            { if (!this.AddChar(' ')) break; }
        }
        public void FinishLine(int totalWidth)
        {
            if (totalWidth == 0) return;
            this.AddSpace(totalWidth - 1, totalWidth);
            this.AddChar(' ');
        }

        public void Fill(CharColors color)
        {
            for (int i = 0; i < this.v.Length; i++)
            {
                this.v[i].Char = ' ';
                this.v[i].Color = color;
            }
            this.currentIndex = this.v.Length;
        }

        internal void FillRemaing()
        {
            for (int i = this.currentIndex; i < this.v.Length; i++)
            {
                this.v[i].Char = ' ';
            }
            this.currentIndex = this.v.Length;
        }
    }

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

    public interface IElement : IMainThreadThing
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

    public enum InlineLayoutSizeMode
    {
        Fixed,
        Stretchy,
    }

    public class InlineLayout
    {
        public InlineLayoutSizeMode SizeMode;
        public int Value;

        public InlineLayout(InlineLayoutSizeMode sizeMode, int v)
        {
            SizeMode = sizeMode;
            Value = v;
        }

        public static InlineLayout Fixed(int v) => new(InlineLayoutSizeMode.Fixed, System.Math.Max(v, 1));
        public static InlineLayout Stretchy(int percent) => new(InlineLayoutSizeMode.Stretchy, percent);
        public static InlineLayout Stretchy() => Stretchy(100);
    }

    public interface IInlineLayoutElement
    {
        public InlineLayout Layout { get; }
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

            return element.DrawContent(x, y);
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

        public static bool IsOutside(this DrawBuffer v, int i) => (i < 0) || (i >= v.Length);
        public static Character Clamp(this DrawBuffer v, int i, Character @default) => v.IsOutside(i) ? @default : v[i];
        public static Character? Clamp(this DrawBuffer v, int i) => v.IsOutside(i) ? null : v[i];

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
    }

    internal static class Utils
    {
        public static int GetIndex(int x, int y, int width) => x + (y * width);
        public static int GetIndex(Position position, int width) => position.X + (position.Y * width);
    }
}
