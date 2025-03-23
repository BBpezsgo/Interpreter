namespace LanguageCore;

public delegate void PrintCallback(string message, LogType logType);

[ExcludeFromCodeCoverage]
public static class Output
{
    public static bool LogDebugs { get; set; }
    public static bool LogInfos { get; set; }
    public static bool LogWarnings { get; set; }

    const ConsoleColor InfoColor = ConsoleColor.Blue;
    const ConsoleColor WarningColor = ConsoleColor.DarkYellow;
    const ConsoleColor ErrorColor = ConsoleColor.Red;
    const ConsoleColor DebugColor = ConsoleColor.DarkGray;

    public static void Log(string message, LogType logType)
    {
        switch (logType)
        {
            case LogType.Normal:
                Output.LogInfo(message);
                break;
            case LogType.Warning:
                Output.LogWarning(message);
                break;
            case LogType.Error:
                Output.LogError(message);
                break;
            case LogType.Debug:
                Output.LogDebug(message);
                break;
        }
    }

    public static void LogInfo(string message)
    { if (LogInfos) Log(message, InfoColor); }

    public static void LogError(string message) => Log(message, ErrorColor);

    public static void LogError(LanguageException exception, IEnumerable<ISourceProvider>? sourceProviders = null)
    {
        Console.ForegroundColor = ErrorColor;
        Console.WriteLine(exception.ToString());

        (string SourceCode, string Arrows)? arrows = exception.GetArrows(sourceProviders);
        if (arrows.HasValue)
        {
            Console.WriteLine(arrows.Value.SourceCode);
            Console.WriteLine(arrows.Value.Arrows);
        }
        Console.ResetColor();
    }

    public static void LogDiagnostic(Diagnostic diagnostic, IEnumerable<ISourceProvider>? sourceProviders = null)
        => LogDiagnostic(diagnostic, 0, sourceProviders);

    static void LogDiagnostic(Diagnostic diagnostic, int depth, IEnumerable<ISourceProvider>? sourceProviders = null)
    {
        if (!(diagnostic.Level switch
        {
            DiagnosticsLevel.Error => true,
            DiagnosticsLevel.Warning => LogWarnings,
            DiagnosticsLevel.Information => LogInfos,
            DiagnosticsLevel.Hint => LogInfos,
            DiagnosticsLevel.OptimizationNotice => false,
            _ => false,
        }))
        { return; }

        Console.ForegroundColor = diagnostic.Level switch
        {
            DiagnosticsLevel.Error => ErrorColor,
            DiagnosticsLevel.Warning => WarningColor,
            DiagnosticsLevel.Information => InfoColor,
            DiagnosticsLevel.Hint => InfoColor,
            DiagnosticsLevel.OptimizationNotice => DebugColor,
            _ => DebugColor,
        };

        Console.Write(new string(' ', depth * 2));
        Console.WriteLine(diagnostic.ToString());
        (string SourceCode, string Arrows)? arrows = diagnostic.GetArrows(sourceProviders);
        if (arrows.HasValue)
        {
            Console.Write(new string(' ', depth * 2));
            Console.WriteLine(arrows.Value.SourceCode);
            Console.Write(new string(' ', depth * 2));
            Console.WriteLine(arrows.Value.Arrows);
        }

        if (diagnostic.SubErrors.Length > 0)
        {
            Console.Write(new string(' ', depth * 2));
            Console.WriteLine("-->");
        }

        Console.ResetColor();

        foreach (Diagnostic subdiagnostic in diagnostic.SubErrors)
        { LogDiagnostic(subdiagnostic, depth + 1, sourceProviders); }
    }

    public static void LogError(Exception exception)
    {
        Console.ForegroundColor = ErrorColor;
        Console.WriteLine(exception.ToString());
        Console.ResetColor();
    }

    public static void LogWarning(string message)
    { if (LogWarnings) Log(message, WarningColor); }

    public static void LogDebug(string message)
    { if (LogDebugs) Log(message, DebugColor); }

    static void Log(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}

public enum LogType
{
    Normal,
    Warning,
    Error,
    Debug,
}
