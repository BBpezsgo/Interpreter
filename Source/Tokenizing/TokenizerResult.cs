using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.Tokenizing;

public readonly struct TokenizerResult
{
    public readonly Token[] Tokens;
    public readonly SimpleToken[] UnicodeCharacterTokens;
    public readonly Warning[] Warnings;

    public TokenizerResult(
        IEnumerable<Token> tokens,
        IEnumerable<SimpleToken> unicodeCharacterTokens,
        IEnumerable<Warning> warnings)
    {
        Tokens = tokens.ToArray();
        UnicodeCharacterTokens = unicodeCharacterTokens.ToArray();
        Warnings = warnings.ToArray();
    }

    public static TokenizerResult Empty => new(
        Enumerable.Empty<Token>(),
        Enumerable.Empty<SimpleToken>(),
        Enumerable.Empty<Warning>());

    public static implicit operator Token[](TokenizerResult result) => result.Tokens;
}
