using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Win32.Console;

namespace LanguageCore.Intel;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable RCS1213 // Remove unused member declaration
#pragma warning disable RCS1089 // Use --/++ operator instead of assignment

[Flags]
enum Flags : ushort
{
    _ = 0b_0000_0000_0000_0000,
    /// <summary>
    /// Carry flag
    /// </summary>
    C = 0b_0000_0000_0000_0001,
    /// <summary>
    /// Parity flag
    /// </summary>
    P = 0b_0000_0000_0000_0100,
    /// <summary>
    /// Auxiliary carry flag
    /// </summary>
    A = 0b_0000_0000_0001_0000,
    /// <summary>
    /// Zero flag
    /// </summary>
    Z = 0b_0000_0000_0100_0000,
    /// <summary>
    /// Sign flag
    /// </summary>
    S = 0b_0000_0000_1000_0000,
    /// <summary>
    /// Trap flag
    /// </summary>
    T = 0b_0000_0001_0000_0000,
    /// <summary>
    /// Interrupt flag
    /// </summary>
    I = 0b_0000_0010_0000_0000,
    /// <summary>
    /// Direction flag
    /// </summary>
    D = 0b_0000_0100_0000_0000,
    /// <summary>
    /// Overflow flag
    /// </summary>
    O = 0b_0000_1000_0000_0000,
}

[Flags]
public enum Pin : ulong
{
    _ = 0,
    GND1 = (ulong)1 << 1,
    AD14 = (ulong)1 << 2,
    AD13 = (ulong)1 << 3,
    AD12 = (ulong)1 << 4,
    AD11 = (ulong)1 << 5,
    AD10 = (ulong)1 << 6,
    AD9 = (ulong)1 << 7,
    AD8 = (ulong)1 << 8,
    AD7 = (ulong)1 << 9,
    AD6 = (ulong)1 << 10,
    AD5 = (ulong)1 << 11,
    AD4 = (ulong)1 << 12,
    AD3 = (ulong)1 << 13,
    AD2 = (ulong)1 << 14,
    AD1 = (ulong)1 << 15,
    AD0 = (ulong)1 << 16,
    NMI = (ulong)1 << 17,
    INTR = (ulong)1 << 18,
    CLK = (ulong)1 << 19,
    GND2 = (ulong)1 << 20,
    RESET = (ulong)1 << 21,
    READY = (ulong)1 << 22,
    TEST = (ulong)1 << 23,
    INTA = (ulong)1 << 24,
    ALE = (ulong)1 << 25,
    DEN = (ulong)1 << 26,
    DT_R = (ulong)1 << 27,
    M_IO = (ulong)1 << 28,
    WR = (ulong)1 << 29,
    HLDA = (ulong)1 << 30,
    HOLD = (ulong)1 << 31,
    RD = (ulong)1 << 32,
    MN_MX = (ulong)1 << 33,
    BHE_S7 = (ulong)1 << 34,
    A19_S6 = (ulong)1 << 35,
    A18_S5 = (ulong)1 << 36,
    A17_S4 = (ulong)1 << 37,
    A16_S3 = (ulong)1 << 38,
    AD15 = (ulong)1 << 39,
    VCC = (ulong)1 << 40,
}

enum Mode : byte
{
    MemoryNoDisplacement = 0b_00,
    Memory8Displacement = 0b_01,
    Memory16Displacement = 0b_10,
    Register = 0b_11,
}

[StructLayout(LayoutKind.Explicit)]
struct Registers
{
    [FieldOffset(0)] public ushort AX;
    [FieldOffset(0)] public byte AH;
    [FieldOffset(1)] public byte AL;

    [FieldOffset(2)] public ushort BX;
    [FieldOffset(2)] public byte BH;
    [FieldOffset(3)] public byte BL;

    [FieldOffset(4)] public ushort CX;
    [FieldOffset(4)] public byte CH;
    [FieldOffset(5)] public byte CL;

    [FieldOffset(6)] public ushort DX;
    [FieldOffset(6)] public byte DH;
    [FieldOffset(7)] public byte DL;

    /// <summary>
    /// Source Index
    /// </summary>
    [FieldOffset(8)] public ushort SI;
    /// <summary>
    /// Destination Index
    /// </summary>
    [FieldOffset(10)] public ushort DI;
    /// <summary>
    /// Base Pointer
    /// </summary>
    [FieldOffset(12)] public ushort BP;
    /// <summary>
    /// Stack Pointer
    /// </summary>
    [FieldOffset(14)] public ushort SP;
    /// <summary>
    /// Instruction Pointer
    /// </summary>
    [FieldOffset(16)] public ushort IP;

    /// <summary>
    /// Code Segment
    /// </summary>
    [FieldOffset(18)] public ushort CS;
    /// <summary>
    /// Data Segment
    /// </summary>
    [FieldOffset(20)] public ushort DS;
    /// <summary>
    /// Extra Segment
    /// </summary>
    [FieldOffset(22)] public ushort ES;
    /// <summary>
    /// Stack Segment
    /// </summary>
    [FieldOffset(24)] public ushort SS;

    [FieldOffset(26)] public Flags Flags;
}

/// <summary>
/// <c>0b_W_REG</c>
/// </summary>
public enum Register : byte
{
    AL = 0b_0_000,
    CL = 0b_0_001,
    DL = 0b_0_010,
    BL = 0b_0_011,
    AH = 0b_0_100,
    CH = 0b_0_101,
    DH = 0b_0_110,
    BH = 0b_0_111,
    AX = 0b_1_000,
    CX = 0b_1_001,
    DX = 0b_1_010,
    BX = 0b_1_011,
    SP = 0b_1_100,
    BP = 0b_1_101,
    SI = 0b_1_110,
    DI = 0b_1_111,
}

[StructLayout(LayoutKind.Explicit)]
readonly struct Instruction
{
    [FieldOffset(0)] readonly byte _1;
    [FieldOffset(1)] readonly byte _2;
    [FieldOffset(2)] readonly byte _3;
    [FieldOffset(3)] readonly byte _4;
    [FieldOffset(4)] readonly byte _5;
    [FieldOffset(5)] readonly byte _6;

    public byte OpCode => (byte)(_1 & 0b_1111_1100);
    public bool D => (_1 & 0b_10) != 0;
    public bool W => (_1 & 0b_01) != 0;

    public Mode Mode => (Mode)(_2 & 0b_1100_0000);
    public byte Reg => (byte)(_2 & 0b_0011_1000);
    public byte RM => (byte)(_2 & 0b_0000_0111);

    public byte LowDisplacementData => _3;
    public byte HighDisplacementData => _4;
    public byte LowData => _5;
    public byte HighData => _6;
}

static class Extensions
{
    public static void Set(ref this Flags v, Flags flag, bool value)
    {
        v &= ~flag;
        if (value) v |= flag;
    }
    public static bool Get(ref this Flags v, Flags flag) => (v & flag) != 0;

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetCarry(ref this Flags v, int result)
    {
        Set(ref v, Flags.C, (ushort)result > 0xFF);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetCarryW(ref this Flags v, int result)
    {
        Set(ref v, Flags.C, (uint)result > 0xFFFF);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetOverflowFlag(ref this Flags v, int source, int destination)
    {
        int result = source + destination;
        Set(ref v, Flags.O, ((result ^ source) & (result ^ destination) & 0x80) == 0x80);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetOverflowFlagW(ref this Flags v, int source, int destination)
    {
        int result = source + destination;
        Set(ref v, Flags.O, ((result ^ source) & (result ^ destination) & 0x8000) == 0x8000);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetOverflowSubtract(ref this Flags v, int source, int destination)
    {
        int result = destination - source;
        Set(ref v, Flags.O, ((result ^ destination) & (source ^ destination) & 0x80) == 0x80);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetOverflowSubtractW(ref this Flags v, int source, int destination)
    {
        int result = destination - source;
        Set(ref v, Flags.O, ((result ^ destination) & (source ^ destination) & 0x8000) == 0x8000);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetAuxCarry(ref this Flags v, int source, int destination)
    {
        int result = source + destination;
        Set(ref v, Flags.A, ((source ^ destination ^ result) & 0x10) == 0x10);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetZeroFlag(ref this Flags v, int result)
    {
        Set(ref v, Flags.Z, (result & 0xFF) == 0);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetZeroFlagW(ref this Flags v, int result)
    {
        Set(ref v, Flags.Z, (result & 0xFFFF) == 0);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetSignFlag(ref this Flags v, int result)
    {
        Set(ref v, Flags.S, (result & 0x80) == 0x80);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetSignFlagW(ref this Flags v, int result)
    {
        Set(ref v, Flags.S, (result & 0x8000) == 0x8000);
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetParityFlag(ref this Flags v, int result)
    {
        byte num = (byte)(result & 0xFF);

        byte total;
        for (total = 0; num > 0; total++)
        { num &= (byte)(num - 1); }

        Set(ref v, Flags.P, (total % 2) == 0);
    }
}

public unsafe struct I8086
{
    public bool IsHalted;
    Registers Registers;
    // Pin Pins;

    readonly byte[] Memory;
    readonly ConsoleRenderer Console;

    [SupportedOSPlatform("windows")]
    public I8086(string outputFile)
    {
        Memory = new byte[1024 * 1024 * 4];
        /* C:\Users\bazsi\Desktop\ahh\noname.bin (2024. 03. 14. 6:06:00)
    Kezdőpozíció(h): 00000000, Végpozíció(h): 0000001F, Hossz(h): 00000020 */

        Registers.DS = (1024 * 0) >> 4;
        Registers.SS = (1024 * 1) >> 4;
        Registers.CS = (1024 * 2) >> 4;
        Registers.ES = Registers.DS; // (1024 * 3) >> 4;

        Registers.SP = 1024;

        Console = new ConsoleRenderer(80, 25);

        byte[] rawData = File.ReadAllBytes(outputFile);

        uint dst = GetPhysicalAddress(Registers.CS, 0);
        Array.Copy(rawData, 0, Memory, dst, rawData.Length);
    }

    public void Reset()
    {
        Registers.Flags = Flags._;
        Registers.IP = 0x0000;
        Registers.CS = 0xFFFF;
        Registers.DS = 0x0000;
        Registers.SS = 0x0000;
        Registers.ES = 0x0000;
    }

    const uint MaxPhysicalAddress = (ushort.MaxValue << 4) + ushort.MaxValue;
    static uint GetPhysicalAddress(ushort segment, ushort offset) => (uint)unchecked((segment << 4) + offset);

    readonly int GetEffectiveAddress(byte rm, byte mode, byte displacementLow, byte displacementHigh)
    {
        int ea = rm switch
        {
            0b_000 => Registers.BX + Registers.SI,
            0b_001 => Registers.BX + Registers.DI,
            0b_010 => Registers.BP + Registers.SI,
            0b_011 => Registers.BP + Registers.DI,
            0b_100 => Registers.SI,
            0b_101 => Registers.DI,
            0b_110 => Registers.BP,
            0b_111 => Registers.BX,
            _ => throw new UnreachableException(),
        };

        ea += mode switch
        {
            0b_00 => 0,
            0b_01 => displacementLow,
            0b_10 => (displacementLow << 8) | displacementHigh,
            _ => throw new UnreachableException(),
        };

        return ea;
    }

    void StoreRegister(byte register, byte data)
    {
        switch (register)
        {
            case 0b_000: Registers.AL = data; break;
            case 0b_001: Registers.CL = data; break;
            case 0b_010: Registers.DL = data; break;
            case 0b_011: Registers.BL = data; break;
            case 0b_100: Registers.AH = data; break;
            case 0b_101: Registers.CH = data; break;
            case 0b_110: Registers.DH = data; break;
            case 0b_111: Registers.BH = data; break;
            default: throw new Exception();
        }
    }

    void StoreRegisterW(byte register, byte low, byte high) => StoreRegisterW(register, (ushort)((high << 8) | low));
    void StoreRegisterW(byte register, ushort data)
    {
        switch (register)
        {
            case 0b_000: Registers.AX = data; break;
            case 0b_001: Registers.CX = data; break;
            case 0b_010: Registers.DX = data; break;
            case 0b_011: Registers.BX = data; break;
            case 0b_100: Registers.SP = data; break;
            case 0b_101: Registers.BP = data; break;
            case 0b_110: Registers.SI = data; break;
            case 0b_111: Registers.DI = data; break;
            default: throw new Exception();
        }
    }

    void StoreSegmentRegisterW(byte register, ushort data)
    {
        switch (register)
        {
            case 0b_00: Registers.ES = data; break;
            case 0b_01: Registers.CS = data; break;
            case 0b_10: Registers.SS = data; break;
            case 0b_11: Registers.DS = data; break;
            default: throw new Exception();
        }
    }

    readonly byte FetchRegister(byte register) => register switch
    {
        0b_000 => Registers.AL,
        0b_001 => Registers.CL,
        0b_010 => Registers.DL,
        0b_011 => Registers.BL,
        0b_100 => Registers.AH,
        0b_101 => Registers.CH,
        0b_110 => Registers.DH,
        0b_111 => Registers.BH,
        _ => throw new Exception(),
    };

    readonly ushort FetchRegisterW(byte register) => register switch
    {
        0b_000 => Registers.AX,
        0b_001 => Registers.CX,
        0b_010 => Registers.DX,
        0b_011 => Registers.BX,
        0b_100 => Registers.SP,
        0b_101 => Registers.BP,
        0b_110 => Registers.SI,
        0b_111 => Registers.DI,
        _ => throw new Exception(),
    };

    readonly ushort FetchRegister(byte register, bool word) => word ? FetchRegisterW(register) : FetchRegister(register);

    static bool NeedDLow(byte rm, byte mode)
    {
        if (mode == 0b_11)
        { return false; }

        if (rm == 0b_110 && mode == 0)
        { throw new NotImplementedException(); }

        return mode is 0b_01 or 0b_10;
    }

    static bool NeedDHigh(byte rm, byte mode)
    {
        if (mode == 0b_11)
        { return false; }

        if (rm == 0b_110 && mode == 0)
        { throw new NotImplementedException(); }

        return mode == 0b_10;
    }

    readonly byte Load(byte rm, byte mode, byte displacementLow, byte displacementHigh)
    {
        if (mode == 0b_11)
        { return FetchRegister(rm); }

        if (rm == 0b_110 && mode == 0)
        { throw new NotImplementedException(); }

        int ea = GetEffectiveAddress(rm, mode, displacementLow, displacementHigh);

        return Memory[ea];
    }

    readonly ushort LoadW(byte rm, byte mode, byte displacementLow, byte displacementHigh)
    {
        if (mode == 0b_11)
        { return FetchRegisterW(rm); }

        if (rm == 0b_110 && mode == 0)
        { throw new NotImplementedException(); }

        int ea = GetEffectiveAddress(rm, mode, displacementLow, displacementHigh);

        return BitConverter.ToUInt16(Memory, ea);
    }

    void Store(byte rm, byte mode, byte displacementLow, byte displacementHigh, byte data)
    {
        if (mode == 0b_11)
        {
            StoreRegister(rm, data);
            return;
        }

        if (rm == 0b_110 && mode == 0)
        { throw new NotImplementedException(); }

        int ea = GetEffectiveAddress(rm, mode, displacementLow, displacementHigh);

        Memory[ea] = data;
    }

    void StoreW(byte rm, byte mode, byte displacementLow, byte displacementHigh, ushort data)
    {
        if (mode == 0b_11)
        {
            StoreRegisterW(rm, data);
            return;
        }

        if (rm == 0b_110 && mode == 0)
        { throw new NotImplementedException(); }

        int ea = GetEffectiveAddress(rm, mode, displacementLow, displacementHigh);

        Memory[ea] = (byte)(data >> 8);
        Memory[ea + 1] = (byte)(data & 0xFF);
    }

    readonly byte ReadMemory(ushort segment, ushort offset)
    {
        uint address = GetPhysicalAddress(segment, offset);
        return Memory[address];
    }

    readonly ushort ReadMemoryW(ushort segment, ushort offset)
    {
        uint address = GetPhysicalAddress(segment, offset);
        return BitConverter.ToUInt16(Memory, (int)address);
    }

    readonly void WriteMemory(ushort segment, ushort offset, byte data)
    {
        uint address = GetPhysicalAddress(segment, offset);
        Memory[address] = data;
    }

    readonly void WriteMemoryW(ushort segment, ushort offset, ushort data)
    {
        uint address = GetPhysicalAddress(segment, offset);
        Memory[address] = (byte)(data >> 8);
        Memory[address + 1] = (byte)(data & 0xFF);
    }

    readonly void WriteMemory(ushort segment, ushort offset, ReadOnlySpan<byte> data)
    {
        uint address = GetPhysicalAddress(segment, offset);
        for (int i = 0; i < data.Length; i++)
        { Memory[address + i] = data[i]; }
    }

    (byte Low, byte High) ReadDisplacement(byte rm, byte mod)
    {
        byte low = NeedDLow(rm, mod) ? ReadMemory(Registers.CS, Registers.IP++) : default;
        byte high = NeedDHigh(rm, mod) ? ReadMemory(Registers.CS, Registers.IP++) : default;

        return (low, high);
    }

    (byte Mode, byte Reg, byte RM) ReadExtension()
    {
        byte ext = ReadMemory(Registers.CS, Registers.IP++);
        byte mode = (byte)((ext & 0b_11_000_000) >> 6);
        byte reg = (byte)((ext & 0b_00_111_000) >> 3);
        byte rm = (byte)((ext & 0b_00_000_111) >> 0);
        return (mode, reg, rm);
    }

    [SupportedOSPlatform("windows")]
    public void Clock()
    {
        Do();

        for (int i = 0; i < Console.Buffer.Length; i++)
        { Console.Buffer[i] = new ConsoleChar((char)Memory[i * 2], (ushort)Memory[(i * 2) + 1]); }

        Console.Render();
    }

    void Do()
    {
        byte inst = ReadMemory(Registers.CS, Registers.IP++);

        #region 0b_11111111

        switch (inst)
        {
            // DAA
            case 0b_00100111:
            {
                throw new NotImplementedException();
            }

            // DAS
            case 0b_00101111:
            {
                throw new NotImplementedException();
            }

            // AAA
            case 0b_00110111:
            {
                throw new NotImplementedException();
            }

            // AAS
            case 0b_00111111:
            {
                throw new NotImplementedException();
            }

            // JO
            case 0b_01110000:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (Registers.Flags.Get(Flags.O))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JNO
            case 0b_01110001:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (!Registers.Flags.Get(Flags.O))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JB/JNAE
            case 0b_01110010:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (Registers.Flags.Get(Flags.C))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JNB/JAE
            case 0b_01110011:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (!Registers.Flags.Get(Flags.C))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JE/JZ
            case 0b_01110100:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (Registers.Flags.Get(Flags.Z))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JNE/JNZ
            case 0b_01110101:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (!Registers.Flags.Get(Flags.Z))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JBE/JNA
            case 0b_01110110:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (Registers.Flags.Get(Flags.C) || Registers.Flags.Get(Flags.Z))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JNBE/JA
            case 0b_01110111:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (!(Registers.Flags.Get(Flags.C) || Registers.Flags.Get(Flags.Z)))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JS
            case 0b_01111000:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (Registers.Flags.Get(Flags.S))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JNS
            case 0b_01111001:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                throw new NotImplementedException();
            }

            // JP/JPE
            case 0b_01111010:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (Registers.Flags.Get(Flags.P))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JNP/JPO
            case 0b_01111011:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (!Registers.Flags.Get(Flags.P))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JL/JNGE
            case 0b_01111100:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (Registers.Flags.Get(Flags.S) ^ Registers.Flags.Get(Flags.O))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JNL/JGE
            case 0b_01111101:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (!(Registers.Flags.Get(Flags.S) ^ Registers.Flags.Get(Flags.O)))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JLE/JNG
            case 0b_01111110:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if ((Registers.Flags.Get(Flags.S) ^ Registers.Flags.Get(Flags.O)) || Registers.Flags.Get(Flags.Z))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // JNLE/JG
            case 0b_01111111:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                if (!((Registers.Flags.Get(Flags.S) ^ Registers.Flags.Get(Flags.O)) || Registers.Flags.Get(Flags.Z)))
                {
                    Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));
                }
                return;
            }

            // MOVE Segment register to register/memory
            case 0b_10001100:
            {
                throw new NotImplementedException();
            }

            // LEA
            case 0b_10001101:
            {
                throw new NotImplementedException();
            }

            // MOVE Register/memory to segment register
            case 0b_10001110:
            {
                (byte mod, byte reg, byte rm) = ReadExtension();
                (byte low, byte high) = ReadDisplacement(rm, mod);
                ushort data = LoadW(rm, mod, low, high);
                reg &= 0b_11;
                StoreSegmentRegisterW(reg, data);
                return;
            }

            // POP Register/memory
            case 0b_10001111:
            {
                throw new NotImplementedException();
            }

            // CBW
            case 0b_10011000:
            {
                throw new NotImplementedException();
            }

            // CWD
            case 0b_10011001:
            {
                throw new NotImplementedException();
            }

            // CALL Direct intersegment
            case 0b_10011010:
            {
                throw new NotImplementedException();
            }

            // WAIT
            case 0b_10011011:
            {
                throw new NotImplementedException();
            }

            // PUSHF
            case 0b_10011100:
            {
                throw new NotImplementedException();
            }

            // POPF
            case 0b_10011101:
            {
                throw new NotImplementedException();
            }

            // SAHF
            case 0b_10011110:
            {
                throw new NotImplementedException();
            }

            // LAHF
            case 0b_10011111:
            {
                throw new NotImplementedException();
            }

            // RET Within segment adding immediate to SP
            case 0b_11000010:
            {
                byte low = ReadMemory(Registers.CS, Registers.IP++);
                byte high = ReadMemory(Registers.CS, Registers.IP++);
                throw new NotImplementedException();
            }

            // RET Within segment
            case 0b_11000011:
            {
                ushort data = ReadMemoryW(Registers.SS, Registers.SP);
                Registers.IP = data;
                Registers.SP += 2;
                return;
            }

            // LES
            case 0b_11000100:
            {
                throw new NotImplementedException();
            }

            // LDS
            case 0b_11000101:
            {
                throw new NotImplementedException();
            }

            // RET Intersegment adding immediate to SP
            case 0b_11001010:
            {
                byte low = ReadMemory(Registers.CS, Registers.IP++);
                byte high = ReadMemory(Registers.CS, Registers.IP++);
                throw new NotImplementedException();
            }

            // RET Intersegment
            case 0b_11001011:
            {
                throw new NotImplementedException();
            }

            // INT Type 3
            case 0b_11001100:
            {
                throw new NotImplementedException();
            }

            // INT Type specified
            case 0b_11001101:
            {
                byte data = ReadMemory(Registers.CS, Registers.IP++);
                throw new NotImplementedException();
            }

            // INTO
            case 0b_11001110:
            {
                throw new NotImplementedException();
            }

            // INTO
            case 0b_11001111:
            {
                throw new NotImplementedException();
            }

            // AAM
            case 0b_11010100:
            {
                throw new NotImplementedException();
            }

            // AAD
            case 0b_11010101:
            {
                throw new NotImplementedException();
            }

            // XLAT
            case 0b_11010111:
            {
                throw new NotImplementedException();
            }

            // LOOPNZ/LOOPNE
            case 0b_11100000:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                throw new NotImplementedException();
            }

            // LOOPZ/LOOPE
            case 0b_11100001:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                throw new NotImplementedException();
            }

            // LOOP
            case 0b_11100010:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                throw new NotImplementedException();
            }

            // JCXZ
            case 0b_11100011:
            {
                sbyte ipIncrement = unchecked((sbyte)ReadMemory(Registers.CS, Registers.IP++));
                throw new NotImplementedException();
            }

            // CALL Direct within segment
            case 0b_11101000:
            {
                short ipIncrement = unchecked((short)ReadMemoryW(Registers.CS, Registers.IP));
                Registers.IP += 2;

                Registers.SP -= 2;
                WriteMemoryW(Registers.SS, Registers.SP, Registers.IP);

                Registers.IP = unchecked((ushort)(Registers.IP + ipIncrement));

                return;
            }

            // JMP Direct within segment
            case 0b_11101001:
            {
                byte low = ReadMemory(Registers.CS, Registers.IP++);
                byte high = ReadMemory(Registers.CS, Registers.IP++);
                throw new NotImplementedException();
            }

            // JMP Direct intersegment
            case 0b_11101010:
            {
                throw new NotImplementedException();
            }

            // JMP Direct within segment-short
            case 0b_11101011:
            {
                byte inc = ReadMemory(Registers.CS, Registers.IP++);
                throw new NotImplementedException();
            }

            // LOCK
            case 0b_11110000:
            {
                throw new NotImplementedException();
            }

            // HLT
            case 0b_11110100:
            {
                Registers.IP--;
                IsHalted = true;
                return;
            }

            // CMC
            case 0b_11110101:
            {
                throw new NotImplementedException();
            }

            // CLC
            case 0b_11111000:
            {
                throw new NotImplementedException();
            }

            // STC
            case 0b_11111001:
            {
                throw new NotImplementedException();
            }

            // CLI
            case 0b_11111010:
            {
                throw new NotImplementedException();
            }

            // STI
            case 0b_11111011:
            {
                throw new NotImplementedException();
            }

            // CLD
            case 0b_11111100:
            {
                Registers.Flags &= ~Flags.D;
                return;
            }

            // STD
            case 0b_11111101:
            {
                throw new NotImplementedException();
            }

            case 0b_11111111:
            {
                (byte mod, byte subInstruction, byte rm) = ReadExtension();

                switch (subInstruction)
                {
                    // PUSH Register/memory
                    case 0b_110:
                    {
                        throw new NotImplementedException();
                    }

                    // CALL Indirect within segment
                    case 0b_010:
                    {
                        throw new NotImplementedException();
                    }

                    // CALL Indirect intersegment
                    case 0b_011:
                    {
                        throw new NotImplementedException();
                    }

                    // JMP Indirect within segment
                    case 0b_100:
                    {
                        throw new NotImplementedException();
                    }

                    // JMP Indirect intersegment
                    case 0b_101:
                    {
                        throw new NotImplementedException();
                    }
                }

                throw new NotImplementedException();
            }
        }

        #endregion

        #region 0b_111111_00

        switch (inst & 0b_111111_00)
        {
            // ADD Register/memory with register to either
            case 0b_000000_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                (byte mod, byte reg, byte rm) = ReadExtension();
                (byte low, byte high) = ReadDisplacement(rm, mod);

                if (d)
                {
                    if (w)
                    {
                        ushort src = LoadW(rm, mod, low, high);
                        ushort dst = FetchRegisterW(reg);

                        byte res = (byte)(src + dst);

                        Registers.Flags.Set(Flags.O, false);
                        Registers.Flags.Set(Flags.C, false);

                        Registers.Flags.SetParityFlag(res);

                        Registers.Flags.SetSignFlagW(res);
                        Registers.Flags.SetZeroFlagW(res);

                        StoreRegisterW(reg, res);
                    }
                    else
                    {
                        byte src = Load(rm, mod, low, high);
                        byte dst = FetchRegister(reg);

                        byte res = (byte)(src + dst);

                        Registers.Flags.Set(Flags.O, false);
                        Registers.Flags.Set(Flags.C, false);

                        Registers.Flags.SetParityFlag(res);

                        Registers.Flags.SetSignFlag(res);
                        Registers.Flags.SetZeroFlag(res);

                        StoreRegister(reg, res);
                    }
                }
                else
                {
                    if (w)
                    {
                        ushort src = FetchRegisterW(reg);
                        ushort dst = LoadW(rm, mod, low, high);

                        byte res = (byte)(src + dst);

                        Registers.Flags.Set(Flags.O, false);
                        Registers.Flags.Set(Flags.C, false);

                        Registers.Flags.SetParityFlag(res);

                        Registers.Flags.SetSignFlagW(res);
                        Registers.Flags.SetZeroFlagW(res);

                        StoreW(rm, mod, low, high, res);
                    }
                    else
                    {
                        byte src = FetchRegister(reg);
                        byte dst = Load(rm, mod, low, high);

                        byte res = (byte)(src + dst);

                        Registers.Flags.Set(Flags.O, false);
                        Registers.Flags.Set(Flags.C, false);

                        Registers.Flags.SetParityFlag(res);

                        Registers.Flags.SetSignFlag(res);
                        Registers.Flags.SetZeroFlag(res);

                        Store(rm, mod, low, high, res);
                    }
                }

                return;
            }

            // OR Register/memory and register to either
            case 0b_000010_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                (byte mod, byte reg, byte rm) = ReadExtension();
                (byte low, byte high) = ReadDisplacement(rm, mod);

                if (d)
                {
                    if (w)
                    {
                        ushort src = LoadW(rm, mod, low, high);
                        ushort dst = FetchRegisterW(reg);

                        byte res = (byte)(src | dst);

                        Registers.Flags.Set(Flags.O, false);
                        Registers.Flags.Set(Flags.C, false);

                        Registers.Flags.SetParityFlag(res);

                        Registers.Flags.SetSignFlagW(res);
                        Registers.Flags.SetZeroFlagW(res);

                        StoreRegisterW(reg, res);
                    }
                    else
                    {
                        byte src = Load(rm, mod, low, high);
                        byte dst = FetchRegister(reg);

                        byte res = (byte)(src | dst);

                        Registers.Flags.Set(Flags.O, false);
                        Registers.Flags.Set(Flags.C, false);

                        Registers.Flags.SetParityFlag(res);

                        Registers.Flags.SetSignFlag(res);
                        Registers.Flags.SetZeroFlag(res);

                        StoreRegister(reg, res);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }

                return;
            }

            // ADC Register/memory with register to either
            case 0b_000100_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                throw new NotImplementedException();
            }

            // TEST Register/memory and register
            /*
            case 0b_000100_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                (byte mod, byte reg, byte rm) = ReadExtension();
                (byte low, byte high) = ReadDisplacement(rm, mod);
                throw new NotImplementedException();
            }
            */

            // SBB Register/memory and register to either
            case 0b_000110_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                throw new NotImplementedException();
            }

            // AND Register/memory with register to either
            case 0b_001000_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                (byte mod, byte reg, byte rm) = ReadExtension();
                (byte low, byte high) = ReadDisplacement(rm, mod);
                throw new NotImplementedException();
            }

            // SUB Register/memory and register to either
            case 0b_001010_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                throw new NotImplementedException();
            }

            // XOR Register/memory and register to either
            case 0b_001100_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                (byte mod, byte reg, byte rm) = ReadExtension();
                (byte low, byte high) = ReadDisplacement(rm, mod);
                throw new NotImplementedException();
            }

            // CMP Register/memory and register
            case 0b_001110_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                throw new NotImplementedException();
            }

            case 0b_100000_00:
            {
                bool s = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                (byte mod, byte subInstruction, byte rm) = ReadExtension();
                switch (subInstruction)
                {
                    // ADD Immediate to register/memory
                    case 0b_000:
                    {
                        (byte low, byte high) = ReadDisplacement(rm, mod);
                        if (!s && w)
                        {
                            ushort data = ReadMemoryW(Registers.CS, Registers.IP);
                            Registers.IP += 2;
                            StoreW(rm, mod, low, high, data);
                        }
                        else
                        {
                            byte data = ReadMemory(Registers.CS, Registers.IP);
                            Registers.IP += 1;
                            Store(rm, mod, low, high, data);
                        }
                        return;
                    }

                    // ADC Immediate to register/memory
                    case 0b_010:
                    {
                        throw new NotImplementedException();
                    }

                    // SBB Immediate from register/memory
                    case 0b_011:
                    {
                        throw new NotImplementedException();
                    }

                    // SUB Immediate from register/memory
                    case 0b_101:
                    {
                        throw new NotImplementedException();
                    }

                    // CMP Immediate with register/memory
                    case 0b_111:
                    {
                        throw new NotImplementedException();
                    }
                }

                throw new NotImplementedException();
            }

            // MOVE Register/memory to/from register
            case 0b_100010_00:
            {
                bool d = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                (byte mod, byte reg, byte rm) = ReadExtension();
                (byte low, byte high) = ReadDisplacement(rm, mod);

                if (d)
                {
                    if (w)
                    {
                        ushort data = LoadW(rm, mod, low, high);
                        StoreRegisterW(reg, data);
                    }
                    else
                    {
                        byte data = Load(rm, mod, low, high);
                        StoreRegister(reg, data);
                    }
                }
                else
                {
                    if (w)
                    {
                        ushort data = FetchRegisterW(reg);
                        StoreW(rm, mod, low, high, data);
                    }
                    else
                    {
                        byte data = FetchRegister(reg);
                        Store(rm, mod, low, high, data);
                    }
                }

                return;
            }

            case 0b_110100_00:
            {
                bool v = (inst & 0b_10) != 0;
                bool w = (inst & 0b_01) != 0;
                (byte mod, byte subInstruction, byte rm) = ReadExtension();

                switch (subInstruction)
                {
                    // ROL
                    case 0b_000:
                    {
                        throw new NotImplementedException();
                    }

                    // ROR
                    case 0b_001:
                    {
                        throw new NotImplementedException();
                    }

                    // RCL
                    case 0b_010:
                    {
                        throw new NotImplementedException();
                    }

                    // RCR
                    case 0b_011:
                    {
                        throw new NotImplementedException();
                    }

                    // SHL/SAL
                    case 0b_100:
                    {
                        throw new NotImplementedException();
                    }

                    // SHR
                    case 0b_101:
                    {
                        throw new NotImplementedException();
                    }

                    case 0b_110: throw new UnreachableException();

                    // SAR
                    case 0b_111:
                    {
                        throw new NotImplementedException();
                    }
                }

                throw new UnreachableException();
            }
        }

        #endregion

        #region 0b_1111111_0

        switch (inst & 0b_1111111_0)
        {
            // ADD Immediate to accumulator
            case 0b_0000010_0:
            {
                bool w = (inst & 0b_1) != 0;
                if (w)
                {
                    ushort data = ReadMemoryW(Registers.CS, Registers.IP);
                    Registers.IP += sizeof(ushort);

                    int res = Registers.AX + data;

                    Registers.Flags.SetAuxCarry(data, Registers.AX);
                    Registers.Flags.SetOverflowFlagW(data, Registers.AX);

                    Registers.Flags.SetParityFlag(res);

                    Registers.Flags.SetCarryW(res);
                    Registers.Flags.SetZeroFlagW(res);
                    Registers.Flags.SetSignFlagW(res);

                    Registers.AX = unchecked((ushort)res);
                }
                else
                {
                    byte data = ReadMemory(Registers.CS, Registers.IP);
                    Registers.IP += sizeof(byte);

                    int res = Registers.AX + data;

                    Registers.Flags.SetAuxCarry(data, Registers.AX);
                    Registers.Flags.SetOverflowFlag(data, Registers.AX);

                    Registers.Flags.SetParityFlag(res);

                    Registers.Flags.SetCarry(res);
                    Registers.Flags.SetZeroFlag(res);
                    Registers.Flags.SetSignFlag(res);

                    Registers.AL = unchecked((byte)res);
                }
                return;
            }

            // OR Immediate to accumulator
            case 0b_0000110_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // ADC Immediate to accumulator
            case 0b_0001010_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // SBB Immediate from accumulator
            case 0b_0001110_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // AND Immediate to accumulator
            case 0b_0010010_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // SUB Immediate from accumulator
            case 0b_0010110_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // XOR Immediate to register/memory
            case 0b_0011010_0:
            {
                bool w = (inst & 0b_01) != 0;
                throw new NotImplementedException();
            }

            // XOR Immediate to accumulator
            /*
            case 0b_0011010_0:
            {
                bool w = (inst & 0b_01) != 0;
                throw new NotImplementedException();
            }
            */

            // CMP Immediate with accumulator
            case 0b_0011110_0:
            {
                bool w = (inst & 0b_1) != 0;
                if (w)
                {
                    ushort a = ReadMemoryW(Registers.CS, Registers.IP++);
                    ushort b = Registers.AX;
                    int res = b - a;

                    Registers.Flags.SetAuxCarry(a, b);
                    Registers.Flags.SetOverflowSubtractW(a, b);

                    Registers.Flags.SetParityFlag(res);

                    Registers.Flags.SetZeroFlagW(res);
                    Registers.Flags.SetSignFlagW(res);
                    Registers.Flags.SetZeroFlagW(res);
                }
                else
                {
                    byte a = ReadMemory(Registers.CS, Registers.IP++);
                    byte b = Registers.AL;
                    int res = b - a;

                    Registers.Flags.SetAuxCarry(a, b);
                    Registers.Flags.SetOverflowSubtract(a, b);

                    Registers.Flags.SetParityFlag(res);

                    Registers.Flags.SetZeroFlag(res);
                    Registers.Flags.SetSignFlag(res);
                    Registers.Flags.SetZeroFlag(res);
                }
                return;
            }

            case 0b_1000000_0:
            {
                bool w = (inst & 0b_1) != 0;
                (byte mod, byte subInstruction, byte rm) = ReadExtension();
                switch (subInstruction)
                {
                    // AND Immediate to register/memory
                    case 0b_100:
                    {
                        throw new NotImplementedException();
                    }

                    // OR Immediate to register/memory
                    case 0b_001:
                    {
                        throw new NotImplementedException();
                    }
                }

                throw new NotImplementedException();
            }

            // XCHG Register/memory with register
            case 0b_1000011_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // MOVE Memory to accumulator
            case 0b_1010000_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // MOVE Accumulator to memory
            case 0b_1010001_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // MOVS
            case 0b_1010010_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // CMPS
            case 0b_1010011_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // TEST Immediate data and accumulator
            case 0b_1010100_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // STDS
            case 0b_1010101_0:
            {
                if ((inst & 0b_1) != 0)
                {
                    WriteMemoryW(Registers.ES, Registers.DI, Registers.AX);
                    Registers.DI += sizeof(ushort);
                }
                else
                {
                    WriteMemory(Registers.ES, Registers.DI, Registers.AL);
                    Registers.DI += sizeof(byte);
                }
                return;
            }

            // LODS
            case 0b_1010110_0:
            {
                if ((inst & 0b_1) != 0)
                {
                    Registers.AX = ReadMemoryW(Registers.ES, Registers.SI);
                    Registers.SI += sizeof(ushort);
                }
                else
                {
                    Registers.AL = ReadMemory(Registers.ES, Registers.SI);
                    Registers.SI += sizeof(byte);
                }
                return;
            }

            // SCAS
            case 0b_1010111_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // MOVE Immediate to register/memory
            case 0b_1100011_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // IN Fixed port
            case 0b_1110010_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // OUT Fixed port
            case 0b_1110011_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // IN Variable port
            case 0b_1110110_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // OUT Variable port
            case 0b_1110111_0:
            {
                bool w = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            // REP
            case 0b_1111001_0:
            {
                bool z = (inst & 0b_1) != 0;
                throw new NotImplementedException();
            }

            case 0b_1111011_0:
            {
                (byte mod, byte subInstruction, byte rm) = ReadExtension();

                switch (subInstruction)
                {
                    // TEST Immediate data and register/memory
                    case 0b_000:
                    {
                        bool w = (inst & 0b_1) != 0;
                        throw new NotImplementedException();
                    }

                    case 0b_001: throw new UnreachableException();

                    // NOT
                    case 0b_010:
                    {
                        bool w = (inst & 0b_1) != 0;
                        throw new NotImplementedException();
                    }
                    // NEG
                    case 0b_011:
                    {
                        bool w = (inst & 0b_1) != 0;
                        throw new NotImplementedException();
                    }

                    // MUL
                    case 0b_100:
                    {
                        bool w = (inst & 0b_1) != 0;
                        throw new NotImplementedException();
                    }

                    // IMUL
                    case 0b_101:
                    {
                        bool w = (inst & 0b_1) != 0;
                        throw new NotImplementedException();
                    }

                    // DIV
                    case 0b_110:
                    {
                        bool w = (inst & 0b_1) != 0;
                        throw new NotImplementedException();
                    }

                    // IDIV
                    case 0b_111:
                    {
                        bool w = (inst & 0b_1) != 0;
                        throw new NotImplementedException();
                    }
                }

                throw new UnreachableException();
            }

            case 0b_1111111_0:
            {
                bool w = (inst & 0b_1) != 0;
                (byte mod, byte subInstruction, byte rm) = ReadExtension();
                switch (subInstruction)
                {
                    // INC Register/memory
                    case 0b_000:
                    {
                        throw new NotImplementedException();
                    }

                    // DEC Register/memory
                    case 0b_001:
                    {
                        throw new NotImplementedException();
                    }
                }

                throw new NotImplementedException();
            }
        }

        #endregion

        #region 0b_11111_000

        switch (inst & 0b_11111_000)
        {
            // INC
            case 0b_01000_000:
            {
                byte reg = (byte)(inst & 0b_111);
                throw new NotImplementedException();
            }

            // DEC
            case 0b_01001_000:
            {
                byte reg = (byte)(inst & 0b_111);
                throw new NotImplementedException();
            }

            // PUSH Register
            case 0b_01010_000:
            {
                byte reg = (byte)(inst & 0b_111);
                ushort data = FetchRegisterW(reg);
                Registers.SP -= 2;
                WriteMemoryW(Registers.SS, Registers.SP, data);
                return;
            }

            // POP Register
            case 0b_01011_000:
            {
                byte reg = (byte)(inst & 0b_111);
                ushort data = ReadMemoryW(Registers.SS, Registers.SP);
                Registers.SP += 2;
                StoreRegisterW(reg, data);
                return;
            }

            // XCHG Register with accumulator
            case 0b_10010_000:
            {
                byte reg = (byte)(inst & 0b_111);
                throw new NotImplementedException();
            }

            // ESC
            case 0b_11011_000:
            {
                byte xxx = (byte)(inst & 0b_111);
                throw new NotImplementedException();
            }
        }

        #endregion

        // PUSH Segment register
        if ((inst & 0b_111_000_111) == 0b_000_000_110)
        {
            byte reg = (byte)(inst & (0b_000_111_000 >> 3));
            throw new NotImplementedException();
        }

        // POP Segment register
        if ((inst & 0b_111_000_111) == 0b_000_000_111)
        {
            byte reg = (byte)(inst & (0b_000_111_000 >> 3));
            throw new NotImplementedException();
        }

        // MOVE Immediate to register
        if ((inst & 0b_1111_0_000) == 0b_1011_0_000)
        {
            bool w = (inst & 0b_1_000) != 0;
            byte reg = (byte)(inst & 0b_111);

            if (w)
            {
                ushort data = ReadMemoryW(Registers.CS, Registers.IP);
                StoreRegisterW(reg, data);

                Registers.IP += 2;
            }
            else
            {
                byte data = ReadMemory(Registers.CS, Registers.IP);
                StoreRegister(reg, data);

                Registers.IP += 1;
            }
            return;
        }

        // SEGMENT
        if ((inst & 0b_111_00_111) == 0b_001_00_110)
        {
            byte reg = (byte)((inst & 0b_000_11_000) >> 3);
            throw new NotImplementedException();
        }

        // PUSH Immediate CUSTOM
        if (inst == 0x6a)
        {
            byte data = ReadMemory(Registers.CS, Registers.IP++);
            Registers.SP -= 1;
            WriteMemory(Registers.SS, Registers.SP, data);
            return;
        }

        throw new NotImplementedException();
    }
}
