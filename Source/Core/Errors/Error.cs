namespace LanguageCore;

public sealed class Error : NotExceptionBut
{
#if DEBUG
    bool IsDebugged;
#endif

    public Error(string message, Position position, Uri? uri, bool @break = true) : base(message, position, uri)
    {
        if (@break)
        { Break(); }
    }
    public Error(string message, Position? position, Uri? uri, bool @break = true) : this(message, position ?? Position.UnknownPosition, uri, @break) { }
    public Error(string message, IPositioned? position, Uri? uri, bool @break = true) : this(message, position?.Position ?? Position.UnknownPosition, uri, @break) { }

    public Error Break()
    {
#if DEBUG
        if (!IsDebugged)
        { Debugger.Break(); }
        IsDebugged = true;
#endif
        return this;
    }

    public LanguageException ToException() => new(this);
}
