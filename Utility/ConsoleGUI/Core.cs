using Win32.Console;

namespace ConsoleGUI;

[ExcludeFromCodeCoverage]
public static class CharacterBrush
{
    public static ConsoleChar Solid(byte color) => new(' ', color, color);
    public static ConsoleChar ErrorChar => new(' ', CharColor.BrightMagenta, CharColor.BrightMagenta);
}
