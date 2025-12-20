using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ElseIfBranchStatement : BranchStatementBase
{
    public Expression Condition { get; }

    public ElseIfBranchStatement(
        Token keyword,
        Expression condition,
        Statement body,
        Uri file)
        : base(keyword, IfPart.ElseIf, body, file)
    {
        Condition = condition;
    }
}
