namespace LanguageCore.Compiler;

public class CompiledDereference : CompiledAccessExpression
{
    public required CompiledExpression Address { get; init; }

    public override string Stringify(int depth = 0) => $"*{Address.Stringify(depth + 1)}";
    public override string ToString() => $"*{Address}";
}
