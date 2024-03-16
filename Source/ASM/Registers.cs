namespace LanguageCore.ASM;

public static class Registers
{
    /*
    /// <summary>Accumulator register. Used in arithmetic operations.</summary>
    public const string RAX = "rax", EAX = "eax", AX = "ax", AL = "al";
    /// <summary>Base register (BX). Used as a pointer to data (located in segment register DS, when in segmented mode).</summary>
    public const string RBX = "rbx", EBX = "ebx", BX = "bx", BL = "bl";
    /// <summary>Counter register (CX). Used in shift/rotate instructions and loops.</summary>
    public const string RCX = "rcx", ECX = "ecx", CX = "cx", CL = "cl";
    /// <summary>Data register (DX). Used in arithmetic operations and I/O operations.</summary>
    public const string RDX = "rdx", EDX = "edx", DX = "dx", DL = "dl";
    
    /// <summary>Source Index register (SI). Used as a pointer to a source in stream operations.</summary>
    public const string RSI = "rsi", ESI = "esi", SI = "si", SIL = "sil";
    /// <summary>Destination Index register (DI). Used as a pointer to a destination in stream operations.</summary>
    public const string RDI = "rdi", EDI = "edi", DI = "di", DIL = "dil";
    
    /// <summary>Stack Base Pointer register (BP). Used to point to the base of the stack.</summary>
    public const string RBP = "rbp", EBP = "ebp", BP = "bp", BPL = "bpl";
    /// <summary>Stack Pointer register (SP). Pointer to the top of the stack.</summary>
    public const string RSP = "rsp", ESP = "esp", SP = "sp", SPL = "spl";

    public const string R8 = "r8", R8d = "r8d", R8w = "r8w", R8b = "r8b";
    public const string R9 = "r9", R9d = "r9d", R9w = "r9w", R9b = "r9b";
    public const string R10 = "r10", R10d = "r10d   ", R10w = "r10w", R10b = "r10b";
    public const string R11 = "r11", R11d = "r11d   ", R11w = "r11w", R11b = "r11b";
    public const string R12 = "r12", R12d = "r12d   ", R12w = "r12w", R12b = "r12b";
    public const string R13 = "r13", R13d = "r13d   ", R13w = "r13w", R13b = "r13b";
    public const string R14 = "r14", R14d = "r14d   ", R14w = "r14w", R14b = "r14b";
    public const string R15 = "r15", R15d = "r15d   ", R15w = "r15w", R15b = "r15b";
    */

    public static string AX => "AX";
    public static string AH => "AH";
    public static string AL => "AL";

    public static string BX => "BX";
    public static string BH => "BH";
    public static string BL => "BL";

    public static string CX => "CX";
    public static string CH => "CH";
    public static string CL => "CL";

    public static string DX => "DX";
    public static string DH => "DH";
    public static string DL => "DL";

    public static string SP => "SP";
    public static string BP => "BP";

    public static string SI => "SI";
    public static string DI => "DI";

    public static string DS => "DS";
}
