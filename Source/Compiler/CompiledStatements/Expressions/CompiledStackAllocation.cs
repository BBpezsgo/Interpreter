namespace LanguageCore.Compiler;

public class CompiledStackAllocation : CompiledExpression
{
    public required CompiledTypeExpression TypeExpression { get; init; }

    public override string Stringify(int depth = 0) => $"new {Type}";
    public override string ToString() => $"new {Type}";
}
