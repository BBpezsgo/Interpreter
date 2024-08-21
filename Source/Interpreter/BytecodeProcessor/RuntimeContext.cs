namespace LanguageCore.Runtime;

public readonly struct RuntimeContext
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
}
