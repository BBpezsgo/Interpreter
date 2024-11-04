namespace LanguageCore;

[ExcludeFromCodeCoverage]
public sealed class NotSupportedException : LanguageException
{
    public NotSupportedException(string message, Position position, Uri file) : base(message, position, file) { }
    public NotSupportedException(string message, IPositioned position, Uri uri) : base(message, position.Position, uri) { }
    public NotSupportedException(string message, ILocated location) : base(message, location.Location.Position, location.Location.File) { }
}
