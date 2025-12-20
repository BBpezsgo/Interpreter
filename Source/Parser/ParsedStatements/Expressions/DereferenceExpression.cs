using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class DereferenceExpression : Expression
{
    public Token Operator { get; }
    public Expression Expression { get; }

    public override Position Position => new(Operator, Expression);

    public DereferenceExpression(
        Token @operator,
        Expression expression,
        Uri file) : base(file)
    {
        Operator = @operator;
        Expression = expression;
    }

    public override string ToString()
        => $"{SurroundingBrackets?.Start}{Operator}{Expression}{SurroundingBrackets?.End}{Semicolon}";

}
