namespace LanguageCore.Tokenizing;

[ExcludeFromCodeCoverage]
public sealed class TokenizerException : LanguageException
{
    public TokenizerException(string message, Position position, Uri? file) : base(message, position, file) { }
}
