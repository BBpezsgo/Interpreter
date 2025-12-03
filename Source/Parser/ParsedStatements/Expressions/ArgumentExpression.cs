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

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;
        foreach (Statement v in Value.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis)) yield return v;
    }

    public override string ToString() => Value.ToString();
}
