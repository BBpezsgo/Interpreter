using LanguageCore.Parser.Statement;

namespace LanguageCore.Compiler;

public class CompiledInstructionLabel : InstructionLabel, IHaveInstructionOffset
{
    public static readonly FunctionType Type = new(BuiltinType.Void, Enumerable.Empty<GeneralType>());

    public int InstructionOffset { get; set; }

    public CompiledInstructionLabel(int instructionOffset, InstructionLabel declaration) : base(declaration)
    {
        InstructionOffset = instructionOffset;
    }
}
