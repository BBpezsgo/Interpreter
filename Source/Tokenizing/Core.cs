namespace LanguageCore.Tokenizing;

public abstract partial class Tokenizer
{
    static readonly ImmutableArray<char> Bracelets = ['{', '}', '(', ')', '[', ']'];
    static readonly ImmutableArray<char> Operators = ['+', '-', '*', '/', '=', '<', '>', '!', '%', '^', '|', '&', '~'];
    static readonly ImmutableArray<string> DoubleOperators = ["++", "--", "<<", ">>", "&&", "||"];
    static readonly ImmutableArray<char> SimpleOperators = [';', ',', '#'];

    protected readonly List<Token> Tokens;
    protected readonly List<SimpleToken> UnicodeCharacters;
    protected readonly List<Warning> Warnings;
    protected readonly TokenizerSettings Settings;

    readonly PreparationToken CurrentToken;
    int CurrentColumn;
    int CurrentLine;
    char PreviousChar;
    string? SavedUnicode;

    protected Tokenizer(TokenizerSettings settings)
    {
        CurrentToken = new(default);
        CurrentColumn = 0;
        CurrentLine = 0;
        PreviousChar = default;

        Tokens = new();
        UnicodeCharacters = new();

        Warnings = new();

        Settings = settings;

        SavedUnicode = null;
    }

    Position GetCurrentPosition(int offsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(offsetTotal, offsetTotal + 1));

    void RefreshTokenPosition(int offsetTotal)
    {
        SinglePosition currentPosition = new(CurrentLine, CurrentColumn);
        Position position = new(
                new Range<SinglePosition>(CurrentToken.Position.Range.Start, currentPosition),
                new Range<int>(CurrentToken.Position.AbsoluteRange.Start, offsetTotal)
            );
        CurrentToken.Position = position;
    }

    protected static Token[] NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
    {
        List<Token> result = new(tokens.Count);

        foreach (Token token in tokens)
        {
            if (result.Count == 0)
            {
                result.Add(token);
                continue;
            }

            Token lastToken = result[^1];

            if (token.TokenType == TokenType.Whitespace &&
                lastToken.TokenType == TokenType.Whitespace)
            {
                result[^1] = lastToken + token;
                continue;
            }

            if (settings.JoinLinebreaks &&
                token.TokenType == TokenType.LineBreak &&
                lastToken.TokenType == TokenType.LineBreak)
            {
                result[^1] = lastToken + token;
                continue;
            }

            result.Add(token);
        }

        return result.ToArray();
    }
}
