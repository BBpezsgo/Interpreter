using System.Runtime.InteropServices;

namespace LanguageCore.Tokenizing;

public abstract partial class Tokenizer
{
    static readonly ImmutableArray<char> Bracelets = ImmutableArray.Create(
        '{', '}',
        '(', ')',
        '[', ']'
    );

    static readonly ImmutableArray<char> Operators = ImmutableArray.Create(
        '+', '-',
        '*', '/',
        '=', '<', '>',
        '!', '%',
        '^', '|', '&', '~'
    );

    static readonly ImmutableArray<string> DoubleOperators = ImmutableArray.Create(
        "++", "--",
        "<<", ">>",
        "&&", "||"
    );

    static readonly ImmutableArray<char> SimpleOperators = ImmutableArray.Create(
        ';',
        ','
    );

    protected readonly List<Token> Tokens;
    protected readonly List<SimpleToken> UnicodeCharacters;
    protected readonly DiagnosticsCollection Diagnostics;
    protected readonly TokenizerSettings Settings;
    protected readonly Uri? File;

    readonly PreparationToken CurrentToken;
    int CurrentColumn;
    int CurrentLine;
    string? SavedUnicode;

    protected Tokenizer(TokenizerSettings settings, Uri? file, IEnumerable<string>? preprocessorVariables, DiagnosticsCollection diagnostics)
    {
        CurrentToken = new(default);
        CurrentColumn = 0;
        CurrentLine = 0;

        Tokens = new();
        UnicodeCharacters = new();
        Diagnostics = diagnostics;

        Settings = settings;
        SavedUnicode = null;
        File = file;

        PreprocessorVariables = preprocessorVariables is null ? new HashSet<string>() : new HashSet<string>(preprocessorVariables);
        PreprocessorConditions = new Stack<PreprocessThing>();
    }

    protected SinglePosition CurrentSinglePosition => new(CurrentLine, CurrentColumn);
    protected Position GetCurrentPosition(int offsetTotal) => new(new Range<SinglePosition>(CurrentSinglePosition, new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(offsetTotal, offsetTotal + 1));

    void RefreshTokenPosition(int offsetTotal)
    {
        CurrentToken.Position = new Position(
            new Range<SinglePosition>(CurrentToken.Position.Range.Start, CurrentSinglePosition),
            new Range<int>(CurrentToken.Position.AbsoluteRange.Start, offsetTotal)
        );
    }

    protected static ReadOnlySpan<Token> NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
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

        return CollectionsMarshal.AsSpan(result);
    }
}
