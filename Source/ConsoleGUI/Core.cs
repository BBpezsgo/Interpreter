using Win32;

namespace ConsoleGUI
{
    public readonly struct CharacterBrush
    {
        public static ConsoleChar Solid(byte color) => new(' ', color, color);
        public static ConsoleChar ErrorChar => new(' ', ByteColor.BrightMagenta, ByteColor.BrightMagenta);
    }
}
