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
}
