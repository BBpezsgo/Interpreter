using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ShortOperatorCall : AssignmentStatement, IReadable, IReferenceableTo<CompiledOperatorDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledOperatorDefinition? Reference { get; set; }

    public Token Operator { get; }
    public Expression Expression { get; }

    public ImmutableArray<Expression> Arguments => ImmutableArray.Create(Expression);
    public override Position Position => new(Operator, Expression);

    public ShortOperatorCall(
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

        if (Expression != null)
        {
            if (Expression.ToString().Length <= Stringify.CozyLength)
            { result.Append(Expression); }
            else
            { result.Append("..."); }

            result.Append(' ');
            result.Append(Operator);
        }
        else
        { result.Append(Operator); }

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

    public override SimpleAssignmentStatement ToAssignment()
    {
        BinaryOperatorCallExpression operatorCall = GetOperatorCall();
        Token assignmentToken = Token.CreateAnonymous("=", TokenType.Operator, Operator.Position);
        return new SimpleAssignmentStatement(assignmentToken, Expression, operatorCall, File);
    }

    public BinaryOperatorCallExpression GetOperatorCall()
    {
        switch (Operator.Content)
        {
            case "++":
            {
                LiteralExpression one = LiteralExpression.CreateAnonymous(LiteralType.Integer, "1", Operator.Position, File);
                return new BinaryOperatorCallExpression(Token.CreateAnonymous("+", TokenType.Operator, Operator.Position), Expression, one, File);
            }

            case "--":
            {
                LiteralExpression one = LiteralExpression.CreateAnonymous(LiteralType.Integer, "1", Operator.Position, File);
                return new BinaryOperatorCallExpression(Token.CreateAnonymous("-", TokenType.Operator, Operator.Position), Expression, one, File);
            }

            default: throw new NotImplementedException();
        }
    }
}
