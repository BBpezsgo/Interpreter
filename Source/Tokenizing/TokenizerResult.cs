namespace LanguageCore.Tokenizing;

public readonly struct TokenizerResult
{
    public readonly ImmutableArray<Token> Tokens;
    public readonly ImmutableArray<SimpleToken> UnicodeCharacterTokens;
    public readonly ImmutableArray<Warning> Warnings;

    public TokenizerResult(
        IEnumerable<Token> tokens,
        IEnumerable<SimpleToken> unicodeCharacterTokens,
        IEnumerable<Warning> warnings)
    {
        Tokens = tokens.ToImmutableArray();
        UnicodeCharacterTokens = unicodeCharacterTokens.ToImmutableArray();
        Warnings = warnings.ToImmutableArray();
    }

    public static TokenizerResult Empty => new(
        Enumerable.Empty<Token>(),
        Enumerable.Empty<SimpleToken>(),
        Enumerable.Empty<Warning>());
}
