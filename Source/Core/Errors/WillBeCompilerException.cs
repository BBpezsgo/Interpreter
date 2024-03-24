namespace LanguageCore;

public class WillBeCompilerException
{
    public string Message { get; }

    public WillBeCompilerException(string message)
    {
        Message = message;
    }

    public CompilerException Instantiate(Position position, Uri? uri) => new(Message, position, uri);
    public CompilerException Instantiate(IPositioned? position, Uri? uri) => new(Message, position, uri);

    public Error InstantiateError(Position position, Uri? uri) => new(Message, position, uri);
    public Error InstantiateError(IPositioned? position, Uri? uri) => new(Message, position, uri);

    public Warning InstantiateWarning(Position position, Uri? uri) => new(Message, position, uri);
    public Warning InstantiateWarning(IPositioned? position, Uri? uri) => new(Message, position, uri);

    public override string ToString() => Message;
}
