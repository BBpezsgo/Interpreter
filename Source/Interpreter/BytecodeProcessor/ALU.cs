namespace LanguageCore.Runtime;

public static class ALU
{
    public const byte SignBit8 = unchecked((byte)0x80);
    public const char SignBit16 = unchecked((char)0x8000);
    public const int SignBit32 = unchecked((int)0x80000000);

    public const byte AllBit8 = unchecked((byte)0xFF);
    public const char AllBit16 = unchecked((char)0xFFFF);
    public const int AllBit32 = unchecked((int)0xFFFFFFFF);

    public static RuntimeValue Add(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags) => bitWidth switch
    {
        BitWidth._8 => new RuntimeValue(AddU8(a.U8, b.U8, ref flags)),
        BitWidth._16 => new RuntimeValue(AddU16(a.U16, b.U16, ref flags)),
        BitWidth._32 => new RuntimeValue(AddI32(a.I32, b.I32, ref flags)),
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

    public static byte AddU8(byte a, byte b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit8) != 0);
        flags.Set(Flags.Zero, (result & AllBit8) == 0);
        flags.Set(Flags.Carry, result > AllBit8);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit8) == SignBit8);

        return unchecked((byte)result);
    }

    public static char AddU16(char a, char b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit16) != 0);
        flags.Set(Flags.Zero, (result & AllBit16) == 0);
        flags.Set(Flags.Carry, result > AllBit16);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit16) == SignBit16);

        return unchecked((char)result);
    }

    public static int AddI32(int a, int b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit32) != 0);
        flags.Set(Flags.Zero, (result & AllBit32) == 0);
        flags.Set(Flags.Carry, result > AllBit32);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit32) == SignBit32);

        return unchecked((int)result);
    }

    public static RuntimeValue Subtract(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags) => bitWidth switch
    {
        BitWidth._8 => new RuntimeValue(SubtractU8(a.U8, b.U8, ref flags)),
        BitWidth._16 => new RuntimeValue(SubtractU16(a.U16, b.U16, ref flags)),
        BitWidth._32 => new RuntimeValue(SubtractI32(a.I32, b.I32, ref flags)),
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

    public static byte SubtractU8(byte a, byte b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit8) != 0);
        flags.Set(Flags.Zero, (result & AllBit8) == 0);
        flags.Set(Flags.Carry, result > AllBit8);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit8) == (long)SignBit8);

        return unchecked((byte)result);
    }

    public static char SubtractU16(char a, char b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit16) != 0);
        flags.Set(Flags.Zero, (result & AllBit16) == 0);
        flags.Set(Flags.Carry, result > AllBit16);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x8000) == (long)0x8000);

        return unchecked((char)result);
    }

    public static int SubtractI32(int a, int b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit32) != 0);
        flags.Set(Flags.Zero, (result & AllBit32) == 0);
        flags.Set(Flags.Carry, result > AllBit32);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit32) == (long)SignBit32);

        return unchecked((int)result);
    }
}
