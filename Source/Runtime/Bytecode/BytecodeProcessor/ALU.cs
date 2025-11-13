namespace LanguageCore.Runtime;

#if UNITY_BURST
[Unity.Burst.BurstCompile]
#endif
public static class ALU
{
#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    static long SignExtend(int value, BitWidth width)
    {
        unchecked
        {
            long mask = GetMask(width);
            long sign = GetSignBit(width);
            value &= (int)mask;
            return (((long)value & sign) != 0) ? ((long)value | ~mask) : (long)value;
        }
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int Add(int a, int b, BitWidth width, ref Flags flags)
    {
        uint mask = GetMask(width);
        uint sign = GetSignBit(width);
        uint ua = (uint)a & mask;
        uint ub = (uint)b & mask;

        ulong full = (ulong)ua + (ulong)ub;
        uint r = (uint)full & mask;

        bool cf = (full & ~mask) != 0;
        bool zf = r == 0;
        bool sf = (r & sign) != 0;
        bool of = ((~(ua ^ ub)) & (ua ^ r) & sign) != 0;

        flags.Set(Flags.Carry, cf);
        flags.Set(Flags.Zero, zf);
        flags.Set(Flags.Sign, sf);
        flags.Set(Flags.Overflow, of);

        return r.I32();
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    static uint GetSignBit(BitWidth width) => width switch
    {
        BitWidth._8 => 0x80,
        BitWidth._16 => 0x8000,
        BitWidth._32 => 0x80000000,
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    static uint GetMask(BitWidth width) => width switch
    {
        BitWidth._8 => 0xFFu,
        BitWidth._16 => 0xFFFFu,
        BitWidth._32 => 0xFFFFFFFFu,
        BitWidth._64 => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int Subtract(int a, int b, BitWidth width, ref Flags flags)
    {
        uint mask = GetMask(width);

        uint signBit = GetSignBit(width);

        uint ua = (uint)a & mask;
        uint ub = (uint)b & mask;

        uint result = (ua - ub) & mask;

        flags.Set(Flags.Carry, ua < ub);
        flags.Set(Flags.Zero, result == 0);
        flags.Set(Flags.Sign, (result & signBit) != 0);
        flags.Set(Flags.Overflow, ((ua ^ ub) & (ua ^ result) & signBit) != 0);

        return result.I32();
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int DivSigned(int a, int b, BitWidth width, ref Flags flags, out int remainder)
    {
        long sa = SignExtend(a, width);
        long sb = SignExtend(b, width);
        if (sb == 0) throw new DivideByZeroException();

        long q = sa / sb;
        long r = sa % sb;

        long max = (long)(GetSignBit(width) - 1);
        long min = -GetSignBit(width);
        if (q > max || q < min) throw new OverflowException();

        remainder = (int)(r & GetMask(width));

        flags.Set(Flags.Carry, false);
        flags.Set(Flags.Zero, q == 0);
        flags.Set(Flags.Sign, (q & GetSignBit(width)) != 0);
        flags.Set(Flags.Overflow, false);

        return (int)(q & GetMask(width));
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int DivUnsigned(int a, int b, BitWidth width, ref Flags flags, out int remainder)
    {
        uint mask = GetMask(width);
        uint ua = (uint)a & mask;
        uint ub = (uint)b & mask;
        if (ub == 0) throw new DivideByZeroException();

        uint q = ua / ub;
        uint r = ua % ub;

        if (q > mask) throw new OverflowException();

        remainder = (int)(r & mask);

        flags.Set(Flags.Carry, false);
        flags.Set(Flags.Zero, q == 0);
        flags.Set(Flags.Sign, (q & GetSignBit(width)) != 0);
        flags.Set(Flags.Overflow, false);

        return (int)(q & mask);
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int MulSigned(int a, int b, BitWidth width, ref Flags flags)
    {
        long mask = (long)GetMask(width);
        long sign = (long)GetSignBit(width);

        long sa = SignExtend(a, width);
        long sb = SignExtend(b, width);
        long full = sa * sb;

        long result = full & mask;
        flags.Set(Flags.Carry | Flags.Overflow, full > mask || full < -sign);
        flags.Set(Flags.Zero, result == 0);
        flags.Set(Flags.Sign, (result & sign) != 0);

        return (int)result;
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int MulUnsigned(int a, int b, BitWidth width, ref Flags flags)
    {
        ulong mask = GetMask(width);
        ulong full = ((ulong)a & mask) * ((ulong)b & mask);
        ulong result = full & mask;

        flags.Set(Flags.Carry | Flags.Overflow, (full >> (int)width) != 0);
        flags.Set(Flags.Zero, result == 0);
        flags.Set(Flags.Sign, (result & GetSignBit(width)) != 0);

        return (int)result;
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int BitwiseAnd(int a, int b, BitWidth width, ref Flags flags)
    {
        int result = (a.U32() & b.U32()).I32();

        flags.SetSign(result, width);
        flags.SetZero(result, width);
        flags.Set(Flags.Carry, false);

        return result;
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int BitwiseOr(int a, int b, BitWidth width, ref Flags flags)
    {
        int result = (a.U32() | b.U32()).I32();

        flags.SetSign(result, width);
        flags.SetZero(result, width);
        flags.Set(Flags.Carry, false);

        return result;
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int BitwiseXor(int a, int b, BitWidth width, ref Flags flags)
    {
        int result = (a.U32() ^ b.U32()).I32();

        flags.SetSign(result, width);
        flags.SetZero(result, width);
        flags.Set(Flags.Carry, false);

        return result;
    }

#if UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public static int BitwiseNot(int a, BitWidth width, ref Flags flags)
    {
        int result = (~a.U32()).I32();

        flags.SetSign(result, width);
        flags.SetZero(result, width);
        flags.Set(Flags.Carry, false);

        return result;
    }
}
