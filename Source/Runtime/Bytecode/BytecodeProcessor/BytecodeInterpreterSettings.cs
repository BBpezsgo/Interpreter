namespace LanguageCore.Runtime;

public struct BytecodeInterpreterSettings
{
    public int StackSize;
    public int HeapSize;

    public readonly int StackStart => ProcessorState.StackDirection > 0 ? HeapSize : HeapSize + StackSize - 1;

    public static BytecodeInterpreterSettings Default => new()
    {
        StackSize = 2048,
        HeapSize = 2048,
    };

    public BytecodeInterpreterSettings(BytecodeInterpreterSettings other)
    {
        StackSize = other.StackSize;
        HeapSize = other.HeapSize;
    }
}
