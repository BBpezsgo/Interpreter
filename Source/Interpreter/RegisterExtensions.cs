namespace LanguageCore.Runtime;

public static class RegisterExtensions
{
    public static InstructionOperand ToPtr(this Register register, int offset = 0) => register switch
    {
        Register.StackPointer => new InstructionOperand(offset, InstructionOperandType.PointerSP),
        Register.BasePointer => new InstructionOperand(offset, InstructionOperandType.PointerBP),
        Register.EAX => new InstructionOperand(offset, InstructionOperandType.PointerEAX),
        Register.EBX => new InstructionOperand(offset, InstructionOperandType.PointerEBX),
        Register.ECX => new InstructionOperand(offset, InstructionOperandType.PointerECX),
        Register.EDX => new InstructionOperand(offset, InstructionOperandType.PointerEDX),
        _ => throw new NotImplementedException(),
    };
}
