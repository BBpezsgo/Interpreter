using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[StructLayout(LayoutKind.Explicit)]
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
}
