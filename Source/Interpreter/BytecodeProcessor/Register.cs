namespace LanguageCore.Runtime;

public enum Register
{
    CodePointer = 0b_00_00_001,
    StackPointer = 0b_00_00_010,
    BasePointer = 0b_00_00_011,

    EAX = 0b_00_00_100,
    AX = 0b_00_01_100,
    AH = 0b_00_10_100,
    AL = 0b_00_11_100,

    EBX = 0b_01_00_100,
    BX = 0b_01_01_100,
    BH = 0b_01_10_100,
    BL = 0b_01_11_100,

    ECX = 0b_10_00_100,
    CX = 0b_10_01_100,
    CH = 0b_10_10_100,
    CL = 0b_10_11_100,

    EDX = 0b_11_00_100,
    DX = 0b_11_01_100,
    DH = 0b_11_10_100,
    DL = 0b_11_11_100,

    RAX,
    RBX,
    RCX,
    RDX,
}
