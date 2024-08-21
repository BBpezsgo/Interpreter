namespace LanguageCore.Runtime;

public static class RegisterExtensions
{
    public static InstructionOperand ToPtr(this Register register, int offset, BitWidth dataSize) => register switch
    {
        Register.StackPointer => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerSP8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerSP16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerSP32,
            _ => throw new UnreachableException(),
        }),
        Register.BasePointer => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerBP8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerBP16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerBP32,
            _ => throw new UnreachableException(),
        }),
        Register.EAX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEAX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEAX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEAX32,
            _ => throw new UnreachableException(),
        }),
        Register.EBX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEBX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEBX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEBX32,
            _ => throw new UnreachableException(),
        }),
        Register.ECX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerECX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerECX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerECX32,
            _ => throw new UnreachableException(),
        }),
        Register.EDX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEDX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEDX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEDX32,
            _ => throw new UnreachableException(),
        }),
        Register.RAX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEAX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEAX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEAX32,
            _ => throw new UnreachableException(),
        }),
        Register.RBX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEBX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEBX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEBX32,
            _ => throw new UnreachableException(),
        }),
        Register.RCX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerECX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerECX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerECX32,
            _ => throw new UnreachableException(),
        }),
        Register.RDX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEDX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEDX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEDX32,
            _ => throw new UnreachableException(),
        }),
        _ => throw new NotImplementedException(),
    };

    public static BitWidth BitWidth(this Register register) => register switch
    {
        Register.CodePointer => Runtime.BitWidth._32,
        Register.StackPointer => Runtime.BitWidth._32,
        Register.BasePointer => Runtime.BitWidth._32,
        Register.RAX => Runtime.BitWidth._64,
        Register.EAX => Runtime.BitWidth._32,
        Register.AX => Runtime.BitWidth._16,
        Register.AH => Runtime.BitWidth._8,
        Register.AL => Runtime.BitWidth._8,
        Register.RBX => Runtime.BitWidth._64,
        Register.EBX => Runtime.BitWidth._32,
        Register.BX => Runtime.BitWidth._16,
        Register.BH => Runtime.BitWidth._8,
        Register.BL => Runtime.BitWidth._8,
        Register.RCX => Runtime.BitWidth._64,
        Register.ECX => Runtime.BitWidth._32,
        Register.CX => Runtime.BitWidth._16,
        Register.CH => Runtime.BitWidth._8,
        Register.CL => Runtime.BitWidth._8,
        Register.RDX => Runtime.BitWidth._64,
        Register.EDX => Runtime.BitWidth._32,
        Register.DX => Runtime.BitWidth._16,
        Register.DH => Runtime.BitWidth._8,
        Register.DL => Runtime.BitWidth._8,
        _ => throw new UnreachableException(),
    };
}
