namespace LanguageCore;

[ExcludeFromCodeCoverage]
public sealed class LanguageError : LanguageException
{
#if DEBUG
    bool IsDebugged;
#endif

    public LanguageError(string message, Position position, Uri? uri, bool @break = true) : base(message, position, uri)
    {
        if (@break)
        { Break(); }
    }
    public LanguageError(string message, Position? position, Uri? uri, bool @break = true) : this(message, position ?? Position.UnknownPosition, uri, @break) { }
    public LanguageError(string message, IPositioned? position, Uri? uri, bool @break = true) : this(message, position?.Position ?? Position.UnknownPosition, uri, @break) { }

    public LanguageError Break()
    {
#if DEBUG
        if (!IsDebugged)
        { Debugger.Break(); }
        IsDebugged = true;
#endif
        return this;
    }
}
