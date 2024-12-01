namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public sealed class UserException : RuntimeException
{
    public UserException(string message) : base(message) { }
}
