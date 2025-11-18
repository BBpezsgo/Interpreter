namespace LanguageCore.Compiler;

public class Scope
{
    public readonly Stack<CompiledVariableDeclaration> Variables;
    public readonly ImmutableArray<CompiledVariableConstant> Constants;

    public Scope(ImmutableArray<CompiledVariableConstant> constants)
    {
        Variables = new();
        Constants = constants;
    }
}
