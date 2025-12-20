using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class LinkedElse : LinkedBranch
{
    public override Position Position => new(KeywordToken, Body);

    public LinkedElse(Token keyword, Statement body, Uri file) : base(keyword, body, file)
    { }
}
