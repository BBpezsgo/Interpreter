namespace LanguageCore.Tokenizing;

[ExcludeFromCodeCoverage]
public struct TokenizerSettings
{
    /// <summary> The tokenizer will produce <see cref="TokenType.Whitespace"/> tokens </summary>
    public bool TokenizeWhitespaces;
    /// <summary> The tokenizer will produce <see cref="TokenType.LineBreak"/> tokens </summary>
    public bool DistinguishBetweenSpacesAndNewlines;
    public bool JoinLinebreaks;
    /// <summary> The tokenizer will produce <see cref="TokenType.Comment"/> and <see cref="TokenType.CommentMultiline"/> tokens </summary>
    public bool TokenizeComments;

    public static TokenizerSettings Default => new()
    {
        TokenizeWhitespaces = false,
        DistinguishBetweenSpacesAndNewlines = false,
        JoinLinebreaks = true,
        TokenizeComments = false,
    };

    public TokenizerSettings(TokenizerSettings other)
    {
        TokenizeWhitespaces = other.TokenizeWhitespaces;
        DistinguishBetweenSpacesAndNewlines = other.DistinguishBetweenSpacesAndNewlines;
        JoinLinebreaks = other.JoinLinebreaks;
        TokenizeComments = other.TokenizeComments;
    }
}
