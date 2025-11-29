using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class WhileLoopStatement : StatementWithAnyBody
{
    public Token Keyword { get; }
    public Expression Condition { get; }

    public override Position Position => new(Keyword, Body);

    public WhileLoopStatement(
        Token keyword,
        Expression condition,
        Statement body,
        Uri file) : base(body, file)
    {
        Keyword = keyword;
        Condition = condition;
    }

    public override string ToString()
        => $"{Keyword} ({Condition}) {Body}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Statement statement in Condition.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }

        foreach (Statement statement in Body.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }
    }
}
