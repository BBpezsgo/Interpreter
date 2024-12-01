namespace LanguageCore.Runtime;

#if UNITY
[Unity.Burst.BurstCompile]
#endif
public static class FlagExtensions
{
#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static void Set(ref this Flags flags, Flags flag, bool value)
    {
        if (value) flags |= flag;
        else flags &= ~flag;
    }

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static bool Get(this Flags flags, Flags flag) => (flags & flag) != 0;

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static void SetSign(ref this Flags flags, int v, BitWidth bitWidth)
    {
        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Sign, unchecked((uint)v & (uint)0x80) != 0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Sign, unchecked((uint)v & (uint)0x8000) != 0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Sign, unchecked((uint)v & (uint)0x80000000) != 0);
                break;
        }
    }

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static void SetZero(ref this Flags flags, int v, BitWidth bitWidth)
    {
        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Zero, (v & 0xFF) == 0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Zero, (v & 0xFFFF) == 0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Zero, (v & 0xFFFFFFFF) == 0);
                break;
        }
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static void SetCarry(ref this Flags flags, long result, BitWidth bitWidth)
    {
        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Carry, result > 0xFF);
                break;
            case BitWidth._16:
                flags.Set(Flags.Carry, result > 0xFFFF);
                break;
            case BitWidth._32:
                flags.Set(Flags.Carry, result > 0xFFFFFFFF);
                break;
        }
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static void SetOverflowAfterAdd(ref this Flags flags, int source, int destination, BitWidth bitWidth)
    {
        long result = source + destination;

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Overflow, ((result ^ source) & (result ^ destination) & 0x80) == 0x80);
                break;
            case BitWidth._16:
                flags.Set(Flags.Overflow, ((result ^ source) & (result ^ destination) & 0x8000) == 0x8000);
                break;
            case BitWidth._32:
                flags.Set(Flags.Overflow, ((result ^ source) & (result ^ destination) & 0x80000000) == 0x80000000);
                break;
        }
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    public static void SetOverflowAfterSub(ref this Flags flags, int source, int destination, BitWidth bitWidth)
    {
        long result = destination - source;

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Overflow, ((result ^ destination) & (source ^ destination) & 0x80) == 0x80);
                break;
            case BitWidth._16:
                flags.Set(Flags.Overflow, ((result ^ destination) & (source ^ destination) & 0x8000) == 0x8000);
                break;
            case BitWidth._32:
                flags.Set(Flags.Overflow, ((result ^ destination) & (source ^ destination) & 0x80000000) == 0x80000000);
                break;
        }
    }
}
