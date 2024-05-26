namespace LanguageCore.Runtime;

public static class ALU
{
    public static RuntimeValue Add(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags)
    {
        long _a = bitWidth switch
        {
            BitWidth._8 => a.Byte,
            BitWidth._16 => a.Char,
            BitWidth._32 => a.Int,
            _ => throw new UnreachableException(),
        };
        long _b = bitWidth switch
        {
            BitWidth._8 => b.Byte,
            BitWidth._16 => b.Char,
            BitWidth._32 => b.Int,
            _ => throw new UnreachableException(),
        };
        long result = _a + _b;

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x80) != (long)0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x8000) != (long)0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x80000000) != (long)0);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Zero, (result & (long)0xFF) == (long)0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Zero, (result & (long)0xFFFF) == (long)0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Zero, (result & (long)0xFFFFFFFF) == (long)0);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Carry, result > (long)0xFF);
                break;
            case BitWidth._16:
                flags.Set(Flags.Carry, result > (long)0xFFFF);
                break;
            case BitWidth._32:
                flags.Set(Flags.Carry, result > (long)0xFFFFFFFF);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x80) == (long)0x80);
                break;
            case BitWidth._16:
                flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x8000) == (long)0x8000);
                break;
            case BitWidth._32:
                flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x80000000) == (long)0x80000000);
                break;
        }

        return bitWidth switch
        {
            BitWidth._8 => new RuntimeValue(unchecked((byte)result)),
            BitWidth._16 => new RuntimeValue(unchecked((char)result)),
            BitWidth._32 => new RuntimeValue(unchecked((int)result)),
            _ => throw new UnreachableException(),
        };
    }

    public static RuntimeValue Subtract(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags)
    {
        long _a = bitWidth switch
        {
            BitWidth._8 => a.Byte,
            BitWidth._16 => a.Char,
            BitWidth._32 => a.Int,
            _ => throw new UnreachableException(),
        };
        long _b = bitWidth switch
        {
            BitWidth._8 => b.Byte,
            BitWidth._16 => b.Char,
            BitWidth._32 => b.Int,
            _ => throw new UnreachableException(),
        };
        long result = _a - _b;

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x80) != (long)0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x8000) != (long)0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x80000000) != (long)0);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Zero, (result & (long)0xFF) == (long)0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Zero, (result & (long)0xFFFF) == (long)0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Zero, (result & (long)0xFFFFFFFF) == (long)0);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Carry, result > (long)0xFF);
                break;
            case BitWidth._16:
                flags.Set(Flags.Carry, result > (long)0xFFFF);
                break;
            case BitWidth._32:
                flags.Set(Flags.Carry, result > (long)0xFFFFFFFF);
                break;
        }

        // switch (bitWidth)
        // {
        //     case BitWidth._8:
        //         flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x80) == (long)0x80);
        //         break;
        //     case BitWidth._16:
        //         flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x8000) == (long)0x8000);
        //         break;
        //     case BitWidth._32:
        //         flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x80000000) == (long)0x80000000);
        //         break;
        // }

        return bitWidth switch
        {
            BitWidth._8 => new RuntimeValue(unchecked((byte)result)),
            BitWidth._16 => new RuntimeValue(unchecked((char)result)),
            BitWidth._32 => new RuntimeValue(unchecked((int)result)),
            _ => throw new UnreachableException(),
        };
    }
}
