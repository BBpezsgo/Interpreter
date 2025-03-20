using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
#if UNITY_BURST
[Unity.Burst.BurstCompile]
#endif
public struct Registers
{
    [FieldOffset(0)] public int CodePointer;
    [FieldOffset(4)] public int StackPointer;
    [FieldOffset(8)] public int BasePointer;

    [FieldOffset(12)] public int EAX;
    [FieldOffset(14)] public ushort AX;
    [FieldOffset(14)] public byte AH;
    [FieldOffset(15)] public byte AL;

    [FieldOffset(16)] public int EBX;
    [FieldOffset(18)] public ushort BX;
    [FieldOffset(18)] public byte BH;
    [FieldOffset(19)] public byte BL;

    [FieldOffset(20)] public int ECX;
    [FieldOffset(22)] public ushort CX;
    [FieldOffset(22)] public byte CH;
    [FieldOffset(23)] public byte CL;

    [FieldOffset(24)] public int EDX;
    [FieldOffset(26)] public ushort DX;
    [FieldOffset(26)] public byte DH;
    [FieldOffset(27)] public byte DL;

    [FieldOffset(28)] public Flags Flags;

    public readonly int Get(Register register) => register switch
    {
        Register.CodePointer => CodePointer,
        Register.StackPointer => StackPointer,
        Register.BasePointer => BasePointer,
        Register.EAX => EAX,
        Register.AX => AX,
        Register.AH => AH,
        Register.AL => AL,
        Register.EBX => EBX,
        Register.BX => BX,
        Register.BH => BH,
        Register.BL => BL,
        Register.ECX => ECX,
        Register.CX => CX,
        Register.CH => CH,
        Register.CL => CL,
        Register.EDX => EDX,
        Register.DX => DX,
        Register.DH => DH,
        Register.DL => DL,
        Register.RAX => throw new NotImplementedException(),
        Register.RBX => throw new NotImplementedException(),
        Register.RCX => throw new NotImplementedException(),
        Register.RDX => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };
}
