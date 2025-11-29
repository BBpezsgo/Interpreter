namespace LanguageCore.Compiler;

public class CompiledFieldAccess : CompiledAccessExpression
{
    public required CompiledExpression Object { get; init; }
    public required CompiledField Field { get; init; }

    public override string Stringify(int depth = 0) => $"{Object.Stringify(depth + 1)}.{Field.Identifier}";
    public override string ToString() => $"{Object}.{Field.Identifier}";
}
