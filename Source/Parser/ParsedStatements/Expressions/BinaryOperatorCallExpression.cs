using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class BinaryOperatorCallExpression : Expression, IReadable, IReferenceableTo<CompiledOperatorDefinition>
{
    public const int ParameterCount = 2;

    #region Operators

    public const string BitshiftLeft = "<<";
    public const string BitshiftRight = ">>";
    public const string Addition = "+";
    public const string Subtraction = "-";
    public const string Multiplication = "*";
    public const string Division = "/";
    public const string Modulo = "%";
    public const string BitwiseAND = "&";
    public const string BitwiseOR = "|";
    public const string BitwiseXOR = "^";
    public const string CompLT = "<";
    public const string CompGT = ">";
    public const string CompGEQ = ">=";
    public const string CompLEQ = "<=";
    public const string CompNEQ = "!=";
    public const string CompEQ = "==";
    public const string LogicalAND = "&&";
    public const string LogicalOR = "||";

    #endregion

    public Token Operator { get; }
    public Expression Left { get; }
    /// <summary>
    /// Set by the <see cref="Parser"/>
    /// </summary>
    public Expression Right { get; set; }
    public CompiledOperatorDefinition? Reference { get; set; }

    public override Position Position => new(Operator, Left, Right);
    public ImmutableArray<Expression> Arguments => ImmutableArray.Create(Left, Right);

    public BinaryOperatorCallExpression(
        Token op,
        Expression left,
        Expression right,
        Uri file) : base(file)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBrackets?.Start);

        if (Left.ToString().Length < Stringify.CozyLength)
        { result.Append(Left); }
        else
        { result.Append("..."); }

        result.Append(' ');
        result.Append(Operator);
        result.Append(' ');

        if (Right.ToString().Length < Stringify.CozyLength)
        { result.Append(Right); }
        else
        { result.Append("..."); }

        result.Append(SurroundingBrackets?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(FindStatementType typeSearch)
    {
        StringBuilder result = new();

        result.Append(Operator.Content);
        result.Append('(');
        result.Append(typeSearch.Invoke(Left, out GeneralType? type1, new()) ? type1.ToString() : '?');
        result.Append(", ");
        result.Append(typeSearch.Invoke(Right, out GeneralType? type2, new()) ? type2.ToString() : '?');
        result.Append(')');

        return result.ToString();
    }
}
