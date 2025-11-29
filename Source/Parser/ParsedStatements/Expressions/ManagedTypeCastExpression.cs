namespace LanguageCore.Parser.Statements;

public class ManagedTypeCastExpression : Expression, IHaveType
{
    public Expression Expression { get; }
    public TypeInstance Type { get; }
    public TokenPair Brackets { get; }

    public override Position Position => new(Expression, Type);

    public ManagedTypeCastExpression(
        Expression expression,
        TypeInstance type,
        TokenPair brackets,
        Uri file) : base(file)
    {
        Expression = expression;
        Type = type;
        Brackets = brackets;
    }

    public override string ToString()
        => $"{SurroundingBrackets?.Start}({Type}){Expression}{SurroundingBrackets?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Statement statement in Expression.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }
    }
}
