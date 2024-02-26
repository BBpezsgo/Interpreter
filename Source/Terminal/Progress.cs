using System;

namespace LanguageCore;

public readonly struct ConsoleProgressBar : IDisposable
{
    static bool IsEnabled;
    public static void SetProgramArguments(ProgramArguments arguments) => IsEnabled = arguments.ShowProgress;

    readonly int Line;
    readonly ConsoleColor Color;
    readonly bool Show;

    public ConsoleProgressBar(ConsoleColor color, bool show)
    {
        Line = 0;
        Color = color;
        Show = show && IsEnabled;

        if (!Show) return;

        Line = Console.GetCursorPosition().Top;
        Console.WriteLine();
    }

    public void Print(int iterator, int count) => Print((float)(iterator) / (float)count);
    public void Print(float progress)
    {
        if (!Show) return;

        (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

        Console.SetCursorPosition(0, Line);

        int width = Console.WindowWidth;
        Console.ForegroundColor = Color;
        for (int i = 0; i < width; i++)
        {
            float v = (float)(i + 1) / (float)(width);
            if (v <= progress)
            { Console.Write('═'); }
            else
            { Console.Write(' '); }
        }
        Console.ResetColor();

        Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top);
    }

    public void Dispose()
    {
        if (!Show) return;

        (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

        Console.SetCursorPosition(0, Line);

        int width = Console.WindowWidth;
        for (int i = 0; i < width; i++)
        { Console.Write(' '); }

        Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top - 1);
    }
}

public struct ConsoleProgressLabel : IDisposable
{
    static bool IsEnabled;
    public static void SetProgramArguments(ProgramArguments arguments) => IsEnabled = arguments.ShowProgress;

    public string Label;

    readonly int Line;
    readonly ConsoleColor Color;
    readonly bool Show;

    public ConsoleProgressLabel(string label, ConsoleColor color, bool show)
    {
        Line = 0;
        Label = label;
        Color = color;
        Show = show && IsEnabled;

        if (!Show) return;

        Line = Console.GetCursorPosition().Top;
        Console.WriteLine();
    }

    public readonly void Print()
    {
        if (!Show) return;

        (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

        Console.SetCursorPosition(0, Line);

        int width = Console.WindowWidth;
        Console.ForegroundColor = Color;
        for (int i = 0; i < width; i++)
        {
            if (i < Label.Length)
            { Console.Write(Label[i]); }
            else
            { Console.Write(' '); }
        }
        Console.ResetColor();

        Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top);
    }

    public readonly void Dispose()
    {
        if (!Show) return;

        (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

        Console.SetCursorPosition(0, Line);

        int width = Console.WindowWidth;
        for (int i = 0; i < width; i++)
        { Console.Write(' '); }

        Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top - 1);
    }
}
