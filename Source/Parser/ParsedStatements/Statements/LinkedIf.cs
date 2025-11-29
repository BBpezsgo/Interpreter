using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class LinkedIf : LinkedBranch
{
    public Expression Condition { get; }
    public LinkedBranch? NextLink { get; init; }

    public override Position Position => new(KeywordToken, Condition, Body);

    public LinkedIf(Token keyword, Expression condition, Statement body, Uri file) : base(keyword, body, file)
    {
        Condition = condition;
    }

    public override string ToString()
        => $"{KeywordToken} ({Condition}) {Body}{(NextLink != null ? " ..." : string.Empty)}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Statement statement in Condition.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }

        foreach (Statement statement in Body.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }

        if (NextLink != null)
        {
            foreach (Statement statement in NextLink.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }
    }
}
