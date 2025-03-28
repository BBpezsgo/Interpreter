
namespace LanguageCore.Runtime;

public ref struct UserCallSync
{
    public readonly int InstructionOffset;
    public readonly ReadOnlySpan<byte> Arguments;
    public readonly int ReturnValueSize;
    public byte[]? Result;

    public UserCallSync(int instructionOffset, ReadOnlySpan<byte> arguments, int returnValueSize)
    {
        InstructionOffset = instructionOffset;
        Arguments = arguments;
        ReturnValueSize = returnValueSize;
        Result = null;
    }
}
