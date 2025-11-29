namespace LanguageCore.Compiler;

public class CompiledReinterpretation : CompiledExpression
{
    public required CompiledExpression Value { get; init; }

    public override string Stringify(int depth = 0) => $"{Value.Stringify(depth)}";
    public override string ToString() => $"{Value} as {Type}";
}
