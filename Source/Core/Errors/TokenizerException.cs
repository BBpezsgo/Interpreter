namespace LanguageCore.Tokenizing;

public sealed class TokenizerException : LanguageException
{
    public TokenizerException(string message, Position position, Uri? file) : base(message, position, file) { }
}
