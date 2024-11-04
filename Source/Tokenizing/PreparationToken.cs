namespace LanguageCore.Tokenizing;

sealed class PreparationToken :
    IPositioned,
    IDuplicatable<PreparationToken>,
    IEquatable<string>
{
    public PreparationTokenType TokenType { get; set; }
    public StringBuilder Content { get; }
    public Position Position { get; set; }

    public PreparationToken(Position position)
    {
        Position = position;
        TokenType = PreparationTokenType.Whitespace;
        Content = new StringBuilder();
    }

    PreparationToken(Position position, PreparationTokenType tokenType, string content)
    {
        Position = position;
        TokenType = tokenType;
        Content = new StringBuilder(content);
    }

    public override string ToString() => Content.ToString();
    public bool Equals(string? other) => Content.Equals(other);
    public bool EndsWith(char v) => Content.Length != 0 && Content[^1] == v;
    public bool Contains(char v) => Content.Contains(v);

    /// <exception cref="InternalExceptionWithoutContext"/>
    public Token Instantiate() => new(this);

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public (PreparationToken?, PreparationToken?) Slice(int at)
    {
#if NET_STANDARD
        if (at < 0 || at >= Content.Length) throw new ArgumentOutOfRangeException(nameof(at));
#else
        ArgumentOutOfRangeException.ThrowIfNegative(at);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(at, Content.Length);
#endif

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
