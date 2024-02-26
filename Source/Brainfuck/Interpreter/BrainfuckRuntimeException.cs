using System;

namespace LanguageCore.Brainfuck;

public readonly struct RuntimeContext
{
    public readonly int MemoryPointer;
    public readonly int CodePointer;

    public RuntimeContext(int memoryPointer, int codePointer)
    {
        MemoryPointer = memoryPointer;
        CodePointer = codePointer;
    }
}

public class BrainfuckRuntimeException : Exception
{
    public readonly RuntimeContext RuntimeContext;

    public BrainfuckRuntimeException(string message, RuntimeContext context) : base(message)
    {
        RuntimeContext = context;
    }
}
