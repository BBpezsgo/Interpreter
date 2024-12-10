namespace LanguageCore.Runtime;

#if UNITY && false
[Unity.Burst.BurstCompile]
public readonly ref struct RuntimeContext
{
    public readonly Registers Registers;
    public readonly ReadOnlySpan<byte> Memory;
    public readonly ReadOnlySpan<Instruction> Code;
    public readonly int StackStart;

    public RuntimeContext(
        Registers registers,
        ReadOnlySpan<byte> memory,
        ReadOnlySpan<Instruction> code,
        int stackStart)
    {
        Registers = registers;
        Memory = memory;
        Code = code;
        StackStart = stackStart;
    }
}
#else
public readonly struct RuntimeContext : IDisposable
{
    public readonly Registers Registers;
    public readonly ImmutableArray<byte> Memory;
    public readonly ImmutableArray<Instruction> Code;
    public readonly int StackStart;

    public RuntimeContext(
        Registers registers,
        ImmutableArray<byte> memory,
        ImmutableArray<Instruction> code,
        int stackStart)
    {
        Registers = registers;
        Memory = memory;
        Code = code;
        StackStart = stackStart;
    }

    public void Dispose() { }
}
#endif
