using Win32.Console;

namespace ConsoleGUI;

public static class CharacterBrush
{
    public static ConsoleChar Solid(byte color) => new(' ', color, color);
    public static ConsoleChar ErrorChar => new(' ', CharColor.BrightMagenta, CharColor.BrightMagenta);
}
