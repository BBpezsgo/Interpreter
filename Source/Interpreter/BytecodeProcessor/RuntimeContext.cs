namespace LanguageCore.Runtime;

public readonly struct RuntimeContext
{
    public readonly Registers Registers;
    public readonly ImmutableArray<RuntimeValue> Memory;
    public readonly ImmutableArray<Instruction> Code;

    public readonly ImmutableArray<int> CallTrace;

    public RuntimeContext(
        Registers registers,
        ImmutableArray<RuntimeValue> memory,
        ImmutableArray<Instruction> code)
    {
        Registers = registers;
        Memory = memory;
        Code = code;

        CallTrace = ImmutableArray.Create(DebugUtils.TraceCalls(Memory, Registers.BasePointer));
    }
}
