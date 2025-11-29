namespace LanguageCore.Compiler;

public class CompiledWhileLoop : CompiledStatement
{
    public required CompiledExpression Condition { get; init; }
    public required CompiledStatement Body { get; init; }

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();

        res.Append($"while ({Condition.Stringify(depth + 1)})");
        res.Append(' ');
        res.Append(Body.Stringify(depth));

        return res.ToString();
    }

    public override string ToString() => $"while ({Condition}) {Body}";
}
