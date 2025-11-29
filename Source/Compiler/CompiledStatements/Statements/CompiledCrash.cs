namespace LanguageCore.Compiler;

public class CompiledCrash : CompiledStatement
{
    public required CompiledExpression Value { get; init; }

    public override string Stringify(int depth = 0) => $"crash {Value.Stringify(depth + 1)}";
    public override string ToString() => $"crash {Value}";
}
