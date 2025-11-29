using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public readonly struct TokenPair :
    IPositioned
{
    public Token Start { get; }
    public Token End { get; }

    public Position Position => new(Start, End);

    public TokenPair(Token start, Token end)
    {
        Start = start;
        End = end;
    }

    public static TokenPair CreateAnonymous(Position surround, string start, string end) => new(
        Token.CreateAnonymous(start, TokenType.Operator, surround.Before()),
        Token.CreateAnonymous(end, TokenType.Operator, surround.Before())
    );

    public static TokenPair CreateAnonymous(string start, string end) => new(
        Token.CreateAnonymous(start, TokenType.Operator),
        Token.CreateAnonymous(end, TokenType.Operator)
    );
}
