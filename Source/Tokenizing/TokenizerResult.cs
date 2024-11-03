namespace LanguageCore.Tokenizing;

[ExcludeFromCodeCoverage]
public readonly struct TokenizerResult
{
    public static TokenizerResult Empty => new(
        Enumerable.Empty<Token>(),
        Enumerable.Empty<SimpleToken>(),
        Enumerable.Empty<Diagnostic>());

    public readonly ImmutableArray<Token> Tokens;
    public readonly ImmutableArray<SimpleToken> UnicodeCharacterTokens;
    public readonly ImmutableArray<Diagnostic> Diagnostics;

    public TokenizerResult(
        IEnumerable<Token> tokens,
        IEnumerable<SimpleToken> unicodeCharacterTokens,
        IEnumerable<Diagnostic> diagnostics)
    {
        Tokens = tokens.ToImmutableArray();
        UnicodeCharacterTokens = unicodeCharacterTokens.ToImmutableArray();
        Diagnostics = diagnostics.ToImmutableArray();
    }
}
