namespace LanguageCore;

[ExcludeFromCodeCoverage]
public sealed class Information : NotExceptionBut
{
    public Information(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public Information(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
}
