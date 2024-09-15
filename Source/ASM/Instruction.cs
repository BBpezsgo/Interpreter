namespace LanguageCore.ASM;

[ExcludeFromCodeCoverage]
public readonly struct Instruction
{
    public readonly OpCode OpCode;
    public readonly InstructionOperand ParameterA;
    public readonly InstructionOperand ParameterB;

    public Instruction(OpCode opCode, InstructionOperand parameterA = default, InstructionOperand parameterB = default)
    {
        OpCode = opCode;
        ParameterA = parameterA;
        ParameterB = parameterB;
    }

    public override string ToString()
    {
        if (ParameterB.Equals(default))
        {
            if (ParameterA.Equals(default))
            { return TextSectionBuilder.StringifyInstruction(OpCode); }
            else
            { return $"{TextSectionBuilder.StringifyInstruction(OpCode)} {ParameterA}"; }
        }
        else
        { return $"{TextSectionBuilder.StringifyInstruction(OpCode)} {ParameterA}, {ParameterB}"; }
    }
}
