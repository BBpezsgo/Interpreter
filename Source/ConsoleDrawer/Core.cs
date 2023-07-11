using ConsoleDrawer;

using System;
using System.Diagnostics;

namespace ConsoleGUI
{
    [Flags]
    public enum ForegroundColor : short
    {
        Black = 0,
        Gray = CharInfoAttributes.FOREGROUND_INTENSITY,
        Red = CharInfoAttributes.FOREGROUND_RED,
        DarkGreen = CharInfoAttributes.FOREGROUND_GREEN,
        DarkBlue = CharInfoAttributes.FOREGROUND_BLUE,
        Orange = Red | DarkGreen,
        LightBlue = DarkGreen | DarkBlue,
        Magenta = DarkBlue | Red,
        Default = Red | DarkGreen | DarkBlue,

        LightRed = Red | CharInfoAttributes.FOREGROUND_INTENSITY,
        Green = DarkGreen | CharInfoAttributes.FOREGROUND_INTENSITY,
        Blue = DarkBlue | CharInfoAttributes.FOREGROUND_INTENSITY,
        Yellow = Orange | CharInfoAttributes.FOREGROUND_INTENSITY,
        Cyan = LightBlue | CharInfoAttributes.FOREGROUND_INTENSITY,
        White = Default | CharInfoAttributes.FOREGROUND_INTENSITY,
    }

    [Flags]
    public enum BackgroundColor : short
    {
        Black = ForegroundColor.Black << 4,
        Gray = ForegroundColor.Gray << 4,
        Red = ForegroundColor.Red << 4,
        Green = ForegroundColor.Green << 4,
        Blue = ForegroundColor.Blue << 4,
        Yellow = ForegroundColor.Yellow << 4,
        Cyan = ForegroundColor.Cyan << 4,
        Magenta = ForegroundColor.Magenta << 4,
        White = ForegroundColor.White << 4,
    }

    public static class ConsoleColorExtensions
    {
        public static BackgroundColor ToBackground(this ForegroundColor color)
            => (BackgroundColor)((short)color << 4);

        public static ForegroundColor ToForeground(this BackgroundColor color)
            => (ForegroundColor)((short)color >> 4);
    }

#if NET6_0
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
#endif
    public struct Character
    {
        public char Char;
        public ForegroundColor ForegroundColor;
        public BackgroundColor BackgroundColor;

        public Character(char @char, ForegroundColor foregroundColor, BackgroundColor backgroundColor)
        {
            this.Char = @char;
            this.ForegroundColor = foregroundColor;
            this.BackgroundColor = backgroundColor;
        }

        public static Character Solid(ForegroundColor color) => new(' ', color, color.ToBackground());
        public static Character Solid(BackgroundColor color) => new(' ', color.ToForeground(), color);

        public override readonly string ToString() => Char.ToString();
        readonly string GetDebuggerDisplay() => ToString();

        public static Character ErrorChar => new(' ', ForegroundColor.Magenta, BackgroundColor.Magenta);
    }
}
