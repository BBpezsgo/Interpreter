using System.Runtime.CompilerServices;

namespace LanguageCore;

[ExcludeFromCodeCoverage]
public static class Program
{
    const string _thisFileName = nameof(Program) + ".cs";
    static string? _projectPath;
    public static string ProjectPath => _projectPath ??= GetProjectPath();

    static string GetProjectPath([CallerFilePath] string? callerFilePath = null)
    {
        if (callerFilePath is null || !callerFilePath.EndsWith(_thisFileName, StringComparison.Ordinal)) throw new Exception($"Failed to get the project path");
        return callerFilePath[..^(_thisFileName.Length + 1)];
    }

    public static void Main(string[] args) =>
#if DEBUG
        DevelopmentEntry.Start(args);
#else
        Entry.Run(args);
#endif

}
