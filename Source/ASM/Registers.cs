namespace LanguageCore.ASM;

[ExcludeFromCodeCoverage]
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
