
namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class DiagnosticWithoutContext :
    IEquatable<DiagnosticWithoutContext>
{
    public DiagnosticsLevel Level { get; }
    public string Message { get; }

#if DEBUG
    bool _isDebugged;
#endif

    DiagnosticWithoutContext(DiagnosticsLevel level, string message, bool @break)
    {
        Level = level;
        Message = message;

        if (@break)
        { Break(); }
    }

    public DiagnosticWithoutContext(DiagnosticsLevel level, string message)
    {
        Level = level;
        Message = message;

        if (level == DiagnosticsLevel.Error)
        { Break(); }
    }

    [DoesNotReturn]
    public void Throw() => throw new LanguageExceptionWithoutContext(Message);

    public static DiagnosticWithoutContext Critical(string message, bool @break = true)
        => new(DiagnosticsLevel.Error, message, @break);

    public static DiagnosticWithoutContext Error(string message, bool @break = true)
        => new(DiagnosticsLevel.Error, message, @break);

    public static DiagnosticWithoutContext Warning(string message, bool @break = true)
        => new(DiagnosticsLevel.Warning, message, @break);

    public static DiagnosticWithoutContext Information(string message, bool @break = true)
        => new(DiagnosticsLevel.Information, message, @break);

    public static DiagnosticWithoutContext Hint(string message, bool @break = true)
        => new(DiagnosticsLevel.Hint, message, @break);

    public DiagnosticWithoutContext Break()
    {
#if DEBUG
        if (!_isDebugged)
        { Debugger.Break(); }
        _isDebugged = true;
#endif
        return this;
    }

    public override string ToString() => Message;

    public bool Equals([NotNullWhen(true)] DiagnosticWithoutContext? other)
    {
        if (other is null) return false;
        if (Message != other.Message) return false;
        return true;
    }
}
