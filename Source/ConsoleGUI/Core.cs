using Win32;

namespace ConsoleGUI
{
    public readonly struct Brush
    {
        public static CharInfo Solid(byte color) => new(' ', color, color);
        public static CharInfo ErrorChar => new(' ', ByteColor.BrightMagenta, ByteColor.BrightMagenta);
    }
}
