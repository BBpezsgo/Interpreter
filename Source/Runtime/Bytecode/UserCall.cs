
namespace LanguageCore.Runtime;

public class UserCall
{
    public readonly int InstructionOffset;
    public readonly ImmutableArray<byte> Arguments;
    public readonly int ReturnValueSize;

    public UserCall(int instructionOffset, ImmutableArray<byte> arguments, int returnValueSize)
    {
        InstructionOffset = instructionOffset;
        Arguments = arguments;
        ReturnValueSize = returnValueSize;
    }
}
