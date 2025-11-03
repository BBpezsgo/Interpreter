using System.Runtime.InteropServices;

namespace LanguageCore.Tokenizing;

public partial class Tokenizer
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
        "&&", "||",
        "=>"
    );

    static readonly ImmutableArray<char> SimpleOperators = ImmutableArray.Create(
        ';',
        ',',
        ':'
    );

    readonly List<Token> Tokens;
    readonly List<SimpleToken> UnicodeCharacters;
    readonly DiagnosticsCollection Diagnostics;
    readonly TokenizerSettings Settings;
    readonly Uri? File;

    readonly PreparationToken CurrentToken;
    int CurrentColumn;
    int CurrentLine;
    string? SavedUnicode;

    readonly string Text;

    public static TokenizerResult Tokenize(string text, DiagnosticsCollection diagnostics, ImmutableHashSet<string>? preprocessorVariables = null, Uri? file = null, TokenizerSettings? settings = null)
        => new Tokenizer(text, settings ?? TokenizerSettings.Default, file, preprocessorVariables, diagnostics)
        .TokenizeInternal();

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _marker = new("LanguageCore.Parser");
#endif
    TokenizerResult TokenizeInternal()
    {
#if UNITY
        using Unity.Profiling.ProfilerMarker.AutoScope _1 = _marker.Auto();
#endif
        for (int offsetTotal = 0; offsetTotal < Text.Length; offsetTotal++)
        {
            ProcessCharacter(Text[offsetTotal], offsetTotal);
        }

        EndToken(Text.Length);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToImmutableArray(), UnicodeCharacters.ToImmutableArray());
    }

    public Tokenizer(string text, TokenizerSettings settings, Uri? file, ImmutableHashSet<string>? preprocessorVariables, DiagnosticsCollection diagnostics)
    {
        Text = text;

        CurrentToken = new(default);
        CurrentColumn = 0;
        CurrentLine = 0;

        Tokens = new();
        UnicodeCharacters = new();
        Diagnostics = diagnostics;

        Settings = settings;
        SavedUnicode = null;
        File = file;

        PreprocessorVariables = preprocessorVariables ?? ImmutableHashSet<string>.Empty;
        PreprocessorConditions = new Stack<PreprocessThing>();
    }

    SinglePosition CurrentSinglePosition => new(CurrentLine, CurrentColumn);
    Position GetCurrentPosition(int offsetTotal) => new(new Range<SinglePosition>(CurrentSinglePosition, new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(offsetTotal, offsetTotal + 1));

    void RefreshTokenPosition(int offsetTotal)
    {
        CurrentToken.Position = new Position(
            new Range<SinglePosition>(CurrentToken.Position.Range.Start, CurrentSinglePosition),
            new Range<int>(CurrentToken.Position.AbsoluteRange.Start, offsetTotal)
        );
    }

    static ReadOnlySpan<Token> NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
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
