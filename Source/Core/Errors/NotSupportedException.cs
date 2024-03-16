namespace LanguageCore;

public sealed class NotSupportedException : CompilerException
{
    public NotSupportedException(string message, Position position, Uri? file) : base(message, position, file) { }
    public NotSupportedException(string message, IPositioned? position, Uri? uri) : base(message, position, uri) { }
}
