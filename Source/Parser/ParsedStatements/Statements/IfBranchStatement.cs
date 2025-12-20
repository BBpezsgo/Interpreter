using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class IfBranchStatement : BranchStatementBase
{
    public Expression Condition { get; }

    public IfBranchStatement(
        Token keyword,
        Expression condition,
        Statement body,
        Uri file)
        : base(keyword, IfPart.If, body, file)
    {
        Condition = condition;
    }
}
