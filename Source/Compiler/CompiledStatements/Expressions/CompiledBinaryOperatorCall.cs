namespace LanguageCore.Compiler;

public class CompiledBinaryOperatorCall : CompiledExpression
{
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

    public required string Operator { get; init; }
    public required CompiledExpression Left { get; init; }
    public required CompiledExpression Right { get; init; }

    public override string Stringify(int depth = 0) => $"({Left.Stringify(depth + 1)} {Operator} {Right.Stringify(depth + 1)})";

    public override string ToString()
    {
        StringBuilder result = new();

        if (Left.ToString().Length < CozyLength)
        { result.Append(Left); }
        else
        { result.Append("..."); }

        result.Append(' ');
        result.Append(Operator);
        result.Append(' ');

        if (Right.ToString().Length < CozyLength)
        { result.Append(Right); }
        else
        { result.Append("..."); }

        return result.ToString();
    }
}
