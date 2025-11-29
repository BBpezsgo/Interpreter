using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ReinterpretExpression : Expression, IHaveType
{
    public Expression PrevStatement { get; }
    public Token Keyword { get; }
    public TypeInstance Type { get; }

    public override Position Position => new(PrevStatement, Keyword, Type);

    public ReinterpretExpression(
        Expression prevStatement,
        Token keyword,
        TypeInstance type,
        Uri file) : base(file)
    {
        PrevStatement = prevStatement;
        Keyword = keyword;
        Type = type;
    }

    public override string ToString()
        => $"{SurroundingBrackets?.Start}{PrevStatement} {Keyword} {Type}{SurroundingBrackets?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Statement statement in PrevStatement.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }
    }
}
