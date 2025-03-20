
namespace LanguageCore.Runtime;

public class UserCall
{
    public readonly int InstructionOffset;
    public readonly byte[] Arguments;
    public readonly int ReturnValueSize;
    public byte[]? Result;

    public UserCall(int instructionOffset, byte[] arguments, int returnValueSize)
    {
        InstructionOffset = instructionOffset;
        Arguments = arguments;
        ReturnValueSize = returnValueSize;
        Result = null;
    }
}
