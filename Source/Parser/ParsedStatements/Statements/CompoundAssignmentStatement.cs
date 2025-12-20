using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class CompoundAssignmentStatement : AssignmentStatement, IReferenceableTo<CompiledOperatorDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledOperatorDefinition? Reference { get; set; }

    /// <summary>
    /// This should always starts with <c>"="</c>
    /// </summary>
    public Token Operator { get; }
    public Expression Left { get; }
    public Expression Right { get; }

    public override Position Position => new(Operator, Left, Right);

    public CompoundAssignmentStatement(
        Token @operator,
        Expression left,
        Expression right,
        Uri file) : base(file)
    {
        Operator = @operator;
        Left = left;
        Right = right;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (result.Length + Left.ToString().Length > Stringify.CozyLength)
        {
            result.Append($"... {Operator} ...");
        }
        else
        {
            result.Append(Left);
            result.Append(' ');
            result.Append(Operator);
            result.Append(' ');
            if (result.Length + Right.ToString().Length > Stringify.CozyLength)
            { result.Append("..."); }
            else
            { result.Append(Right); }
        }

        result.Append(Semicolon);
        return result.ToString();
    }

    public override SimpleAssignmentStatement ToAssignment()
    {
        BinaryOperatorCallExpression statementToAssign = GetOperatorCall();
        return new SimpleAssignmentStatement(Token.CreateAnonymous("=", TokenType.Operator, Operator.Position), Left, statementToAssign, File);
    }

    public BinaryOperatorCallExpression GetOperatorCall() => new(
        Token.CreateAnonymous(Operator.Content.Replace("=", string.Empty, StringComparison.Ordinal), TokenType.Operator, Operator.Position),
        Left,
        Right,
        File);
}
