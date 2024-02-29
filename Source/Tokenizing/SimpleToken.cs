namespace LanguageCore.Tokenizing;

public readonly struct SimpleToken :
    IPositioned,
    IEquatable<SimpleToken>,
    IEqualityOperators<SimpleToken, SimpleToken, bool>
{
    public Position Position { get; }
    public string Content { get; }

    public SimpleToken(string content, Position position)
    {
        Content = content;
        Position = position;
    }

    public override string ToString() => Content;
    public override bool Equals(object? obj) => obj is SimpleToken token && Equals(token);
    public bool Equals(SimpleToken other) => Content.Equals(other.Content) && Position.Equals(other.Position);
    public override int GetHashCode() => HashCode.Combine(Content, Position);

    public static bool operator ==(SimpleToken left, SimpleToken right) => left.Equals(right);
    public static bool operator !=(SimpleToken left, SimpleToken right) => !left.Equals(right);
}
