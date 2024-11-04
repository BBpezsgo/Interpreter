
namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class InternalException : LanguageException
{
    public InternalException(string message, Position position, Uri uri) : base(message, position, uri) { }
    public InternalException(string message, IPositioned position, Uri uri) : base(message, position.Position, uri) { }
    public InternalException(string message, ILocated location) : base(message, location.Location.Position, location.Location.File) { }
}
