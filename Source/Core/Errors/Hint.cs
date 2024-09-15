namespace LanguageCore;

[ExcludeFromCodeCoverage]
public sealed class Hint : NotExceptionBut
{
    public Hint(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public Hint(string message, IPositioned? position, Uri? uri) : this(message, position?.Position ?? Position.UnknownPosition, uri) { }
}
