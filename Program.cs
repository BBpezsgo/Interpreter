#if DEBUG
#pragma warning disable CS0162 // Unreachable code detected
#endif

namespace LanguageCore;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static void Main(string[] args)
    {
#if DEBUG
        DevelopmentEntry.Start(args);
        return;
#endif

        Entry.Run(args);
    }
}
