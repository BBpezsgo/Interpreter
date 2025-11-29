using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class InstructionLabelDeclaration : Statement
{
    public IdentifierExpression Identifier { get; }
    public Token Colon { get; }

    public override Position Position => new(
        Identifier,
        Colon
    );

    public InstructionLabelDeclaration(
        IdentifierExpression identifier,
        Token colon,
        Uri file) : base(file)
    {
        Identifier = identifier;
        Colon = colon;
    }

    public InstructionLabelDeclaration(InstructionLabelDeclaration other) : base(other.File)
    {
        Identifier = other.Identifier;
        Colon = other.Colon;
    }

    public override string ToString() => $"{Identifier}{Colon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;
    }
}
