using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class GetReferenceExpression : Expression
{
    public Token Operator { get; }
    public Expression Expression { get; }

    public override Position Position => new(Operator, Expression);

    public GetReferenceExpression(
        Token operatorToken,
        Expression expression,
        Uri file) : base(file)
    {
        Operator = operatorToken;
        Expression = expression;
    }

    public override string ToString()
        => $"{SurroundingBrackets?.Start}{Operator}{Expression}{SurroundingBrackets?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Statement statement in Expression.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }
    }
}
