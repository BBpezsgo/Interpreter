using System.Drawing;
using System.Runtime.Versioning;
using Win32.Console;

namespace ConsoleGUI;

[ExcludeFromCodeCoverage]
public class DrawBuffer
{
    static readonly ImmutableArray<(int x, int y, char character)> LineSegments = ImmutableArray.Create<(int, int, char)>(
        (-1, -1, '┐'),
        (-1, 0, '─'),
        (-1, 1, '┘'),

        (0, -1, '│'),
        (0, 0, '+'),
        (0, 1, '│'),

        (1, -1, '┌'),
        (1, 0, '─'),
        (1, 1, '└')
    );

    public int CurrentLine => Width == 0 ? 0 : CurrentIndex / Width;
    public int CurrentColumn => Width == 0 ? 0 : CurrentIndex % Width;
    public int CurrentIndex { get; private set; }

    public int Length => ConsoleBuffer.Length;

    public byte ForegroundColor { get; set; }
    public byte BackgroundColor { get; set; }

    public int Width { get; }
    public int Height { get; }

    readonly ConsoleChar[] ConsoleBuffer;

    public ConsoleChar this[int index]
    {
        get => ConsoleBuffer[index];
        set => ConsoleBuffer[index] = value;
    }

    public ConsoleChar this[int x, int y]
    {
        get => this[x + (y * Width)];
        set => this[x + (y * Width)] = value;
    }

    public DrawBuffer()
    {
        this.ConsoleBuffer = Array.Empty<ConsoleChar>();
        this.CurrentIndex = 0;
    }

    public DrawBuffer(int width, int height)
    {
        this.ConsoleBuffer = new ConsoleChar[Math.Max(width * height, 0)];
        this.CurrentIndex = 0;
        this.Width = width;
        this.Height = height;
    }

    /*
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
    */

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

    public void DrawLine((int x, int y)[] points, byte color)
    {
        if (points.Length == 0) return;

        points = SimplifyLine(points);

        if (points.Length == 1)
        {
            this[points[0].x, points[0].y] = CharacterBrush.Solid(color);
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
                    /*
                    char c = '█'
                    if (i + 1 < points.Length)
                    {
                        // (int x, int y) nextPoint = (points[i + 1].x, points[i + 1].y);

                        // (int x, int y) dir = (Math.Clamp(prevPoint.x - point.x, -1, 1), Math.Clamp(prevPoint.y - point.y, -1, 1));
                        // (int x, int y) nextDir = (Math.Clamp(point.x - nextPoint.x, -1, 1), Math.Clamp(point.y - nextPoint.y, -1, 1));

                        c = '█';
                    }
                    */

                    this[x, y] = CharacterBrush.Solid(color);
                }
            }
        }
    }
    public void DrawLine((int, int) p1, (int, int) p2, byte color)
        => DrawLine(p1.Item1, p1.Item2, p2.Item1, p2.Item2, color);
    public void DrawLine(int x1, int y1, int x2, int y2, byte color)
    {
        (int, int)? prev = (x1, y1);
        for (int x = x1; x <= x2; x++)
        {
            for (int y = y1; y <= y2; y++)
            {
                char c = '+';
                if (prev.HasValue)
                {
                    int dx = x - prev.Value.Item1;
                    int dy = y - prev.Value.Item2;
                    for (int segment = 0; segment < LineSegments.Length; segment++)
                    {
                        if (LineSegments[segment].x == dx &&
                            LineSegments[segment].y == dy)
                        {
                            c = LineSegments[segment].character;
                            break;
                        }
                    }
                }

                this[x, y] = new ConsoleChar(c, color, CharColor.Black);

                prev = (x, y);
            }
        }
    }

    public void StepTo(int index) => CurrentIndex = index;
    public int Step(int steps)
    {
        CurrentIndex += steps;
        return CurrentIndex;
    }
    public int Step() => Step(1);

    public void ResetColor()
    {
        this.ForegroundColor = CharColor.Silver;
        this.BackgroundColor = CharColor.Black;
    }

    public bool AddChar(char v)
    {
        if (CurrentIndex >= this.ConsoleBuffer.Length) return false;
        if (CurrentIndex < 0) return false;

        this.ConsoleBuffer[Math.Clamp(CurrentIndex, 0, this.ConsoleBuffer.Length - 1)] = new ConsoleChar(v, ForegroundColor, BackgroundColor);

        CurrentIndex++;
        if (CurrentIndex >= this.ConsoleBuffer.Length) return false;

        return true;
    }

    public bool SetChar(char v, int i)
    {
        if (i >= this.ConsoleBuffer.Length) return false;
        if (i < 0) return false;

        this.ConsoleBuffer[i].Foreground = ForegroundColor;
        this.ConsoleBuffer[i].Background = BackgroundColor;
        this.ConsoleBuffer[i].Char = v;

        return true;
    }
    public void SetText(string v, int from)
    {
        for (int i = 0; i < v.Length; i++)
        { if (!this.SetChar(v[i], i + from)) break; }
    }

    public bool AddChar(char v, byte fg, byte bg)
    {
        if (CurrentIndex >= this.ConsoleBuffer.Length) return false;

        this.ConsoleBuffer[Math.Clamp(CurrentIndex, 0, this.ConsoleBuffer.Length - 1)] = new ConsoleChar(v, fg, bg);

        CurrentIndex++;
        if (CurrentIndex >= this.ConsoleBuffer.Length) return false;

        return true;
    }

    public void AddText(string v)
    {
        for (int i = 0; i < v.Length; i++)
        { if (!this.AddChar(v[i])) break; }
    }

    public void AddText(char v, int count)
    {
        for (int i = 0; i < count; i++)
        { if (!this.AddChar(v)) break; }
    }

    public void AddText(char v) => this.AddChar(v);

    public void AddSpace(int to)
    {
        if (Width == 0) return;
        while (this.CurrentIndex % Width < to)
        { if (!this.AddChar(' ')) break; }
    }
    public void FinishLine()
    {
        if (Width == 0) return;
        this.AddSpace(Width - 1);
        this.AddChar(' ');
    }

    public void Fill(byte color)
    {
        for (int i = 0; i < this.ConsoleBuffer.Length; i++)
        {
            this.ConsoleBuffer[i].Char = ' ';
            this.ConsoleBuffer[i].Foreground = color;
            this.ConsoleBuffer[i].Background = color;
        }
        this.CurrentIndex = this.ConsoleBuffer.Length;
    }

    public void FillRemaining()
    {
        for (int i = this.CurrentIndex; i < this.ConsoleBuffer.Length; i++)
        {
            this.ConsoleBuffer[i].Char = ' ';
        }
        this.CurrentIndex = this.ConsoleBuffer.Length;
    }
}

public interface IElementWithSubelements
{
    Element[] Elements { get; }
}

public enum InlineLayoutSizeMode
{
    Fixed,
    Stretchy,
}

[ExcludeFromCodeCoverage]
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

[ExcludeFromCodeCoverage]
public static class Extensions
{
    [SupportedOSPlatform("windows")]
    public static ConsoleChar DrawBorder(this Element element, int x, int y)
    {
        if (!element.Contains(x, y)) return ConsoleChar.Empty;
        Side side = element.Rect.GetSide(x, y);
        if (!string.IsNullOrEmpty(element.Title))
        {
            const int titleOffset = 2;
            int relativeX = x - element.Rect.X;
            if (side == Side.Top)
            {
                if (relativeX == titleOffset - 1)
                { return '┤'.Details(); }
                if (relativeX == element.Title.Length + titleOffset)
                { return '├'.Details(); }
                if (relativeX >= titleOffset && relativeX < element.Title.Length + titleOffset)
                { return element.Title[relativeX - titleOffset].Details(); }
            }
        }
        return side switch
        {
            Side.TopLeft => '┌'.Details(),
            Side.Top => '─'.Details(),
            Side.TopRight => '┒'.Details(),
            Side.Right => '┃'.Details(),
            Side.BottomRight => '┛'.Details(),
            Side.Bottom => '━'.Details(),
            Side.BottomLeft => '┕'.Details(),
            Side.Left => '│'.Details(),
            _ => ConsoleChar.Empty,
        };
    }

    [SupportedOSPlatform("windows")]
    public static bool Contains(this Element element, int x, int y)
        =>
        element.Rect.Contains(x, y) ||
        element.Rect.Contains(x - 1, y) ||
        element.Rect.Contains(x, y - 1) ||
        element.Rect.Contains(x - 1, y - 1);
    [SupportedOSPlatform("windows")]
    public static bool Contains(this Element element, Coord position)
        => element.Contains(position.X, position.Y);

    [SupportedOSPlatform("windows")]
    public static void OnMouseEvent(this Element[] elements, MouseEvent e)
    {
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] is not Element element) continue;
            if (elements[i].Rect.IsEmpty) continue;
            if (!elements[i].Contains(e.MousePosition.X, e.MousePosition.Y)) continue;
            element.OnMouseEvent(e);
        }
    }

    [SupportedOSPlatform("windows")]
    public static void OnKeyEvent(this Element[] elements, KeyEvent e)
    {
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] is not Element element) continue;
            if (elements[i].Rect.IsEmpty) continue;
            element.OnKeyEvent(e);
        }
    }

    [SupportedOSPlatform("windows")]
    public static ConsoleChar DrawContentWithBorders(this Element element, int x, int y)
    {
        if (element.HasBorder)
        {
            if (element.Rect.Top == y ||
                element.Rect.Left == x ||
                element.Rect.Bottom == y ||
                element.Rect.Right == x)
            { return element.DrawBorder(x, y); }

            return element.DrawContent(x - element.Rect.Left - 1, y - element.Rect.Top - 1);
        }

        return element.DrawContent(x, y);
    }

    [SupportedOSPlatform("windows")]
    public static ConsoleChar? DrawContent(this Element[] elements, int x, int y)
    {
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i].Rect.IsEmpty) continue;
            if (!elements[i].Contains(x, y)) continue;
            return elements[i].DrawContentWithBorders(x, y);
        }
        return null;
    }

    [SupportedOSPlatform("windows")]
    public static void BeforeDraw(this Element[] elements)
    { for (int i = 0; i < elements.Length; i++) elements[i].BeforeDraw(); }

    [SupportedOSPlatform("windows")]
    public static void BeforeDraw(this IEnumerable<Element> elements)
    { foreach (Element element in elements) element.BeforeDraw(); }

    [SupportedOSPlatform("windows")]
    public static void RefreshSize(this Element[] elements)
    { for (int i = 0; i < elements.Length; i++) elements[i].RefreshSize(); }

    [SupportedOSPlatform("windows")]
    public static void RefreshSize(this IEnumerable<Element> elements)
    { foreach (Element element in elements) element.RefreshSize(); }

    public static bool IsOutside<T>(this T[] v, int i) => (i < 0) || (i >= v.Length);
    public static T Clamp<T>(this T[] v, int i, T @default) => v.IsOutside(i) ? @default : v[i];
    public static T? Clamp<T>(this T[] v, int i) where T : struct => v.IsOutside(i) ? null : v[i];

    public static bool IsOutside(this DrawBuffer v, int i) => (i < 0) || (i >= v.Length);
    public static ConsoleChar Clamp(this DrawBuffer v, int i, ConsoleChar @default) => v.IsOutside(i) ? @default : v[i];
    public static ConsoleChar? Clamp(this DrawBuffer v, int i) => v.IsOutside(i) ? null : v[i];

    public static Side GetSide(this Rectangle v, int x, int y)
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

    public static ConsoleChar Details(this char v) => new(v, CharColor.Silver, CharColor.Black);
}

[ExcludeFromCodeCoverage]
public static class Utils
{
    public static int GetIndex(int x, int y, int width) => x + (y * width);
    public static int GetIndex(Coord position, int width) => position.X + (position.Y * width);
}
