namespace LanguageCore.Compiler;

public class CompiledForLoop : CompiledStatement
{
    public required CompiledStatement? Initialization { get; init; }
    public required CompiledExpression? Condition { get; init; }
    public required CompiledStatement? Step { get; init; }
    public required CompiledStatement Body { get; init; }

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();

        res.Append($"for ({Initialization?.Stringify(depth + 1)}; {Condition?.Stringify(depth + 1)}; {Step?.Stringify(depth + 1)})");
        res.Append(' ');
        res.Append(Body.Stringify(depth));

        return res.ToString();
    }

    public override string ToString() => $"for ({Initialization}; {Condition}; {Step}) {Body}";
}
