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
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    // [Unity.Burst.BurstCompile]
#endif
    public static RuntimeValue AddU(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags) => bitWidth switch
    {
        BitWidth._8 => new RuntimeValue(AddU8(a.U8, b.U8, ref flags)),
        BitWidth._16 => new RuntimeValue(AddU16(a.U16, b.U16, ref flags)),
        BitWidth._32 => new RuntimeValue(AddU32(a.U32, b.U32, ref flags)),
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

#if UNITY
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    // [Unity.Burst.BurstCompile]
#endif
    public static RuntimeValue AddI(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags) => bitWidth switch
    {
        BitWidth._8 => new RuntimeValue(AddI8(a.I8, b.I8, ref flags)),
        BitWidth._16 => new RuntimeValue(AddI16(a.I16, b.I16, ref flags)),
        BitWidth._32 => new RuntimeValue(AddI32(a.I32, b.I32, ref flags)),
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static byte AddU8(byte a, byte b, ref Flags flags)
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
    public static ushort AddU16(ushort a, ushort b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit16) != 0);
        flags.Set(Flags.Zero, (result & AllBit16) == 0);
        flags.Set(Flags.Carry, result > AllBit16);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit16) == SignBit16);

        return unchecked((ushort)result);
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
    public static uint AddU32(uint a, uint b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit32) != 0);
        flags.Set(Flags.Zero, (result & AllBit32) == 0);
        flags.Set(Flags.Carry, result > AllBit32);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit32) == SignBit32);

        return unchecked((uint)result);
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
    public static long AddI64(long a, long b, ref Flags flags)
    {
        long result = a + b;

        flags.Set(Flags.Sign, unchecked(result & SignBit64) != 0);
        flags.Set(Flags.Zero, (result & AllBit64) == 0);
        flags.Set(Flags.Carry, result > AllBit64);
        flags.Set(Flags.Overflow, ((result ^ a) & (result ^ b) & SignBit64) == SignBit64);

        return unchecked((int)result);
    }

#if UNITY
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    // [Unity.Burst.BurstCompile]
#endif
    public static RuntimeValue SubtractU(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags) => bitWidth switch
    {
        BitWidth._8 => new RuntimeValue(SubtractU8(a.U8, b.U8, ref flags)),
        BitWidth._16 => new RuntimeValue(SubtractU16(a.U16, b.U16, ref flags)),
        BitWidth._32 => new RuntimeValue(SubtractU32(a.U32, b.U32, ref flags)),
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

#if UNITY
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    // [Unity.Burst.BurstCompile]
#endif
    public static RuntimeValue SubtractI(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags) => bitWidth switch
    {
        BitWidth._8 => new RuntimeValue(SubtractI8(a.I8, b.I8, ref flags)),
        BitWidth._16 => new RuntimeValue(SubtractI16(a.I16, b.I16, ref flags)),
        BitWidth._32 => new RuntimeValue(SubtractI32(a.I32, b.I32, ref flags)),
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static byte SubtractU8(byte a, byte b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit8) != 0);
        flags.Set(Flags.Zero, (result & AllBit8) == 0);
        flags.Set(Flags.Carry, result > AllBit8);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit8) == (long)SignBit8);

        return unchecked((byte)result);
    }

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
    public static ushort SubtractU16(ushort a, ushort b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit16) != 0);
        flags.Set(Flags.Zero, (result & AllBit16) == 0);
        flags.Set(Flags.Carry, result > AllBit16);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit16) == (long)SignBit16);

        return unchecked((ushort)result);
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
    public static uint SubtractU32(uint a, uint b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit32) != 0);
        flags.Set(Flags.Zero, (result & AllBit32) == 0);
        flags.Set(Flags.Carry, result > AllBit32);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit32) == (long)SignBit32);

        return unchecked((uint)result);
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

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static long SubtractI64(long a, long b, ref Flags flags)
    {
        long result = a - b;

        flags.Set(Flags.Sign, unchecked(result & SignBit64) != 0);
        flags.Set(Flags.Zero, (result & AllBit64) == 0);
        flags.Set(Flags.Carry, result > AllBit64);
        // flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit64) == (long)SignBit64);

        return unchecked((int)result);
    }
}
