namespace LanguageCore.Tokenizing;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class Token :
    IPositioned,
    IEquatable<Token>,
    IEquatable<string>,
    IDuplicatable<Token>,
    IAdditionOperators<Token, Token, Token>
{
    public static Token Empty => new(TokenType.Whitespace, string.Empty, true, Position.Zero);

    public TokenType TokenType { get; }
    public bool IsAnonymous { get; }
    public string Content { get; }
    public Position Position { get; }
    public TokenAnalyzedType AnalyzedType { get; set; }

    public Token(TokenType type, string content, bool isAnonymous, Position position)
    {
        TokenType = type;
        Content = content;
        IsAnonymous = isAnonymous;
        Position = position;
        AnalyzedType = TokenAnalyzedType.None;
    }

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

    public static explicit operator Token(string v)
        => Token.CreateAnonymous(v);
    public static explicit operator Token(int v)
        => Token.CreateAnonymous(v.ToString(CultureInfo.InvariantCulture), TokenType.LiteralNumber);
    public static explicit operator Token(float v)
        => Token.CreateAnonymous(v.ToString(CultureInfo.InvariantCulture), TokenType.LiteralFloat);
    public static explicit operator Token(char v)
        => Token.CreateAnonymous(char.ToString(v), TokenType.LiteralCharacter);

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

    string GetDebuggerDisplay() => TokenType switch
    {
        TokenType.LiteralString => $"\"{Content.Escape()}\"",
        TokenType.LiteralCharacter => $"\'{Content.Escape()}\'",
        _ => Content.Escape(),
    };

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public (Token?, Token?) Slice(int at)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(at);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(at, Content.Length);

        if (Content.Length == 0)
        { return (null, null); }

        if (Content.Length == 1)
        { return (Duplicate(), null); }

        (Position leftPosition, Position rightPosition) = Position.Slice(at);

        Token left = new(TokenType, Content[..at], IsAnonymous, leftPosition);
        Token right = new(TokenType, Content[at..], IsAnonymous, rightPosition);

        return (left, right);
    }

    /// <exception cref="Exception"/>
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
