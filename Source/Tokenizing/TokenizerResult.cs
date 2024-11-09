namespace LanguageCore.Tokenizing;

[ExcludeFromCodeCoverage]
public readonly struct TokenizerResult
{
    public static TokenizerResult Empty => new(
        Enumerable.Empty<Token>(),
        Enumerable.Empty<SimpleToken>());

    public readonly ImmutableArray<Token> Tokens;
    public readonly ImmutableArray<SimpleToken> UnicodeCharacterTokens;

    public TokenizerResult(
        IEnumerable<Token> tokens,
        IEnumerable<SimpleToken> unicodeCharacterTokens)
    {
        Tokens = tokens.ToImmutableArray();
        UnicodeCharacterTokens = unicodeCharacterTokens.ToImmutableArray();
    }
}
