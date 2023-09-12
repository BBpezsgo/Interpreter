using System;
using System.Diagnostics;
using Win32;

namespace ConsoleGUI
{
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
