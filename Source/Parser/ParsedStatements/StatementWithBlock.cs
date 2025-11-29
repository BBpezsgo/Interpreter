namespace LanguageCore.Parser.Statements;

public abstract class StatementWithBlock : Statement
{
    public Block Block { get; }

    protected StatementWithBlock(Block block, Uri file) : base(file)
    {
        Block = block;
    }
}
