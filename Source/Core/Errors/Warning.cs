namespace LanguageCore;

[ExcludeFromCodeCoverage]
public sealed class Warning : NotExceptionBut
{
    public Warning(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public Warning(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
}
