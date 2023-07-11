using ConsoleDrawer;

using System;
using System.Collections.Generic;
using System.Drawing;

namespace ConsoleGUI
{
    public class DrawBuffer
    {
        readonly Character[] v;

        int currentIndex;
        readonly int Width;
        readonly int Height;
        BackgroundColor currentBgColor;
        ForegroundColor currentFgColor;

        public DrawBuffer()
        {
            this.v = System.Array.Empty<Character>();
            this.currentIndex = 0;
        }

        public DrawBuffer(int width, int height)
        {
            this.v = new Character[System.Math.Max(width * height, 0)];
            this.currentIndex = 0;
            this.Width = width;
            this.Height = height;
        }

        public int Length => v.Length;

        public Character this[int index]
        {
            get => v[index];
            set => v[index] = value;
        }

        public Character this[int x, int y]
        {
            get => this[x + (y * Width)];
            set => this[x + (y * Width)] = value;
        }

        static readonly (int x, int y, char character)[] LineSegments = new (int, int, char)[]
        {
            (-1, -1, '┐'),
            (-1, 0, '─'),
            (-1, 1, '┘'),

            (0, -1, '│'),
            (0, 0, '+'),
            (0, 1, '│'),

            (1, -1, '┌'),
            (1, 0, '─'),
            (1, 1, '└'),
        };

        static (int x, int y)[] SimplifyLine_((int x, int y)[] line)
        {
            List<(int x, int y)> result = new();
            (int x, int y) dirOld = (0, 0);

            for (int i = 1; i < line.Length; i++)
            {
                (int x, int y) dirNew = new(line[i - 1].x - line[i].x, line[i - 1].y - line[i].y);

                if (dirNew == (0, 0)) continue;

                if (dirNew != dirOld)
                {
                    result.Add(line[i]);
                }

                dirOld = dirNew;
            }
            return result.ToArray();
        }

        static (int x, int y)[] SimplifyLine((int x, int y)[] points)
        {
            if (points.Length < 2) return points;

            List<(int x, int y)> newPoints = new(points);

            (int x, int y) prevP = newPoints[^1];

            for (int i = newPoints.Count - 2; i >= 0; i--)
            {
                (int x, int y) p = newPoints[i];

                if (p == prevP)
                { newPoints.RemoveAt(i); continue; }

                prevP.x = p.x;
                prevP.y = p.y;
            }

            return newPoints.ToArray();
        }

        public void DrawLine((int x, int y)[] points, BackgroundColor color)
        {
            if (points.Length == 0) return;

            points = SimplifyLine(points);

            if (points.Length == 1)
            {
                this[points[0].x, points[0].y] = Character.Solid(color);
                return;
            }

            for (int i = 1; i < points.Length; i++)
            {
                (int x, int y) _prevPoint = points[i - 1];
                (int x, int y) _point = points[i];

                (int x, int y) prevPoint = (Math.Min(_prevPoint.x, _point.x), Math.Min(_prevPoint.y, _point.y));
                (int x, int y) point = (Math.Max(_prevPoint.x, _point.x), Math.Max(_prevPoint.y, _point.y));

                for (int x = prevPoint.x; x <= point.x; x++)
                {
                    for (int y = prevPoint.y; y <= point.y; y++)
                    {
                        char c = '█';
                        /*
                        if (i + 1 < points.Length)
                        {
                            // (int x, int y) nextPoint = (points[i + 1].x, points[i + 1].y);

                            // (int x, int y) dir = (Math.Clamp(prevPoint.x - point.x, -1, 1), Math.Clamp(prevPoint.y - point.y, -1, 1));
                            // (int x, int y) nextDir = (Math.Clamp(point.x - nextPoint.x, -1, 1), Math.Clamp(point.y - nextPoint.y, -1, 1));

                            c = '█';
                        }
                        */

                        this[x, y] = Character.Solid(color);
                    }
                }
            }
        }
        public void DrawLine((int, int) p1, (int, int) p2, BackgroundColor color)
            => DrawLine(p1.Item1, p1.Item2, p2.Item1, p2.Item2, color);
        public void DrawLine(int x1, int y1, int x2, int y2, BackgroundColor color)
        {
            (int, int)? prev = (x1, y1);
            for (int x = x1; x <= x2; x++)
            {
                for (int y = y1; y <= y2; y++)
                {
                    char c = '+';
                    if (prev != null)
                    {
                        int dx = x - prev.Value.Item1;
                        int dy = y - prev.Value.Item2;
                        for (int segment = 0; segment < LineSegments.Length; segment++)
                        {
                            if (LineSegments[segment].Item1 == dx &&
                                LineSegments[segment].Item2 == dy)
                            {
                                c = LineSegments[segment].Item3;
                                break;
                            }
                        }

                        if (c == '+')
                        {

                        }
                    }

                    this[x, y] = new Character(c, color.ToForeground(), color);

                    prev = (x, y);
                }
            }
        }

        public void StepTo(int index) => currentIndex = index;
        public int Step(int steps)
        {
            currentIndex += steps;
            return currentIndex;
        }
        public int Step() => Step(1);

        public int CurrentIndex => currentIndex;

        public ForegroundColor ForegroundColor
        {
            get => currentFgColor;
            set => currentFgColor = value;
        }
        public BackgroundColor BackgroundColor
        {
            get => currentBgColor;
            set => currentBgColor = value;
        }

        public void ResetColor()
        {
            this.ForegroundColor = ForegroundColor.Default;
            this.BackgroundColor = BackgroundColor.Black;
        }

        short CurrentColor
        {
            get => (short)((short)currentFgColor | (short)currentBgColor);
        }

        public bool AddChar(char v)
        {
            if (currentIndex >= this.v.Length) return false;
            if (currentIndex < 0) return false;

            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].ForegroundColor = currentFgColor;
            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].BackgroundColor = currentBgColor;
            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].Char = v;

            currentIndex++;
            if (currentIndex >= this.v.Length) return false;

            return true;
        }

        public bool SetChar(char v, int i)
        {
            if (i >= this.v.Length) return false;
            if (i < 0) return false;

            this.v[i].ForegroundColor = currentFgColor;
            this.v[i].BackgroundColor = currentBgColor;
            this.v[i].Char = v;

            return true;
        }
        public void SetText(string v, int from)
        {
            for (int i = 0; i < v.Length; i++)
            { if (!this.SetChar(v[i], i + from)) break; }
        }

        public bool AddChar(char v, ForegroundColor fg, BackgroundColor bg)
        {
            if (currentIndex >= this.v.Length) return false;

            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].ForegroundColor = fg;
            this.v[System.Math.Clamp(currentIndex, 0, this.v.Length - 1)].BackgroundColor = bg;
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

        public void Fill(BackgroundColor color)
        {
            for (int i = 0; i < this.v.Length; i++)
            {
                this.v[i].Char = ' ';
                this.v[i].ForegroundColor = color.ToForeground();
                this.v[i].BackgroundColor = color;
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
        void OnDestroy();
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
        public static bool Contains(this IElement element, Coord position)
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
            ForegroundColor = ForegroundColor.Default,
            BackgroundColor = BackgroundColor.Black,
        };
    }

    internal static class Utils
    {
        public static int GetIndex(int x, int y, int width) => x + (y * width);
        public static int GetIndex(Coord position, int width) => position.X + (position.Y * width);
    }
}
