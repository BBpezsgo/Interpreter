namespace LanguageCore.Compiler;

public class CompiledFunctionReference : CompiledExpression
{
    public required IHaveInstructionOffset Function { get; init; }

    public override string Stringify(int depth = 0) => $"&{Function switch
    {
        CompiledFunctionDefinition v => v.Identifier,
        _ => Function.ToString(),
    }}";
    public override string ToString() => $"&{Function switch
    {
        CompiledFunctionDefinition v => v.Identifier,
        _ => Function.ToString(),
    }}";
}
