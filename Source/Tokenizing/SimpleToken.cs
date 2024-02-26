using System;

namespace LanguageCore.Tokenizing;

public readonly struct SimpleToken : IPositioned, IEquatable<SimpleToken>
{
    readonly string _content;
    readonly Position _position;

    public Position Position => _position;
    public string Content => _content;

    public SimpleToken(string content, Position position)
    {
        _content = content;
        _position = position;
    }

    public override string ToString() => Content;
    public override bool Equals(object? obj) => obj is SimpleToken token && Equals(token);
    public bool Equals(SimpleToken other) => _content.Equals(other._content) && _position.Equals(other._position);
    public override int GetHashCode() => HashCode.Combine(_content, _position);

    public static bool operator ==(SimpleToken left, SimpleToken right) => left.Equals(right);
    public static bool operator !=(SimpleToken left, SimpleToken right) => !left.Equals(right);
}
