namespace LanguageCore.Runtime;

public struct BytecodeInterpreterSettings
{
    public int StackSize;
    public int HeapSize;

    public static BytecodeInterpreterSettings Default => new()
    {
        StackSize = 2048,
        HeapSize = 2048,
    };
}
