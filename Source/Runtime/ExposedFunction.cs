namespace LanguageCore.Runtime;

public readonly struct ExposedFunction
{
    public readonly string Identifier;
    public readonly int ReturnValueSize;
    public readonly int InstructionOffset;
    public readonly int ArgumentsSize;

    public ExposedFunction(string identifier, int returnValueSize, int instructionOffset, int argumentsSize)
    {
        Identifier = identifier;
        ReturnValueSize = returnValueSize;
        InstructionOffset = instructionOffset;
        ArgumentsSize = argumentsSize;
    }
}
