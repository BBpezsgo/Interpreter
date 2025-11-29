namespace LanguageCore.Compiler;

public class CompiledReturn : CompiledStatement
{
    public required CompiledExpression? Value { get; init; }

    public override string Stringify(int depth = 0)
        => Value is null
        ? $"return"
        : $"return {Value.Stringify(depth + 1)}";

    public override string ToString()
        => Value is null
        ? $"return"
        : $"return {Value}";
}
