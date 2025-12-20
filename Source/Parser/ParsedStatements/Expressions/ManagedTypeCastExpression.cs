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
}
