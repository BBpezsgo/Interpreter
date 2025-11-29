namespace LanguageCore.Compiler;

public class CompiledElementAccess : CompiledAccessExpression
{
    public required CompiledExpression Base { get; init; }
    public required CompiledExpression Index { get; init; }

    public override string Stringify(int depth = 0) => $"{Base.Stringify(depth + 1)}[{Index.Stringify(depth + 1)}]";
    public override string ToString() => $"{Base}[{Index}]";
}
