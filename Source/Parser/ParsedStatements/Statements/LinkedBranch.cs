using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public abstract class LinkedBranch : StatementWithAnyBody
{
    public Token KeywordToken { get; }

    public IdentifierExpression Keyword => new(KeywordToken, File);

    protected LinkedBranch(Token keyword, Statement body, Uri file) : base(body, file)
    {
        KeywordToken = keyword;
    }
}
