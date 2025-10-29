using System.Diagnostics;

namespace LanguageCore.TUI;

readonly struct AnsiCharacter
{
    public readonly char Char;
    public readonly AnsiColor Foreground;
    public readonly AnsiColor Background;

    [DebuggerStepThrough]
    public AnsiCharacter(char @char, AnsiColor foreground)
    {
        Char = @char;
        Foreground = foreground;
        Background = AnsiColor.Default;
    }

    [DebuggerStepThrough]
    public AnsiCharacter(char @char, AnsiColor foreground, AnsiColor background)
    {
        Char = @char;
        Foreground = foreground;
        Background = background;
    }

    [DebuggerStepThrough]
    public AnsiCharacter(char @char)
    {
        Char = @char;
        Foreground = AnsiColor.Default;
        Background = AnsiColor.Default;
    }

    [DebuggerStepThrough]
    public static implicit operator AnsiCharacter(char v) => new(v);
}
