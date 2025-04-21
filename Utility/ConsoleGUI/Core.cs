namespace ConsoleGUI;

[ExcludeFromCodeCoverage]
public static class CharacterBrush
{
    public static CLI.AnsiChar Solid(CLI.AnsiColor color) => new(' ', color, color);
    public static CLI.AnsiChar ErrorChar => new(' ', CLI.AnsiColor.BrightMagenta, CLI.AnsiColor.BrightMagenta);
}
