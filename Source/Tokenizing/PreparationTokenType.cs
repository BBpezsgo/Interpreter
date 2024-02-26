namespace LanguageCore.Tokenizing;

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
