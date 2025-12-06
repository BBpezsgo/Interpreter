namespace LanguageCore.Compiler;

public class CompiledHeapAllocation : CompiledExpression
{
    public required CompiledFunctionDefinition Allocator { get; init; }
    public required CompiledTypeExpression TypeExpression { get; init; }

    public override string Stringify(int depth = 0) => $"new {Type}";
    public override string ToString() => $"new {Type}";
}
