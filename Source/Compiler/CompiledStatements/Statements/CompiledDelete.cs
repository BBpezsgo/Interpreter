namespace LanguageCore.Compiler;

public class CompiledDelete : CompiledStatement
{
    public required CompiledExpression Value { get; init; }
    public required CompiledCleanup Cleanup { get; init; }

    public override string Stringify(int depth = 0) => $"delete {Value.Stringify(depth + 1)}";
    public override string ToString() => $"delete {Value}";
}
