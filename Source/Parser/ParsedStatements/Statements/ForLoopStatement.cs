using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ForLoopStatement : StatementWithBlock
{
    public Token KeywordToken { get; }
    public Statement? Initialization { get; }
    public Expression? Condition { get; }
    public Statement? Step { get; }

    public IdentifierExpression Identifier => new(KeywordToken, File);
    public override Position Position => new(KeywordToken, Block);

    public ForLoopStatement(
        Token keyword,
        Statement? initialization,
        Expression? condition,
        Statement? step,
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
}
