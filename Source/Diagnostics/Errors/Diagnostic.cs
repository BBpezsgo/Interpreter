
namespace LanguageCore;

public enum DiagnosticsLevel
{
    Error,
    Warning,
    Information,
    Hint,
    OptimizationNotice,
}

[ExcludeFromCodeCoverage]
public class Diagnostic :
    IEquatable<Diagnostic>
{
    public DiagnosticsLevel Level { get; }
    public string Message { get; }
    public Position Position { get; }
    public Uri? File { get; }
    public ImmutableArray<Diagnostic> SubErrors { get; }

#if DEBUG
    bool _isDebugged;
#endif

    Diagnostic(DiagnosticsLevel level, string message, Position position, Uri? file, bool _break, IEnumerable<Diagnostic?>? suberrors)
    {
        Level = level;
        Message = message;
        Position = position;
        File = file;
        SubErrors = suberrors is null ? ImmutableArray<Diagnostic>.Empty : suberrors.Where(v => v is not null).ToImmutableArray()!;

        if (_break)
        { Break(); }
    }

    public Diagnostic(DiagnosticsLevel level, string message, Position position, Uri? file, IEnumerable<Diagnostic?>? suberrors)
    {
        Level = level;
        Message = message;
        Position = position;
        File = file;
        SubErrors = suberrors is null ? ImmutableArray<Diagnostic>.Empty : suberrors.Where(v => v is not null).ToImmutableArray()!;

        if (level == DiagnosticsLevel.Error)
        { Break(); }
    }

    [DoesNotReturn]
    public void Throw() => throw new LanguageException(Message, Position, File);

    public static Diagnostic Internal(string message, IPositioned? position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, position?.Position ?? Position.UnknownPosition, file, true, suberrors);

    public static Diagnostic Critical(string message, IPositioned? position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, position?.Position ?? Position.UnknownPosition, file, true, suberrors);

    public static Diagnostic Error(string message, IPositioned position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, position.Position, file, true, suberrors);

    public static Diagnostic Warning(string message, IPositioned position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Warning, message, position.Position, file, false, suberrors);

    public static Diagnostic Information(string message, IPositioned position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Information, message, position.Position, file, false, suberrors);

    public static Diagnostic Hint(string message, IPositioned position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Hint, message, position.Position, file, false, suberrors);

    public static Diagnostic Internal(string message, Position position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, position, file, true, suberrors);

    public static Diagnostic Critical(string message, Position position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, position, file, true, suberrors);

    public static Diagnostic Error(string message, Position position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, position, file, true, suberrors);

    public static Diagnostic Warning(string message, Position position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Warning, message, position, file, false, suberrors);

    public static Diagnostic Information(string message, Position position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Information, message, position, file, false, suberrors);

    public static Diagnostic Hint(string message, Position position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Hint, message, position, file, false, suberrors);

    public static Diagnostic Internal(string message, Position? position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, position ?? Position.UnknownPosition, file, true, suberrors);

    public static Diagnostic Error(string message, Position? position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, position ?? Position.UnknownPosition, file, true, suberrors);

    public static Diagnostic Warning(string message, Position? position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Warning, message, position ?? Position.UnknownPosition, file, false, suberrors);

    public static Diagnostic Information(string message, Position? position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Information, message, position ?? Position.UnknownPosition, file, false, suberrors);

    public static Diagnostic Hint(string message, Position? position, Uri? file, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Hint, message, position ?? Position.UnknownPosition, file, false, suberrors);

    public static Diagnostic Internal(string message, ILocated location, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, location.Location.Position, location.Location.File, true, suberrors);

    public static Diagnostic Critical(string message, ILocated location, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, location.Location.Position, location.Location.File, true, suberrors);

    public static Diagnostic Error(string message, ILocated location, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Error, message, location.Location.Position, location.Location.File, true, suberrors);

    public static Diagnostic Warning(string message, ILocated location, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Warning, message, location.Location.Position, location.Location.File, false, suberrors);

    public static Diagnostic Information(string message, ILocated location, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Information, message, location.Location.Position, location.Location.File, false, suberrors);

    public static Diagnostic Hint(string message, ILocated location, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.Hint, message, location.Location.Position, location.Location.File, false, suberrors);

    public static Diagnostic OptimizationNotice(string message, ILocated location, params Diagnostic?[] suberrors)
        => new(DiagnosticsLevel.OptimizationNotice, message, location.Location.Position, location.Location.File, false, suberrors);

    public Diagnostic Break()
    {
#if DEBUG
        if (!_isDebugged)
        { Debugger.Break(); }
        _isDebugged = true;
#endif
        return this;
    }

    public (string SourceCode, string Arrows)? GetArrows(LanguageException.GetFileContent? getFileContent = null)
    {
        if (File == null) return null;
        if (!File.IsFile) return null;
        return LanguageException.GetArrows(Position, getFileContent?.Invoke(File) ?? System.IO.File.ReadAllText(File.AbsolutePath));
    }

    public override string ToString()
        => LanguageException.Format(Message, Position, File);

    public bool Equals([NotNullWhen(true)] Diagnostic? other)
    {
        if (other is null) return false;
        if (Message != other.Message) return false;
        if (Position != other.Position) return false;
        if (File != other.File) return false;
        return true;
    }
}