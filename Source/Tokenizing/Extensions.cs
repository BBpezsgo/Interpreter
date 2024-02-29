namespace LanguageCore.Tokenizing;

public static class Extensions
{
    public static Token? GetTokenAt(this IEnumerable<Token> tokens, SinglePosition position)
    {
        foreach (Token token in tokens)
        {
            if (token.Position.Range.Contains(position))
            { return token; }
        }

        return null;
    }

    public static Token? GetTokenAt(this IEnumerable<Token> tokens, int absolutePosition)
    {
        foreach (Token token in tokens)
        {
            if (token.Position.AbsoluteRange.Contains(absolutePosition))
            { return token; }
        }

        return null;
    }
}
