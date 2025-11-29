namespace LanguageCore.Compiler;

public class CompiledStackAllocation : CompiledExpression
{
    public override string Stringify(int depth = 0) => $"new {Type}";
    public override string ToString() => $"new {Type}";
}
