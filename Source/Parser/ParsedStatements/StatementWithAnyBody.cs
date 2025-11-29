namespace LanguageCore.Parser.Statements;

public abstract class StatementWithAnyBody : Statement
{
    public Statement Body { get; }

    protected StatementWithAnyBody(Statement body, Uri file) : base(file)
    {
        Body = body;
    }
}
