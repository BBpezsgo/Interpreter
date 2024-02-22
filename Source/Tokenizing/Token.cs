using System;
using System.Diagnostics;

namespace LanguageCore.Tokenizing
{
    public enum TokenType
    {
        Whitespace,
        LineBreak,

        Identifier,

        LiteralNumber,
        LiteralHex,
        LiteralBinary,
        LiteralString,
        LiteralCharacter,
        LiteralFloat,

        Operator,

        Comment,
        CommentMultiline,
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class Token :
        IPositioned,
        IEquatable<Token>,
        IEquatable<string>,
        IDuplicatable<Token>,
        System.Numerics.IAdditionOperators<Token, Token, Token>
    {
        readonly Position position;

        public TokenAnalyzedType AnalyzedType;

        public readonly TokenType TokenType;
        public readonly bool IsAnonymous;

        public readonly string Content;

        public static Token Empty => new(TokenType.Whitespace, string.Empty, true, new Position(new Range<SinglePosition>(new SinglePosition(0, 0), new SinglePosition(0, 0)), new Range<int>(0, 0)));

        public Position Position => position;

        public Token(TokenType type, string content, bool isAnonymous, Position position) : base()
        {
            TokenType = type;
            AnalyzedType = TokenAnalyzedType.None;
            Content = content;
            IsAnonymous = isAnonymous;
            this.position = position;
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
            => Token.CreateAnonymous(v.ToString(System.Globalization.CultureInfo.InvariantCulture), TokenType.LiteralNumber);
        public static explicit operator Token(float v)
            => Token.CreateAnonymous(v.ToString(System.Globalization.CultureInfo.InvariantCulture), TokenType.LiteralFloat);
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

        public Token Duplicate() => new(TokenType, new string(Content), IsAnonymous, Position)
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
            if (string.IsNullOrEmpty(Content))
            { return (null, null); }

            if (at < 0)
            { throw new ArgumentOutOfRangeException(nameof(at), at, $"Slice location is less than zero"); }

            if (at > Content.Length)
            { throw new ArgumentOutOfRangeException(nameof(at), at, $"Slice location is less than zero"); }

            Token left;
            Token right;

            if (Content.Length == 1)
            {
                left = Duplicate();
                return (left, null);
            }

            (Position leftPosition, Position rightPosition) = position.Slice(at);

            left = new Token(TokenType, Content[..at], IsAnonymous, leftPosition);
            right = new Token(TokenType, Content[at..], IsAnonymous, rightPosition);

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
                a.position.Union(b.position));
        }
    }
}
