using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class IfBranchStatement : BranchStatementBase
{
    public Expression Condition { get; }

    public IfBranchStatement(
        Token keyword,
        Expression condition,
        Statement body,
        Uri file)
        : base(keyword, IfPart.If, body, file)
    {
        Condition = condition;
    }

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Statement statement in Condition.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }

        foreach (Statement statement in Body.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }
    }
}
