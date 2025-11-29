using LanguageCore.Compiler;

namespace LanguageCore.Parser.Statements;

public class CompiledTypeExpression : Expression
{
    public GeneralType Type { get; }
    public override Position Position { get; }

    public CompiledTypeExpression(GeneralType type, Position position, Uri file) : base(file)
    {
        Type = type;
        Position = position;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBrackets?.Start);

        result.Append(Type);

        result.Append(SurroundingBrackets?.End);
        result.Append(Semicolon);

        return result.ToString();
    }
    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;
    }
}
