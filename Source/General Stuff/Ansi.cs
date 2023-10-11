
#nullable enable

namespace LanguageCore.ANSI
{
    public readonly struct BackgroundColor
    {
        public const int BLACK     = 40 + Color.ANSI_BLACK;
        public const int RED       = 40 + Color.ANSI_RED;
        public const int GREEN     = 40 + Color.ANSI_GREEN;
        public const int YELLOW    = 40 + Color.ANSI_YELLOW;
        public const int BLUE      = 40 + Color.ANSI_BLUE;
        public const int MAGENTA   = 40 + Color.ANSI_MAGENTA;
        public const int CYAN      = 40 + Color.ANSI_CYAN;
        public const int WHITE     = 40 + Color.ANSI_WHITE;

        public const int GRAY          = 100 + Color.ANSI_BLACK;
        public const int BRIGHT_RED    = 100 + Color.ANSI_RED;
        public const int BRIGHT_GREEN  = 100 + Color.ANSI_GREEN;
        public const int BRIGHT_YELLOW = 100 + Color.ANSI_YELLOW;
        public const int BRIGHT_BLUE   = 100 + Color.ANSI_BLUE;
        public const int BRIGHT_MAGENTA= 100 + Color.ANSI_MAGENTA;
        public const int BRIGHT_CYAN   = 100 + Color.ANSI_CYAN;
        public const int BRIGHT_WHITE  = 100 + Color.ANSI_WHITE;
    }

    public readonly struct ForegroundColor
    {
        public const int BLACK     = 30 + Color.ANSI_BLACK;
        public const int RED       = 30 + Color.ANSI_RED;
        public const int GREEN     = 30 + Color.ANSI_GREEN;
        public const int YELLOW    = 30 + Color.ANSI_YELLOW;
        public const int BLUE      = 30 + Color.ANSI_BLUE;
        public const int MAGENTA   = 30 + Color.ANSI_MAGENTA;
        public const int CYAN      = 30 + Color.ANSI_CYAN;
        public const int WHITE     = 30 + Color.ANSI_WHITE;

        public const int GRAY          = 90 + Color.ANSI_BLACK;
        public const int BRIGHT_RED    = 90 + Color.ANSI_RED;
        public const int BRIGHT_GREEN  = 90 + Color.ANSI_GREEN;
        public const int BRIGHT_YELLOW = 90 + Color.ANSI_YELLOW;
        public const int BRIGHT_BLUE   = 90 + Color.ANSI_BLUE;
        public const int BRIGHT_MAGENTA= 90 + Color.ANSI_MAGENTA;
        public const int BRIGHT_CYAN   = 90 + Color.ANSI_CYAN;
        public const int BRIGHT_WHITE  = 90 + Color.ANSI_WHITE;
    }

    public readonly struct Style
    {
        public const int ANSI_BOLD         = 1;
        public const int ANSI_UNDERLINE    = 4;
        public const int ANSI_INVERT       = 7;
    }

    readonly struct Color
    {
        public const int ANSI_BLACK        = 0;
        public const int ANSI_RED          = 1;
        public const int ANSI_GREEN        = 2;
        public const int ANSI_YELLOW       = 3;
        public const int ANSI_BLUE         = 4;
        public const int ANSI_MAGENTA      = 5;
        public const int ANSI_CYAN         = 6;
        public const int ANSI_WHITE        = 7;
    }

    public static class Generator
    {
        public static string Generate(int code) => $"\x1b[{code}m";

        public static string Generate(int code, string text) => $"\x1b[{code}m{text}\x1b[0m";
    }
}
