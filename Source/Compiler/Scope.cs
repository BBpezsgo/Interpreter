namespace LanguageCore.Compiler;

public class Scope
{
    public readonly Stack<CompiledVariableDefinition> Variables;
    public readonly ImmutableArray<CompiledVariableConstant> Constants;

    public Scope(ImmutableArray<CompiledVariableConstant> constants)
    {
        Variables = new();
        Constants = constants;
    }
}
