namespace LanguageCore.Runtime;

public sealed class UserException : RuntimeException
{
    public UserException(string message) : base(message) { }
}
