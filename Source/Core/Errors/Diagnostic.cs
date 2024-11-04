
namespace LanguageCore;

public enum DiagnosticsLevel
{
    Error,
    Warning,
    Information,
    Hint,
}

[ExcludeFromCodeCoverage]
public class Diagnostic : IDiagnostic
{
    public DiagnosticsLevel Level { get; }
    public string Message { get; }
    public Position Position { get; }
    public Uri? File { get; }

#if DEBUG
    bool _isDebugged;
#endif

    Diagnostic(DiagnosticsLevel level, string message, Position position, Uri? file, bool @break)
    {
        Level = level;
        Message = message;
        Position = position;
        File = file;
        _isDebugged = false;

        if (@break)
        { Break(); }
    }

    public Diagnostic(DiagnosticsLevel level, string message, Position position, Uri? file)
    {
        Level = level;
        Message = message;
        Position = position;
        File = file;
        _isDebugged = false;

        if (level == DiagnosticsLevel.Error)
        { Break(); }
    }

    [DoesNotReturn]
    public void Throw() => throw new LanguageException(Message, Position, File);

    public static Diagnostic Critical(string message, IPositioned? position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Error, message, position?.Position ?? Position.UnknownPosition, file, @break);

    public static Diagnostic Error(string message, IPositioned position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Error, message, position.Position, file, @break);

    public static Diagnostic Warning(string message, IPositioned position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Warning, message, position.Position, file, @break);

    public static Diagnostic Information(string message, IPositioned position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Information, message, position.Position, file, @break);

    public static Diagnostic Hint(string message, IPositioned position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Hint, message, position.Position, file, @break);

    public static Diagnostic Critical(string message, Position position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Error, message, position, file, @break);

    public static Diagnostic Error(string message, Position position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Error, message, position, file, @break);

    public static Diagnostic Warning(string message, Position position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Warning, message, position, file, @break);

    public static Diagnostic Information(string message, Position position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Information, message, position, file, @break);

    public static Diagnostic Hint(string message, Position position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Hint, message, position, file, @break);

    public static Diagnostic Error(string message, Position? position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Error, message, position ?? Position.UnknownPosition, file, @break);

    public static Diagnostic Warning(string message, Position? position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Warning, message, position ?? Position.UnknownPosition, file, @break);

    public static Diagnostic Information(string message, Position? position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Information, message, position ?? Position.UnknownPosition, file, @break);

    public static Diagnostic Hint(string message, Position? position, Uri? file, bool @break = true)
        => new(DiagnosticsLevel.Hint, message, position ?? Position.UnknownPosition, file, @break);

    public static Diagnostic Critical(string message, ILocated location, bool @break = true)
        => new(DiagnosticsLevel.Error, message, location.Location.Position, location.Location.File, @break);

    public static Diagnostic Error(string message, ILocated location, bool @break = true)
        => new(DiagnosticsLevel.Error, message, location.Location.Position, location.Location.File, @break);

    public static Diagnostic Warning(string message, ILocated location, bool @break = true)
        => new(DiagnosticsLevel.Warning, message, location.Location.Position, location.Location.File, @break);

    public static Diagnostic Information(string message, ILocated location, bool @break = true)
        => new(DiagnosticsLevel.Information, message, location.Location.Position, location.Location.File, @break);

    public static Diagnostic Hint(string message, ILocated location, bool @break = true)
        => new(DiagnosticsLevel.Hint, message, location.Location.Position, location.Location.File, @break);

    public Diagnostic Break()
    {
#if DEBUG
        if (!_isDebugged)
        { Debugger.Break(); }
        _isDebugged = true;
#endif
        return this;
    }

    public string? GetArrows()
    {
        if (File == null) return null;
        if (!File.IsFile) return null;
        return LanguageException.GetArrows(Position, System.IO.File.ReadAllText(File.AbsolutePath));
    }

    public override string ToString()
    {
        StringBuilder result = new(Message);

        result.Append(Position.ToStringCool().Surround(" (at ", ")"));

        if (File != null)
        { result.Append($" (in {File})"); }

        return result.ToString();
    }
}
