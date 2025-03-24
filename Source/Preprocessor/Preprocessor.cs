namespace LanguageCore;

public readonly struct PreprocessorResult
{
    public readonly string Text;
    public readonly ImmutableArray<Range<int>> TrimmedRanges;

    public PreprocessorResult(string text, ImmutableArray<Range<int>> trimmedRanges)
    {
        Text = text;
        TrimmedRanges = trimmedRanges;
    }
}

enum PreprocessorParseState
{
    None,
    NewLine,
    Statement,
    Expression,
}

enum PreprocessorEvaluationState
{
    None,
    ConditionTrue,
    ConditionFalse,
    Fallthrough,
}

public class Preprocessor
{
    readonly string Input;
    readonly ImmutableArray<string> Variables;

    readonly StringBuilder Result;
    readonly ImmutableArray<Range<int>>.Builder TrimmedRanges;

    PreprocessorParseState ParseState;
    int SymbolStart;
    string? CurrentStatement;
    MutableRange<int>? TrimmingRange;

    PreprocessorEvaluationState EvaluationState;

    Preprocessor(string input, ImmutableArray<string> variables)
    {
        Input = input;
        Variables = variables;

        Result = new StringBuilder();
        TrimmedRanges = ImmutableArray.CreateBuilder<Range<int>>();

        ParseState = PreprocessorParseState.NewLine;
        SymbolStart = -1;
        CurrentStatement = null;
    }

    public static PreprocessorResult Process(string input, ImmutableArray<string> variables)
    {
        Preprocessor preprocessor = new(input, variables);
        preprocessor.ProcessImpl();
        return new PreprocessorResult(preprocessor.Result.ToString(), ImmutableArray<Range<int>>.Empty);
    }

    void ProcessImpl()
    {
        for (int i = 0; i < Input.Length; i++)
        {
            char c = Input[i];

            switch (ParseState)
            {
                case PreprocessorParseState.None:
                    if (c is '\r' or '\n')
                    {
                        CurrentStatement = null;
                        ParseState = PreprocessorParseState.NewLine;
                        HandleCharacter(i, false);
                        continue;
                    }
                    else
                    {
                        CurrentStatement = null;
                        ParseState = PreprocessorParseState.None;
                        HandleCharacter(i, false);
                        continue;
                    }
                case PreprocessorParseState.NewLine:
                    if (c is '#')
                    {
                        CurrentStatement = null;
                        ParseState = PreprocessorParseState.Statement;
                        continue;
                    }
                    else
                    {
                        CurrentStatement = null;
                        ParseState = PreprocessorParseState.None;
                        HandleCharacter(i, false);
                        continue;
                    }
                case PreprocessorParseState.Statement:
                    if (SymbolStart == -1)
                    {
                        if (char.IsLetter(c))
                        {
                            CurrentStatement = null;
                            SymbolStart = i;
                            ParseState = PreprocessorParseState.Statement;
                            HandleCharacter(i - 1, true);
                            HandleCharacter(i, true);
                            continue;
                        }
                        else if (c is '\r' or '\n')
                        {
                            CurrentStatement = null;
                            ParseState = PreprocessorParseState.NewLine;
                            SymbolStart = -1;
                            HandleCharacter(i - i, false);
                            HandleCharacter(i, false);
                            continue;
                        }
                        else
                        {
                            CurrentStatement = null;
                            ParseState = PreprocessorParseState.None;
                            SymbolStart = -1;
                            HandleCharacter(i - i, false);
                            HandleCharacter(i, false);
                            continue;
                        }
                    }
                    else
                    {
                        if (char.IsLetter(c))
                        {
                            CurrentStatement = null;
                            ParseState = PreprocessorParseState.Statement;
                            HandleCharacter(i, true);
                            continue;
                        }
                        else
                        {
                            HandleCharacter(i, true);
                            CurrentStatement = Input[SymbolStart..i];
                            SymbolStart = -1;
                            if (c is '\r' or '\n')
                            {
                                Evaluate(CurrentStatement, null);
                                ParseState = PreprocessorParseState.NewLine;
                            }
                            else
                            {
                                ParseState = PreprocessorParseState.Expression;
                            }
                            continue;
                        }
                    }
                case PreprocessorParseState.Expression:
                    if (char.IsLetterOrDigit(c))
                    {
                        if (SymbolStart == -1)
                        {
                            SymbolStart = i;
                        }
                        ParseState = PreprocessorParseState.Expression;
                        HandleCharacter(i, true);
                        continue;
                    }
                    else if (c is '\r' or '\n')
                    {
                        if (SymbolStart != -1)
                        {
                            if (string.IsNullOrWhiteSpace(CurrentStatement))
                            { throw new UnreachableException(); }
                            HandleCharacter(i, true);
                            Evaluate(CurrentStatement, Input[SymbolStart..i]);
                            CurrentStatement = null;
                            SymbolStart = -1;
                            ParseState = PreprocessorParseState.NewLine;
                        }
                        else
                        {
                            HandleCharacter(i, true);
                            CurrentStatement = null;
                            SymbolStart = -1;
                            ParseState = PreprocessorParseState.NewLine;
                        }
                        continue;
                    }
                    else
                    {
                        HandleCharacter(i, true);
                        CurrentStatement = null;
                        SymbolStart = -1;
                        ParseState = PreprocessorParseState.None;
                        continue;
                    }
            }
        }
    }

    void HandleCharacter(int i, bool skip)
    {
        char c = Input[i];

        if (EvaluationState is PreprocessorEvaluationState.ConditionFalse or PreprocessorEvaluationState.Fallthrough)
        {
            Console.ForegroundColor = skip ? ConsoleColor.Yellow : ConsoleColor.Red;
        }
        else
        {
            Console.ForegroundColor = skip ? ConsoleColor.Yellow : ConsoleColor.Green;
        }
        Console.Write(c);
        Console.ResetColor();

        if (skip || EvaluationState is PreprocessorEvaluationState.ConditionFalse or PreprocessorEvaluationState.Fallthrough)
        {
            if (TrimmingRange.HasValue)
            {
                TrimmingRange = new MutableRange<int>(TrimmingRange.Value.Start, i);
            }
            else
            {
                TrimmingRange = new MutableRange<int>(i);
            }
        }
        else
        {
            TrimmingRange = null;
            Result.Append(c);
        }
    }

    void Evaluate(string statement, string? expression)
    {
        switch (statement)
        {
            case "if":
            {
                if (expression is null) throw new Exception();
                EvaluationState = Variables.Contains(expression) ? PreprocessorEvaluationState.ConditionTrue : PreprocessorEvaluationState.ConditionFalse;
                break;
            }
            case "elseif":
            {
                if (expression is null) throw new Exception();
                if (EvaluationState == PreprocessorEvaluationState.ConditionTrue)
                {
                    EvaluationState = PreprocessorEvaluationState.Fallthrough;
                }
                else if (EvaluationState == PreprocessorEvaluationState.ConditionFalse)
                {
                    EvaluationState = Variables.Contains(expression) ? PreprocessorEvaluationState.ConditionTrue : PreprocessorEvaluationState.ConditionFalse;
                }
                else if (EvaluationState == PreprocessorEvaluationState.Fallthrough)
                {
                    EvaluationState = PreprocessorEvaluationState.Fallthrough;
                }
                break;
            }
            case "else":
            {
                if (expression is not null) throw new Exception();
                if (EvaluationState == PreprocessorEvaluationState.ConditionTrue)
                {
                    EvaluationState = PreprocessorEvaluationState.ConditionFalse;
                }
                else if (EvaluationState == PreprocessorEvaluationState.ConditionFalse)
                {
                    EvaluationState = PreprocessorEvaluationState.ConditionTrue;
                }
                else if (EvaluationState == PreprocessorEvaluationState.Fallthrough)
                {
                    EvaluationState = PreprocessorEvaluationState.Fallthrough;
                }
                break;
            }
            case "endif":
            {
                if (expression is not null) throw new Exception();
                EvaluationState = PreprocessorEvaluationState.None;
                break;
            }
            default:
            {
                throw new Exception();
            }
        }
    }
}
