using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ArgumentExpression : Expression
{
    public Token? Modifier { get; }
    public Expression Value { get; }

    public override Position Position => new(Modifier, Value);

    public ArgumentExpression(
        Token? modifier,
        Expression value,
        Uri file) : base(file)
    {
        Modifier = modifier;
        Value = value;
    }

    public static ArgumentExpression Wrap(Expression expression) => expression is ArgumentExpression v ? v : new ArgumentExpression(null, expression, expression.File);

    public override string ToString() => Value.ToString();
}
