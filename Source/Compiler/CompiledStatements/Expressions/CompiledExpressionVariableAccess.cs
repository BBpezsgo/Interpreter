namespace LanguageCore.Compiler;

public class CompiledExpressionVariableAccess : CompiledAccessExpression
{
    public required ExpressionVariable Variable { get; init; }

    public override string Stringify(int depth = 0) => Variable.Name;
    public override string ToString() => Variable.Name;
}
