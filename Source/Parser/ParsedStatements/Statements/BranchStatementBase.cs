using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public abstract class BranchStatementBase : StatementWithAnyBody
{
    public Token Keyword { get; }
    public IfPart Type { get; }

    public override Position Position => new(Keyword, Body);

    protected BranchStatementBase(
        Token keyword,
        IfPart type,
        Statement body,
        Uri file)
        : base(body, file)
    {
        Keyword = keyword;
        Type = type;
    }

    public enum IfPart
    {
        If,
        Else,
        ElseIf,
    }

    public override string ToString()
        => $"{Keyword}{((Type != IfPart.Else) ? " (...)" : "")} {Body}{Semicolon}";
}
