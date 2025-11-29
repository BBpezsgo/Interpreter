namespace LanguageCore.Compiler;

public class CompiledSetter : CompiledStatement
{
    public required CompiledAccessExpression Target { get; init; }
    public required CompiledExpression Value { get; init; }
    public required bool IsCompoundAssignment { get; init; }

    public override string Stringify(int depth = 0) => $"{Target.Stringify(depth)} = {Value.Stringify(depth)}";
    public override string ToString() => throw new NotImplementedException();
}
