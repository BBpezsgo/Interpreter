namespace LanguageCore.Compiler;

public class CompiledLabelReference : CompiledExpression
{
    public required CompiledLabelDeclaration InstructionLabel { get; init; }

    public override string Stringify(int depth = 0) => $"&{InstructionLabel.Identifier}";
    public override string ToString() => $"&{InstructionLabel.Identifier}";
}
