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

    public override string ToString()
    {
        if (Branches.Length == 0) return "null";
        return Branches[0].ToString();
    }
}
