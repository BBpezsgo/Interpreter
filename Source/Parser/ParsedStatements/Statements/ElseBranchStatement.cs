using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ElseBranchStatement : BranchStatementBase
{
    public ElseBranchStatement(
        Token keyword,
        Statement body,
        Uri file)
        : base(keyword, IfPart.Else, body, file)
    { }
}
