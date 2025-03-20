namespace LanguageCore.Compiler;

class Scope
{
    public readonly Stack<CompiledVariableDeclaration> Variables;
    public readonly ImmutableArray<CompiledVariableConstant> Constants;
    public readonly ImmutableArray<CompiledInstructionLabelDeclaration> InstructionLabels;

    public Scope(ImmutableArray<CompiledVariableConstant> constants, ImmutableArray<CompiledInstructionLabelDeclaration> instructionLabels)
    {
        Variables = new();
        Constants = constants;
        InstructionLabels = instructionLabels;
    }
}
