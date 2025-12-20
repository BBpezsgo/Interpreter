using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class UnaryOperatorCallExpression : Expression, IReadable, IReferenceableTo<CompiledOperatorDefinition>
{
    public const int ParameterCount = 1;

    #region Operators

    public const string LogicalNOT = "!";
    public const string BinaryNOT = "~";
    public const string UnaryPlus = "+";
    public const string UnaryMinus = "-";

    #endregion

    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledOperatorDefinition? Reference { get; set; }

    public Token Operator { get; }
    public Expression Expression { get; }

    public override Position Position => new(Operator, Expression);
    public ImmutableArray<Expression> Arguments => ImmutableArray.Create(Expression);

    public UnaryOperatorCallExpression(
        Token op,
        Expression expression,
        Uri file) : base(file)
    {
        Operator = op;
        Expression = expression;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBrackets?.Start);

        if (Expression.ToString().Length < Stringify.CozyLength)
        { result.Append(Expression); }
        else
        { result.Append("..."); }

        result.Append(' ');

        result.Append(Operator);

        result.Append(SurroundingBrackets?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(FindStatementType typeSearch)
    {
        StringBuilder result = new();

        result.Append(Operator.Content);
        result.Append('(');
        result.Append(typeSearch.Invoke(Expression, out GeneralType? type, new()) ? type.ToString() : '?');
        result.Append(')');

        return result.ToString();
    }
}
