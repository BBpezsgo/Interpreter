namespace LanguageCore.Runtime;

public enum Register
{
    _8 = 0b_0101_0000,
    _4 = 0b_0100_0000,
    _2 = 0b_0011_0000,
    _H = 0b_0010_0000,
    _L = 0b_0001_0000,

    CodePointer = 0b_0001 | _4,
    StackPointer = 0b_0010 | _4,
    BasePointer = 0b_0011 | _4,

    _A = 0b_0000_0100,
    RAX = _A | _8,
    EAX = _A | _4,
    AX = _A | _2,
    AH = _A | _H,
    AL = _A | _L,

    _B = 0b_0000_0101,
    RBX = _B | _8,
    EBX = _B | _4,
    BX = _B | _2,
    BH = _B | _H,
    BL = _B | _L,

    _C = 0b_0000_0110,
    RCX = _C | _8,
    ECX = _C | _4,
    CX = _C | _2,
    CH = _C | _H,
    CL = _C | _L,

    _D = 0b_0000_0111,
    RDX = _D | _8,
    EDX = _D | _4,
    DX = _D | _2,
    DH = _D | _H,
    DL = _D | _L,
}
