using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statement;

public class PreprocessorTag : Statement
{
    public Token Identifier { get; }
    public Token? Argument { get; }

    public PreprocessorTag(Token identifier, Token? argument)
    {
        Identifier = identifier;
        Argument = argument;
    }

    public override Position Position => new(Identifier, Argument);

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }

    public override string ToString() => $"{Identifier} {Argument}".Trim();
}
