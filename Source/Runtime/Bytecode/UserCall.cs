namespace LanguageCore.Runtime;

public class UserCall
{
    public readonly int InstructionOffset;
    public readonly ImmutableArray<byte> Arguments;

    public UserCall(int instructionOffset, ImmutableArray<byte> arguments)
    {
        InstructionOffset = instructionOffset;
        Arguments = arguments;
    }
}
