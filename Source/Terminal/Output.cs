namespace LanguageCore;

public delegate void PrintCallback(string message, LogType logType);

public static class Output
{
    static ProgramArguments arguments;

    public static bool LogDebugs => (arguments.LogFlags & LogType.Debug) != 0;
    public static bool LogInfos => (arguments.LogFlags & LogType.Normal) != 0;
    public static bool LogWarnings => (arguments.LogFlags & LogType.Warning) != 0;

    const ConsoleColor InfoColor = ConsoleColor.Blue;
    const ConsoleColor WarningColor = ConsoleColor.DarkYellow;
    const ConsoleColor ErrorColor = ConsoleColor.Red;
    const ConsoleColor DebugColor = ConsoleColor.DarkGray;

    public static void SetProgramArguments(ProgramArguments arguments) => Output.arguments = arguments;

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

    public static void LogInfo(Information information)
    {
        if (!LogInfos) return;

        Console.ForegroundColor = InfoColor;
        Console.WriteLine(information.ToString());

        string? arrows = information.GetArrows();
        if (arrows != null)
        { Console.WriteLine(arrows); }
        Console.ResetColor();
    }

    public static void LogInfo(Hint hint)
    {
        if (!LogInfos) return;

        Console.ForegroundColor = InfoColor;
        Console.WriteLine(hint.ToString());

        string? arrows = hint.GetArrows();
        if (arrows != null)
        { Console.WriteLine(arrows); }
        Console.ResetColor();
    }

    public static void LogError(string message)
    { Log(message, ErrorColor); }

    public static void LogError(LanguageException exception)
    {
        Console.ForegroundColor = ErrorColor;
        Console.WriteLine(exception.ToString());

        string? arrows = exception.GetArrows();
        if (arrows != null)
        { Console.WriteLine(arrows); }
        Console.ResetColor();
    }

    public static void LogError(LanguageError error)
    {
        Console.ForegroundColor = ErrorColor;
        Console.WriteLine(error.ToString());

        string? arrows = error.GetArrows();
        if (arrows != null)
        { Console.WriteLine(arrows); }
        Console.ResetColor();
    }

    public static void LogError(Exception exception)
    {
        Console.ForegroundColor = ErrorColor;
        Console.WriteLine(exception.ToString());
        Console.ResetColor();
    }

    public static void LogWarning(string message)
    { if (LogWarnings) Log(message, WarningColor); }

    public static void LogWarning(Warning warning)
    {
        if (!LogWarnings) return;

        Console.ForegroundColor = WarningColor;
        Console.WriteLine(warning.ToString());

        string? arrows = warning.GetArrows();
        if (arrows != null)
        { Console.WriteLine(arrows); }
        Console.ResetColor();
    }

    public static void LogDebug(string message)
    { if (LogDebugs) Log(message, DebugColor); }

    static void Log(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}

[Flags]
public enum LogType
{
    None = 0x0,
    Normal = 0x1,
    Warning = 0x2,
    Error = 0x4,
    Debug = 0x8,
}
