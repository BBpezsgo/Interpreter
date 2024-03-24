namespace LanguageCore.Tokenizing;

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

    PreprocessIdentifier,
    PreprocessArgument,
    PreprocessSkipped,

    Comment,
    CommentMultiline,
}
