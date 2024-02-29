namespace LanguageCore.Tokenizing;

sealed class PreparationToken :
    IPositioned,
    IDuplicatable<PreparationToken>
{
    public PreparationTokenType TokenType { get; set; }
    public StringBuilder Content { get; }
    public Position Position { get; set; }

    public PreparationToken(Position position)
    {
        this.Position = position;
        this.TokenType = PreparationTokenType.Whitespace;
        this.Content = new StringBuilder();
    }

    PreparationToken(Position position, PreparationTokenType tokenType, string content)
    {
        this.Position = position;
        this.TokenType = tokenType;
        this.Content = new StringBuilder(content);
    }

    public override string ToString() => Content.ToString();

    /// <exception cref="InternalException"/>
    public Token Instantiate() => new(TokenType switch
    {
        PreparationTokenType.Whitespace => Tokenizing.TokenType.Whitespace,
        PreparationTokenType.LineBreak => Tokenizing.TokenType.LineBreak,
        PreparationTokenType.Identifier => Tokenizing.TokenType.Identifier,
        PreparationTokenType.LiteralNumber => Tokenizing.TokenType.LiteralNumber,
        PreparationTokenType.LiteralHex => Tokenizing.TokenType.LiteralHex,
        PreparationTokenType.LiteralBinary => Tokenizing.TokenType.LiteralBinary,
        PreparationTokenType.LiteralString => Tokenizing.TokenType.LiteralString,
        PreparationTokenType.LiteralCharacter => Tokenizing.TokenType.LiteralCharacter,
        PreparationTokenType.LiteralFloat => Tokenizing.TokenType.LiteralFloat,
        PreparationTokenType.Operator => Tokenizing.TokenType.Operator,
        PreparationTokenType.Comment => Tokenizing.TokenType.Comment,
        PreparationTokenType.CommentMultiline => Tokenizing.TokenType.CommentMultiline,
        _ => throw new InternalException($"Token {this} isn't finished (type is {TokenType})"),
    }, Content.ToString(), false, Position);

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public (PreparationToken?, PreparationToken?) Slice(int at)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(at);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(at, Content.Length);

        if (Content.Length == 0)
        { return (null, null); }

        PreparationToken left;
        PreparationToken right;

        if (Content.Length == 1)
        {
            left = Duplicate();
            return (left, null);
        }

        (Position leftPosition, Position rightPosition) = Position.Slice(at);

        string content = Content.ToString();

        left = new PreparationToken(leftPosition, TokenType, content[..at]);
        right = new PreparationToken(rightPosition, TokenType, content[at..]);

        return (left, right);
    }

    public PreparationToken Duplicate() => new(Position, TokenType, Content.ToString());
}
