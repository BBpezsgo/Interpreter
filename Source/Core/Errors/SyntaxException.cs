namespace LanguageCore.Parser;

public sealed class SyntaxException : LanguageException
{
    public SyntaxException(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public SyntaxException(string message, Position? position, Uri? uri) : base(message, position ?? Position.UnknownPosition, uri) { }
    public SyntaxException(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
}
