using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ForLoopStatement : StatementWithBlock
{
    public Token KeywordToken { get; }
    public Statement? Initialization { get; }
    public Expression Condition { get; }
    public Statement Step { get; }

    public IdentifierExpression Identifier => new(KeywordToken, File);
    public override Position Position => new(KeywordToken, Block);

    public ForLoopStatement(
        Token keyword,
        Statement? initialization,
        Expression condition,
        Statement step,
        Block body,
        Uri file)
        : base(body, file)
    {
        KeywordToken = keyword;
        Initialization = initialization;
        Condition = condition;
        Step = step;
    }

    public override string ToString()
        => $"{KeywordToken} (...) {Block}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        if (Initialization is not null)
        {
            foreach (Statement statement in Initialization.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }

        foreach (Statement statement in Condition.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }

        foreach (Statement statement in Step.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }

        foreach (Statement statement in Block.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }
    }
}
