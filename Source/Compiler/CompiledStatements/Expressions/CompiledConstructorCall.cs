namespace LanguageCore.Compiler;

public class CompiledConstructorCall : CompiledExpression
{
    public required CompiledConstructorDefinition Function { get; init; }
    public required CompiledExpression Object { get; init; }
    public required ImmutableArray<CompiledArgument> Arguments { get; init; }

    public override string Stringify(int depth = 0) => $"new {Function.Type}({string.Join(", ", Arguments.Select(v => v.Stringify(depth + 1)))})";
    public override string ToString() => $"new {Function.Type}({string.Join(", ", Arguments.Select(v => v.ToString()))})";
}
