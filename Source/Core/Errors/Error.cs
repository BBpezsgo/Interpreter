namespace LanguageCore;

public sealed class Error : NotExceptionBut
{
    public Error(string message, Position position, Uri? uri) : base(message, position, uri)
    {
#if DEBUG
        Debugger.Break();
#endif
    }

    public Error(string message, Position? position, Uri? uri) : this(message, position ?? Position.UnknownPosition, uri) { }
    public Error(string message, IPositioned? position, Uri? uri) : this(message, position?.Position ?? Position.UnknownPosition, uri) { }

    public LanguageException ToException() => new(this);
}
