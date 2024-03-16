namespace LanguageCore;

public class CompilerException : LanguageException
{
    public CompilerException(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public CompilerException(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
}
