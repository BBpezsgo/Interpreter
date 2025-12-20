using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class AnyCallExpression : Expression, IReadable, IReferenceableTo<CompiledFunctionDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledFunctionDefinition? Reference { get; set; }

    public Expression Expression { get; }
    public TokenPair Brackets { get; }
    public ImmutableArray<ArgumentExpression> Arguments { get; }
    public ImmutableArray<Token> Commas { get; }

    public override Position Position => new(Expression, Brackets);

    public AnyCallExpression(
        Expression expression,
        ImmutableArray<ArgumentExpression> arguments,
        ImmutableArray<Token> commas,
        TokenPair brackets,
        Uri file) : base(file)
    {
        Expression = expression;
        Arguments = arguments;
        Commas = commas;
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBrackets?.Start);

        result.Append(Expression);
        result.Append(Brackets.Start);

        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0)
            { result.Append(", "); }

            if (result.Length >= Stringify.CozyLength)
            { result.Append("..."); break; }

            result.Append(Arguments[i]);
        }
        result.Append(Brackets.End);

        result.Append(SurroundingBrackets?.End);
        result.Append(Semicolon);

        return result.ToString();
    }

    public string ToReadable(FindStatementType typeSearch)
    {
        StringBuilder result = new();
        result.Append("...");
        result.Append('(');
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) { result.Append(", "); }
            result.Append(typeSearch.Invoke(Arguments[i], out GeneralType? type, new()) ? type.ToString() : '?');
        }
        result.Append(')');
        return result.ToString();
    }
}
