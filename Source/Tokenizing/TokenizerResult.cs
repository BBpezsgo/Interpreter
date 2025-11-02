namespace LanguageCore.Tokenizing;

[ExcludeFromCodeCoverage]
public readonly struct TokenizerResult
{
    public static TokenizerResult Empty => new(
        ImmutableArray<Token>.Empty,
        ImmutableArray<SimpleToken>.Empty
    );

    public readonly ImmutableArray<Token> Tokens;
    public readonly ImmutableArray<SimpleToken> UnicodeCharacterTokens;

    public TokenizerResult(
        ImmutableArray<Token> tokens,
        ImmutableArray<SimpleToken> unicodeCharacterTokens)
    {
        Tokens = tokens;
        UnicodeCharacterTokens = unicodeCharacterTokens;
    }
}
