using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ElseBranchStatement : BranchStatementBase
{
    public ElseBranchStatement(
        Token keyword,
        Statement body,
        Uri file)
        : base(keyword, IfPart.Else, body, file)
    { }

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Statement statement in Body.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }
    }
}
