using System.Diagnostics;

namespace LanguageCore.TUI;

public struct ConsoleWriter : IBufferWriter<ConsoleWriter>
{
    int _offset;

    public int Offset => _offset;

    static ConsoleColor ToConsoleColor(AnsiColor ansiColor) => ansiColor switch
    {
        AnsiColor.Black => ConsoleColor.Black,
        AnsiColor.Red => ConsoleColor.DarkRed,
        AnsiColor.Green => ConsoleColor.DarkGreen,
        AnsiColor.Yellow => ConsoleColor.DarkYellow,
        AnsiColor.Blue => ConsoleColor.DarkBlue,
        AnsiColor.Magenta => ConsoleColor.DarkMagenta,
        AnsiColor.Cyan => ConsoleColor.DarkCyan,
        AnsiColor.White => ConsoleColor.Gray,
        AnsiColor.Default => ConsoleColor.Gray,
        AnsiColor.BrightBlack => ConsoleColor.DarkGray,
        AnsiColor.BrightRed => ConsoleColor.Red,
        AnsiColor.BrightGreen => ConsoleColor.Green,
        AnsiColor.BrightYellow => ConsoleColor.Yellow,
        AnsiColor.BrightBlue => ConsoleColor.Blue,
        AnsiColor.BrightMagenta => ConsoleColor.Magenta,
        AnsiColor.BrightCyan => ConsoleColor.Cyan,
        AnsiColor.BrightWhite => ConsoleColor.White,
        _ => throw new UnreachableException(),
    };

    public ConsoleWriter Write(ReadOnlySpan<char> text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        if (foreground == AnsiColor.Default || background == AnsiColor.Default) Console.ResetColor();
        if (foreground != AnsiColor.Default) Console.ForegroundColor = ToConsoleColor(foreground);
        if (background != AnsiColor.Default) Console.BackgroundColor = ToConsoleColor(background);
        Console.Write(text.ToString());
        _offset += text.Length;
        return this;
    }

    public ConsoleWriter Write(char text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        if (foreground == AnsiColor.Default || background == AnsiColor.Default) Console.ResetColor();
        if (foreground != AnsiColor.Default) Console.ForegroundColor = ToConsoleColor(foreground);
        if (background != AnsiColor.Default) Console.BackgroundColor = ToConsoleColor(background);
        Console.Write(text);
        _offset++;
        return this;
    }

    public ConsoleWriter Write(char c, int repeat, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        if (foreground == AnsiColor.Default || background == AnsiColor.Default) Console.ResetColor();
        if (foreground != AnsiColor.Default) Console.ForegroundColor = ToConsoleColor(foreground);
        if (background != AnsiColor.Default) Console.BackgroundColor = ToConsoleColor(background);
        for (int i = 0; i < repeat; i++)
        {
            Console.Write(c);
        }
        _offset += repeat;
        return this;
    }
}
