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

    sealed class PreparationToken : IThingWithPosition
    {
        Position position;
        public PreparationTokenType TokenType;
        public readonly StringBuilder Content;

        public ref Position Position => ref position;
        Position IThingWithPosition.Position => position;

        public PreparationToken(Position position)
        {
            this.position = position;
            this.TokenType = PreparationTokenType.Whitespace;
            this.Content = new StringBuilder();
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
    }
}
