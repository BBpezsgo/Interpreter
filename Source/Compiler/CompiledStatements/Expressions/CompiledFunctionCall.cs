namespace LanguageCore.Compiler;

public class CompiledFunctionCall : CompiledExpression
{
    public required ICompiledFunctionDefinition Function { get; init; }
    public required ImmutableArray<CompiledArgument> Arguments { get; init; }

    public override string Stringify(int depth = 0) => $"{Function switch
    {
        CompiledFunctionDefinition v => v.Identifier.Content,
        CompiledOperatorDefinition v => v.Identifier.Content,
        CompiledGeneralFunctionDefinition v => v.Identifier.Content,
        CompiledConstructorDefinition v => v.Type.ToString(),
        _ => throw new UnreachableException(),
    }}({string.Join(", ", Arguments.Select(v => v.Stringify(depth + 1)))})";

    public override string ToString() => $"{Function switch
    {
        CompiledFunctionDefinition v => v.Identifier.Content,
        CompiledOperatorDefinition v => v.Identifier.Content,
        CompiledGeneralFunctionDefinition v => v.Identifier.Content,
        CompiledConstructorDefinition v => v.Type.ToString(),
        _ => throw new UnreachableException(),
    }}({string.Join(", ", Arguments.Select(v => v.ToString()))})";
}
