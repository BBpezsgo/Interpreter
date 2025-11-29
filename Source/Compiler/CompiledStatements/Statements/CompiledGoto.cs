namespace LanguageCore.Compiler;

public class CompiledGoto : CompiledStatement
{
    public required CompiledExpression Value { get; init; }

    public override string Stringify(int depth = 0) => $"goto {Value.Stringify(depth + 1)}";
    public override string ToString() => $"goto {Value}";
}
