namespace LanguageCore.Compiler;

public class CompiledGetReference : CompiledExpression
{
    public required CompiledExpression Of { get; init; }

    public override string Stringify(int depth = 0) => $"&{Of.Stringify(depth + 1)}";
    public override string ToString() => $"&{Of}";
}
