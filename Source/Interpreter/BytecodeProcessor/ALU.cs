namespace LanguageCore.Runtime;

#if UNITY
[Unity.Burst.BurstCompile]
#endif
public static class ALU
{
    public const byte SignBit8 = unchecked((byte)0x80);
    public const char SignBit16 = unchecked((char)0x8000);
    public const int SignBit32 = unchecked((int)0x80000000);
    public const int SignBit64 = unchecked((int)0x8000000000000000);

    public const byte AllBit8 = unchecked((byte)0xFF);
    public const char AllBit16 = unchecked((char)0xFFFF);
    public const int AllBit32 = unchecked((int)0xFFFFFFFF);
    public const int AllBit64 = unchecked((int)0xFFFFFFFFFFFFFFFF);

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static int AddI(int a, int b, BitWidth bitWidth, ref Flags flags) => bitWidth switch
    {
        BitWidth._8 => AddI8(a.I8(), b.I8(), ref flags).I32(),
        BitWidth._16 => AddI16(a.I16(), b.I16(), ref flags).I32(),
        BitWidth._32 => AddI32(a.I32(), b.I32(), ref flags).I32(),
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static byte AddI8(sbyte a, sbyte b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit8) != 0);
        flags.Set(Flags.Zero, (result & AllBit8) == 0);
        flags.Set(Flags.Carry, result > AllBit8);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit8) == SignBit8);

        return unchecked((byte)result);
    }

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static short AddI16(short a, short b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit16) != 0);
        flags.Set(Flags.Zero, (result & AllBit16) == 0);
        flags.Set(Flags.Carry, result > AllBit16);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit16) == SignBit16);

        return unchecked((short)result);
    }

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static int AddI32(int a, int b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit32) != 0);
        flags.Set(Flags.Zero, (result & AllBit32) == 0);
        flags.Set(Flags.Carry, result > AllBit32);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit32) == SignBit32);

        return unchecked((int)result);
    }

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static int SubtractI(int a, int b, BitWidth bitWidth, ref Flags flags) => bitWidth switch
    {
        BitWidth._8 => SubtractI8(a.I8(), b.I8(), ref flags).I32(),
        BitWidth._16 => SubtractI16(a.I16(), b.I16(), ref flags).I32(),
        BitWidth._32 => SubtractI32(a.I32(), b.I32(), ref flags).I32(),
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static sbyte SubtractI8(sbyte a, sbyte b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit8) != 0);
        flags.Set(Flags.Zero, (result & AllBit8) == 0);
        flags.Set(Flags.Carry, result > AllBit8);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit8) == (long)SignBit8);

        return unchecked((sbyte)result);
    }

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static short SubtractI16(short a, short b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit16) != 0);
        flags.Set(Flags.Zero, (result & AllBit16) == 0);
        flags.Set(Flags.Carry, result > AllBit16);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit16) == (long)SignBit16);

        return unchecked((short)result);
    }

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
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
