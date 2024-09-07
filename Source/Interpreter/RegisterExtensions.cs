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
            Runtime.BitWidth._64 => InstructionOperandType.PointerBP64,
            _ => throw new UnreachableException(),
        }),

        Register.EAX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEAX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEAX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEAX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerEAX64,
            _ => throw new UnreachableException(),
        }),
        Register.EBX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEBX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEBX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEBX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerEBX64,
            _ => throw new UnreachableException(),
        }),
        Register.ECX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerECX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerECX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerECX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerECX64,
            _ => throw new UnreachableException(),
        }),
        Register.EDX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEDX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEDX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEDX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerEDX64,
            _ => throw new UnreachableException(),
        }),
        Register.RAX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerRAX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerRAX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerRAX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerRAX64,
            _ => throw new UnreachableException(),
        }),
        Register.RBX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerRBX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerRBX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerRBX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerRBX64,
            _ => throw new UnreachableException(),
        }),
        Register.RCX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerRCX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerRCX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerRCX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerRCX64,
            _ => throw new UnreachableException(),
        }),
        Register.RDX => new InstructionOperand(offset, dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerRDX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerRDX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerRDX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerRDX64,
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
