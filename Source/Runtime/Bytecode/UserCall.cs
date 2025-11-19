namespace LanguageCore.Runtime;

public class UserCall
{
    public readonly ExposedFunction Function;
    public readonly byte[] Arguments;
    public byte[]? Result;

    public UserCall(ExposedFunction function, byte[] arguments)
    {
        Function = function;
        Arguments = arguments;
        Result = null;
    }
}
