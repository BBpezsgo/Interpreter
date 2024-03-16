namespace LanguageCore;

public class InternalException : LanguageException
{
    public InternalException() : base(string.Empty, Position.UnknownPosition, null) { }
    public InternalException(string message) : base(message, Position.UnknownPosition, null) { }
    public InternalException(string message, Uri? uri) : base(message, Position.UnknownPosition, uri) { }
    public InternalException(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public InternalException(string message, IPositioned position, Uri? uri) : base(message, position.Position, uri) { }
}
