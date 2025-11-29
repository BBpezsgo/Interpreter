namespace LanguageCore.Compiler;

public class CompiledVariableAccess : CompiledAccessExpression
{
    public required CompiledVariableDefinition Variable { get; init; }

    public override string Stringify(int depth = 0) => $"{Variable.Identifier}";
    public override string ToString() => $"{Variable.Identifier}";
}
