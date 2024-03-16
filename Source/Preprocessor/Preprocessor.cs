using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Tokenizing;

namespace LanguageCore.Preprocessor;

enum PreprocessorState
{
    Text,
    DirectiveIdentifier,
    DirectiveParameter,
}

public class Directive : IPositioned
{
    public Token Identifier { get; }
    public Token Parameter { get; }
    public Position Position => new(Identifier, Parameter);

    public Directive(Token identifier, Token parameter)
    {
        Identifier = identifier;
        Parameter = parameter;
    }
}

internal class PreparationDirective
{
    public PreparationToken Identifier { get; }
    public PreparationToken Parameter { get; }

    public PreparationDirective()
    {
        Identifier = new PreparationToken(Position.UnknownPosition);
        Parameter = new PreparationToken(Position.UnknownPosition);
    }

    public Directive Instantiate() => new(Identifier.Instantiate(), Parameter.Instantiate());
}

public readonly struct PreprocessorResult
{
    public static PreprocessorResult Empty => new()
    {
        Text = string.Empty,
        Directives = ImmutableArray.Create<Directive>(),
    };

    public string? Text { get; init; }
    public ImmutableArray<Directive> Directives { get; init; }

    public Position TransformPosition(Position position)
    {
        int lineOffset = 0;
        int totalOffset = 0;

        for (int i = 0; i < Directives.Length; i++)
        {
            Position directive = Directives[i].Position;
            if (position.AbsoluteRange.Middle() + totalOffset > directive.AbsoluteRange.Middle())
            {
                lineOffset++;
                totalOffset += directive.AbsoluteRange.Size();
            }
            else
            {
                break;
            }
        }

        return new Position(
            (
                new SinglePosition(
                    position.Range.Start.Line + lineOffset,
                    position.Range.Start.Character
                    ),
                new SinglePosition(
                    position.Range.End.Line + lineOffset,
                    position.Range.End.Character
                    )
            ),
            (
                position.AbsoluteRange.Start + totalOffset,
                position.AbsoluteRange.End + totalOffset
            )
        );
    }
}

public class Preprocessor
{
    readonly List<string> Variables;
    readonly List<Directive> Directives = new();
    readonly StringBuilder Result = new();
    readonly Stack<bool> Skips = new();

    bool IsSkipping
    {
        get
        {
            for (int i = Skips.Count - 1; i >= 0; i--)
            { if (Skips[i]) return true; }
            return false;
        }
    }

    PreparationDirective? CurrentDirective;

    PreprocessorState State;
    int CurrentColumn;
    int CurrentLine;
    char PreviousChar;

    public Preprocessor(IEnumerable<string>? variables)
    {
        Variables = new List<string>(variables ?? Enumerable.Empty<string>());
        State = PreprocessorState.Text;
    }

    [return: NotNullIfNotNull(nameof(text))]
    public static PreprocessorResult Preprocess(string? text, IEnumerable<string>? variables)
    {
        if (text is null) return default;

        Preprocessor preprocessor = new(variables);
        preprocessor.PreprocessInternal(text);

        return new PreprocessorResult()
        {
            Text = preprocessor.Result.ToString(),
            Directives = preprocessor.Directives.ToImmutableArray(),
        };
    }

    void PreprocessInternal(string text)
    {
        for (int i = 0; i < text.Length; i++)
        { PreprocessInternal(text[i], i); }
    }

    void PreprocessInternal(char currChar, int offsetTotal)
    {
        bool breakLine = false;
        bool returnLine = false;

        char prevChar = PreviousChar;
        PreviousChar = currChar;

        if (prevChar is '\r' && currChar is '\n') // CRLF
        { breakLine = true; }
        else if (currChar is '\n') // LF
        { breakLine = true; }

        if (currChar is '\r' or '\n')
        { returnLine = true; }

        if (CurrentDirective is null)
        {
            if (currChar is '#')
            {
                CurrentDirective = new PreparationDirective();
                CurrentDirective.Identifier.Position = new Position(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn)), new Range<int>(offsetTotal));
                State = PreprocessorState.DirectiveIdentifier;
            }
            else if (!IsSkipping)
            {
                Result.Append(currChar);
            }
        }
        else
        {
            if (currChar is ' ')
            {
                if (CurrentDirective.Identifier.Content.Length == 0)
                {
                    CurrentDirective = null;
                }
                else if (State == PreprocessorState.DirectiveIdentifier)
                {
                    State = PreprocessorState.DirectiveParameter;
                    CurrentDirective.Parameter.Position = new Position(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn)), new Range<int>(offsetTotal));
                }
            }
            else if (currChar is '\r' or '\n')
            {
                Directive directive = CurrentDirective.Instantiate();
                CurrentDirective = null;
                Directives.Add(directive);

                switch (directive.Identifier.Content)
                {
                    case "define":
                    {
                        if (GetValue(directive.Parameter.Content))
                        { break; }
                        Variables.Add(directive.Parameter.Content);
                        break;
                    }

                    case "if":
                    {
                        Skips.Push(!GetValue(directive.Parameter.Content));
                        break;
                    }

                    case "else":
                    {
                        if (Skips.Count == 0)
                        { throw new SyntaxException($"Unexpected preprocessor directive \"{directive.Identifier}\"", directive.Identifier, null); }
                        Skips.Push(!Skips.Pop());
                        break;
                    }

                    case "endif":
                    {
                        if (Skips.Count == 0)
                        { throw new SyntaxException($"Unexpected preprocessor directive \"{directive.Identifier}\"", directive.Identifier, null); }
                        Skips.Pop();
                        break;
                    }

                    default:
                        throw new SyntaxException($"Invalid preprocessor directive \"{directive.Identifier}\"", directive.Identifier, null);
                }

                if (!IsSkipping)
                {
                    Result.Append(currChar);
                }
            }
            else
            {
                switch (State)
                {
                    case PreprocessorState.DirectiveIdentifier:
                    {
                        CurrentDirective.Identifier.Content.Append(currChar);
                        FinishToken(CurrentDirective.Identifier, offsetTotal);
                        break;
                    }

                    case PreprocessorState.DirectiveParameter:
                    {
                        CurrentDirective.Parameter.Content.Append(currChar);
                        FinishToken(CurrentDirective.Parameter, offsetTotal);
                        break;
                    }

                    default: throw new UnreachableException();
                }
            }
        }

        CurrentColumn++;
        if (breakLine) CurrentLine++;
        if (returnLine) CurrentColumn = 0;
    }

    void FinishToken(PreparationToken token, int offsetTotal)
    {
        token.Position = new Position(
            (
                new SinglePosition(
                    token.Position.Range.Start.Line,
                    token.Position.Range.Start.Character
                    ),
                new SinglePosition(
                    CurrentLine,
                    CurrentColumn
                    )
            ),
            (
                token.Position.AbsoluteRange.Start,
                offsetTotal
            )
        );
    }

    bool GetValue(string variable)
    {
        foreach (string other in Variables)
        {
            if (string.Equals(variable, other))
            { return true; }
        }
        return false;
    }
}
