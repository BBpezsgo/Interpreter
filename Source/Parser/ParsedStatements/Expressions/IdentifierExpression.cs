using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class IdentifierExpression : Expression, IReferenceableTo
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public object? Reference { get; set; }

    public Token Identifier { get; }

    public TokenAnalyzedType AnalyzedType
    {
        get => Identifier.AnalyzedType;
        set => Identifier.AnalyzedType = value;
    }
    public string Content => Identifier.Content;
    public override Position Position => Identifier.Position;

    public IdentifierExpression(
        Token token,
        Uri file) : base(file)
    {
        Identifier = token;
    }

    public override string ToString() => Identifier.Content;

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;
    }
}
