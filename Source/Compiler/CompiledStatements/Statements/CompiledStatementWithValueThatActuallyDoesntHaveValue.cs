namespace LanguageCore.Compiler;

public class CompiledDummyExpression : CompiledExpression
{
    public required CompiledStatement Statement { get; init; }

    public override string Stringify(int depth = 0) => Statement.Stringify(depth);
    public override string ToString() => Statement.ToString();
}
