namespace LanguageCore.Runtime;

public static class RegisterExtensions
{
    public static InstructionOperandType ToPtr(this Register register, BitWidth dataSize) => register switch
    {
        Register.StackPointer => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerSP8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerSP16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerSP32,
            _ => throw new UnreachableException(),
        },
        Register.BasePointer => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerBP8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerBP16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerBP32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerBP64,
            _ => throw new UnreachableException(),
        },

        Register.EAX => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEAX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEAX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEAX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerEAX64,
            _ => throw new UnreachableException(),
        },
        Register.EBX => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEBX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEBX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEBX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerEBX64,
            _ => throw new UnreachableException(),
        },
        Register.ECX => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerECX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerECX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerECX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerECX64,
            _ => throw new UnreachableException(),
        },
        Register.EDX => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerEDX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerEDX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerEDX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerEDX64,
            _ => throw new UnreachableException(),
        },
        Register.RAX => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerRAX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerRAX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerRAX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerRAX64,
            _ => throw new UnreachableException(),
        },
        Register.RBX => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerRBX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerRBX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerRBX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerRBX64,
            _ => throw new UnreachableException(),
        },
        Register.RCX => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerRCX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerRCX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerRCX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerRCX64,
            _ => throw new UnreachableException(),
        },
        Register.RDX => dataSize switch
        {
            Runtime.BitWidth._8 => InstructionOperandType.PointerRDX8,
            Runtime.BitWidth._16 => InstructionOperandType.PointerRDX16,
            Runtime.BitWidth._32 => InstructionOperandType.PointerRDX32,
            Runtime.BitWidth._64 => InstructionOperandType.PointerRDX64,
            _ => throw new UnreachableException(),
        },
        _ => throw new NotImplementedException(),
    };

    public static InstructionOperand ToPtr(this Register register, int offset, BitWidth dataSize) => new(offset, register.ToPtr(dataSize));

    public static bool IsGeneralPurpose(this Register register) => register is not Register.BasePointer and not Register.CodePointer and not Register.StackPointer;

    public static BitWidth BitWidth(this Register register) => register switch
    {
        0 => 0,
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

    public static bool IsImmediate(this InstructionOperandType type) => type
        is InstructionOperandType.Immediate8
        or InstructionOperandType.Immediate16
        or InstructionOperandType.Immediate32
        or InstructionOperandType.Immediate64;

    public static bool IsPointer(this InstructionOperandType type) => type
        is InstructionOperandType.Pointer8
        or InstructionOperandType.Pointer16
        or InstructionOperandType.Pointer32;

    public static bool IsRegisterPointer(this InstructionOperandType type) => type
        is InstructionOperandType.PointerBP8
        or InstructionOperandType.PointerBP16
        or InstructionOperandType.PointerBP32
        or InstructionOperandType.PointerBP64
        or InstructionOperandType.PointerSP8
        or InstructionOperandType.PointerSP16
        or InstructionOperandType.PointerSP32
        or InstructionOperandType.PointerEAX8
        or InstructionOperandType.PointerEAX16
        or InstructionOperandType.PointerEAX32
        or InstructionOperandType.PointerEAX64
        or InstructionOperandType.PointerEBX8
        or InstructionOperandType.PointerEBX16
        or InstructionOperandType.PointerEBX32
        or InstructionOperandType.PointerEBX64
        or InstructionOperandType.PointerECX8
        or InstructionOperandType.PointerECX16
        or InstructionOperandType.PointerECX32
        or InstructionOperandType.PointerECX64
        or InstructionOperandType.PointerEDX8
        or InstructionOperandType.PointerEDX16
        or InstructionOperandType.PointerEDX32
        or InstructionOperandType.PointerEDX64
        or InstructionOperandType.PointerRAX8
        or InstructionOperandType.PointerRAX16
        or InstructionOperandType.PointerRAX32
        or InstructionOperandType.PointerRAX64
        or InstructionOperandType.PointerRBX8
        or InstructionOperandType.PointerRBX16
        or InstructionOperandType.PointerRBX32
        or InstructionOperandType.PointerRBX64
        or InstructionOperandType.PointerRCX8
        or InstructionOperandType.PointerRCX16
        or InstructionOperandType.PointerRCX32
        or InstructionOperandType.PointerRCX64
        or InstructionOperandType.PointerRDX8
        or InstructionOperandType.PointerRDX16
        or InstructionOperandType.PointerRDX32
        or InstructionOperandType.PointerRDX64;

    public static Register RegisterOfPointer(this InstructionOperandType type) => type switch
    {
        InstructionOperandType.Immediate8 => throw new InvalidOperationException(),
        InstructionOperandType.Immediate16 => throw new InvalidOperationException(),
        InstructionOperandType.Immediate32 => throw new InvalidOperationException(),
        InstructionOperandType.Immediate64 => throw new InvalidOperationException(),
        InstructionOperandType.Pointer8 => throw new InvalidOperationException(),
        InstructionOperandType.Pointer16 => throw new InvalidOperationException(),
        InstructionOperandType.Pointer32 => throw new InvalidOperationException(),
        InstructionOperandType.Register => throw new InvalidOperationException(),
        InstructionOperandType.PointerBP8 => Register.BasePointer,
        InstructionOperandType.PointerBP16 => Register.BasePointer,
        InstructionOperandType.PointerBP32 => Register.BasePointer,
        InstructionOperandType.PointerBP64 => Register.BasePointer,
        InstructionOperandType.PointerSP8 => Register.StackPointer,
        InstructionOperandType.PointerSP16 => Register.StackPointer,
        InstructionOperandType.PointerSP32 => Register.StackPointer,
        InstructionOperandType.PointerEAX8 => Register.EAX,
        InstructionOperandType.PointerEAX16 => Register.EAX,
        InstructionOperandType.PointerEAX32 => Register.EAX,
        InstructionOperandType.PointerEAX64 => Register.EAX,
        InstructionOperandType.PointerEBX8 => Register.EBX,
        InstructionOperandType.PointerEBX16 => Register.EBX,
        InstructionOperandType.PointerEBX32 => Register.EBX,
        InstructionOperandType.PointerEBX64 => Register.EBX,
        InstructionOperandType.PointerECX8 => Register.ECX,
        InstructionOperandType.PointerECX16 => Register.ECX,
        InstructionOperandType.PointerECX32 => Register.ECX,
        InstructionOperandType.PointerECX64 => Register.ECX,
        InstructionOperandType.PointerEDX8 => Register.EDX,
        InstructionOperandType.PointerEDX16 => Register.EDX,
        InstructionOperandType.PointerEDX32 => Register.EDX,
        InstructionOperandType.PointerEDX64 => Register.EDX,
        InstructionOperandType.PointerRAX8 => Register.RAX,
        InstructionOperandType.PointerRAX16 => Register.RAX,
        InstructionOperandType.PointerRAX32 => Register.RAX,
        InstructionOperandType.PointerRAX64 => Register.RAX,
        InstructionOperandType.PointerRBX8 => Register.RBX,
        InstructionOperandType.PointerRBX16 => Register.RBX,
        InstructionOperandType.PointerRBX32 => Register.RBX,
        InstructionOperandType.PointerRBX64 => Register.RBX,
        InstructionOperandType.PointerRCX8 => Register.RCX,
        InstructionOperandType.PointerRCX16 => Register.RCX,
        InstructionOperandType.PointerRCX32 => Register.RCX,
        InstructionOperandType.PointerRCX64 => Register.RCX,
        InstructionOperandType.PointerRDX8 => Register.RDX,
        InstructionOperandType.PointerRDX16 => Register.RDX,
        InstructionOperandType.PointerRDX32 => Register.RDX,
        InstructionOperandType.PointerRDX64 => Register.RDX,
        _ => throw new UnreachableException(),
    };

    public static BitWidth BitwidthOfPointer(this InstructionOperandType type) => type switch
    {
        InstructionOperandType.Immediate8 => throw new InvalidOperationException(),
        InstructionOperandType.Immediate16 => throw new InvalidOperationException(),
        InstructionOperandType.Immediate32 => throw new InvalidOperationException(),
        InstructionOperandType.Immediate64 => throw new InvalidOperationException(),
        InstructionOperandType.Pointer8 => Runtime.BitWidth._8,
        InstructionOperandType.Pointer16 => Runtime.BitWidth._16,
        InstructionOperandType.Pointer32 => Runtime.BitWidth._32,
        InstructionOperandType.Register => throw new InvalidOperationException(),
        InstructionOperandType.PointerBP8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerBP16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerBP32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerBP64 => Runtime.BitWidth._64,
        InstructionOperandType.PointerSP8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerSP16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerSP32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerEAX8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerEAX16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerEAX32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerEAX64 => Runtime.BitWidth._64,
        InstructionOperandType.PointerEBX8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerEBX16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerEBX32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerEBX64 => Runtime.BitWidth._64,
        InstructionOperandType.PointerECX8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerECX16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerECX32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerECX64 => Runtime.BitWidth._64,
        InstructionOperandType.PointerEDX8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerEDX16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerEDX32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerEDX64 => Runtime.BitWidth._64,
        InstructionOperandType.PointerRAX8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerRAX16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerRAX32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerRAX64 => Runtime.BitWidth._64,
        InstructionOperandType.PointerRBX8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerRBX16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerRBX32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerRBX64 => Runtime.BitWidth._64,
        InstructionOperandType.PointerRCX8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerRCX16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerRCX32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerRCX64 => Runtime.BitWidth._64,
        InstructionOperandType.PointerRDX8 => Runtime.BitWidth._8,
        InstructionOperandType.PointerRDX16 => Runtime.BitWidth._16,
        InstructionOperandType.PointerRDX32 => Runtime.BitWidth._32,
        InstructionOperandType.PointerRDX64 => Runtime.BitWidth._64,
        _ => throw new UnreachableException(),
    };
}
