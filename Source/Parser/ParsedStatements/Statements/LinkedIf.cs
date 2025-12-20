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
}
