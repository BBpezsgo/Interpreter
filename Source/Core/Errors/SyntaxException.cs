namespace LanguageCore.Parser;

[ExcludeFromCodeCoverage]
public sealed class SyntaxException : LanguageException
{
    public SyntaxException(string message, Position position, Uri file) : base(message, position, file) { }
    public SyntaxException(string message, IPositioned position, Uri file) : base(message, position.Position, file) { }
}
