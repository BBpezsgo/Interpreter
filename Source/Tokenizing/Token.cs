namespace LanguageCore.Tokenizing;

public class Token :
    IPositioned,
    IEquatable<Token>,
    IEquatable<string>,
    IDuplicatable<Token>
{
    public static Token Empty => new(TokenType.Whitespace, string.Empty, true, Position.Zero);

    public TokenType TokenType { get; }
    public bool IsAnonymous { get; }
    public string Content { get; }
    public Position Position { get; }
    public TokenAnalyzedType AnalyzedType { get; set; }

    Token(TokenType type, string content, bool isAnonymous, Position position)
    {
        TokenType = type;
        Content = content;
        IsAnonymous = isAnonymous;
        Position = position;
        AnalyzedType = TokenAnalyzedType.None;
    }

    internal Token(PreparationToken preparationToken) : this(preparationToken.TokenType switch
    {
        PreparationTokenType.Whitespace => TokenType.Whitespace,
        PreparationTokenType.LineBreak => TokenType.LineBreak,
        PreparationTokenType.Identifier => TokenType.Identifier,
        PreparationTokenType.LiteralNumber => TokenType.LiteralNumber,
        PreparationTokenType.LiteralHex => TokenType.LiteralHex,
        PreparationTokenType.LiteralBinary => TokenType.LiteralBinary,
        PreparationTokenType.LiteralString => TokenType.LiteralString,
        PreparationTokenType.LiteralCharacter => TokenType.LiteralCharacter,
        PreparationTokenType.LiteralFloat => TokenType.LiteralFloat,
        PreparationTokenType.Operator => TokenType.Operator,
        PreparationTokenType.Comment => TokenType.Comment,
        PreparationTokenType.CommentMultiline => TokenType.CommentMultiline,
        PreparationTokenType.PREPROCESS_Identifier => TokenType.PreprocessIdentifier,
        PreparationTokenType.PREPROCESS_Argument => TokenType.PreprocessArgument,
        PreparationTokenType.PREPROCESS_Skipped => TokenType.PreprocessSkipped,
        _ => throw new InternalExceptionWithoutContext($"Token \"{preparationToken}\" isn't finished (type is \"{preparationToken.TokenType}\")"),
    }, preparationToken.Content.ToString(), false, preparationToken.Position)
    { }

    public override string ToString() => Content;
    public string ToOriginalString() => TokenType switch
    {
        TokenType.LiteralString => $"\"{Content}\"",
        TokenType.LiteralCharacter => $"\'{Content}\'",
        TokenType.Comment => $"//{Content}",
        TokenType.CommentMultiline => $"/*{Content}*/",
        _ => Content,
    };

    #region CreateAnonymous

    public static Token CreateAnonymous(string content, TokenType type = TokenType.Identifier)
        => new(type, content, true, Position.UnknownPosition);

    public static Token CreateAnonymous(string content, TokenType type, Position position)
        => new(type, content, true, position);

    #endregion

    public override bool Equals(object? obj) => obj is Token other && Equals(other);
    public bool Equals(Token? other) =>
        other is not null &&
        Position.Equals(other.Position) &&
        TokenType == other.TokenType &&
        string.Equals(Content, other.Content) &&
        IsAnonymous == other.IsAnonymous;
    public bool Equals(string? other) =>
        other is not null &&
        string.Equals(Content, other);

    public override int GetHashCode() => HashCode.Combine(Position, TokenType, Content);

    public Token Duplicate() => new(TokenType, Content, IsAnonymous, Position)
    { AnalyzedType = AnalyzedType };

    public (Token?, Token?) Slice(int at)
    {
#if NET_STANDARD
        if (at < 0 || at > Content.Length) throw new ArgumentOutOfRangeException(nameof(at));
#else
        ArgumentOutOfRangeException.ThrowIfNegative(at);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(at, Content.Length);
#endif

        if (Content.Length == 0)
        { return (null, null); }

        if (Content.Length == 1)
        { return (Duplicate(), null); }

        (Position leftPosition, Position rightPosition) = Position.Cut(at);

        Token left = new(TokenType, Content[..at], IsAnonymous, leftPosition) { AnalyzedType = AnalyzedType };
        Token right = new(TokenType, Content[at..], IsAnonymous, rightPosition) { AnalyzedType = AnalyzedType };

        return (left, right);
    }

    public Token this[Range range]
    {
        get
        {
            (int start, int length) = range.GetOffsetAndLength(Content.Length);

            Position position = new(
                ((Position.Range.Start.Character + start, Position.Range.Start.Line), (Position.Range.Start.Character + start + length, Position.Range.Start.Line)),
                (Position.AbsoluteRange.Start + start, Position.AbsoluteRange.Start + start + length)
            );

            return new Token(TokenType, Content[range], IsAnonymous, position) { AnalyzedType = AnalyzedType };
        }
    }

    public static Token operator +(Token a, Token b)
    {
        if (a.TokenType != b.TokenType)
        { throw new Exception(); }

        if (a.IsAnonymous != b.IsAnonymous)
        { throw new Exception(); }

        return new Token(
            a.TokenType,
            a.Content + b.Content,
            a.IsAnonymous,
            a.Position.Union(b.Position));
    }
}
