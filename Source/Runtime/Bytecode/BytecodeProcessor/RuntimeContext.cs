namespace LanguageCore.Runtime;

#if UNITY
[Unity.Burst.BurstCompile]
public readonly struct RuntimeContext : IDisposable
{
    public readonly Registers Registers;
    [Unity.Collections.ReadOnly] public readonly Unity.Collections.NativeArray<byte> Memory;
    [Unity.Collections.ReadOnly] public readonly Unity.Collections.NativeArray<Instruction> Code;
    public readonly int StackStart;

    public RuntimeContext(
        Registers registers,
        Unity.Collections.NativeArray<byte> memory,
        Unity.Collections.NativeArray<Instruction> code,
        int stackStart)
    {
        Registers = registers;
        Memory = memory;
        Code = code;
        StackStart = stackStart;
    }

    public void Dispose()
    {
        Memory.Dispose();
        Code.Dispose();
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
