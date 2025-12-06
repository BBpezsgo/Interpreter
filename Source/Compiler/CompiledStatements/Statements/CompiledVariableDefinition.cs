namespace LanguageCore.Compiler;

public class CompiledVariableDefinition : CompiledStatement
{
    public required CompiledTypeExpression TypeExpression { get; init; }
    public required GeneralType Type { get; init; }
    public required string Identifier { get; init; }
    public required CompiledExpression? InitialValue { get; init; }
    public required CompiledCleanup Cleanup { get; init; }
    public required bool IsGlobal { get; init; }
    public HashSet<CompiledVariableAccess> Setters { get; } = new();
    public HashSet<CompiledVariableAccess> Getters { get; } = new();

    public override string Stringify(int depth = 0)
        =>
        InitialValue is null
        ? $"{Type} {Identifier}"
        : $"{Type} {Identifier} = {InitialValue.Stringify(depth + 1)}";

    public override string ToString()
        =>
        InitialValue is null
        ? $"{Type} {Identifier}"
        : $"{Type} {Identifier} = {InitialValue}";
}
