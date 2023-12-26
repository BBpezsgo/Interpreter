using System;
using System.Text;

namespace LanguageCore.Tokenizing
{
    public enum PreparationTokenType
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

        STRING_EscapeSequence,
        STRING_UnicodeCharacter,

        CHAR_EscapeSequence,
        CHAR_UnicodeCharacter,

        POTENTIAL_COMMENT,
        POTENTIAL_END_MULTILINE_COMMENT,
        POTENTIAL_FLOAT,
    }

    sealed class PreparationToken :
        IPositioned,
        IDuplicatable<PreparationToken>
    {
        Position position;
        public PreparationTokenType TokenType;
        public readonly StringBuilder Content;

        public ref Position Position => ref position;
        Position IPositioned.Position => position;

        public PreparationToken(Position position)
        {
            this.position = position;
            this.TokenType = PreparationTokenType.Whitespace;
            this.Content = new StringBuilder();
        }

        PreparationToken(Position position, PreparationTokenType tokenType, string content)
        {
            this.position = position;
            this.TokenType = tokenType;
            this.Content = new StringBuilder(content);
        }

        public override string ToString() => Content.ToString();

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
            if (Content.Length == 0)
            { return (null, null); }

            if (at < 0)
            { throw new ArgumentOutOfRangeException(nameof(at), at, $"Slice location is less than zero"); }

            if (at > Content.Length)
            { throw new ArgumentOutOfRangeException(nameof(at), at, $"Slice location is less than zero"); }

            PreparationToken left;
            PreparationToken right;

            if (Content.Length == 1)
            {
                left = Duplicate();
                return (left, null);
            }

            (Position leftPosition, Position rightPosition) = position.Slice(at);

            string content = Content.ToString();

            left = new PreparationToken(leftPosition, TokenType, content[..at]);
            right = new PreparationToken(rightPosition, TokenType, content[at..]);

            return (left, right);
        }

        public PreparationToken Duplicate() => new(position, TokenType, Content.ToString());
    }
}
