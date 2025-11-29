namespace LanguageCore.Parser.Statements;

public class IfContainer : Statement
{
    public ImmutableArray<BranchStatementBase> Branches { get; }

    public override Position Position => new(Branches);

    public IfContainer(
        ImmutableArray<BranchStatementBase> parts,
        Uri file) : base(file)
    {
        Branches = parts;
    }

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (BranchStatementBase branch in Branches)
        {
            foreach (Statement statement in branch.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }
    }

    public override string ToString()
    {
        if (Branches.Length == 0) return "null";
        return Branches[0].ToString();
    }
}
