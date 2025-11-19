using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

public readonly struct ExposedFunction
{
    public readonly string Identifier;
    public readonly int ReturnValueSize;
    public readonly int InstructionOffset;
    public readonly int ArgumentsSize;
    public readonly FunctionFlags Flags;

    public ExposedFunction(string identifier, int returnValueSize, int instructionOffset, int argumentsSize, FunctionFlags flags)
    {
        Identifier = identifier;
        ReturnValueSize = returnValueSize;
        InstructionOffset = instructionOffset;
        ArgumentsSize = argumentsSize;
        Flags = flags;
    }
}
