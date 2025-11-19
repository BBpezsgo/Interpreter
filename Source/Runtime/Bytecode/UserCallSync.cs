
namespace LanguageCore.Runtime;

public ref struct UserCallSync
{
    public readonly ExposedFunction Function;
    public readonly ReadOnlySpan<byte> Arguments;
    public byte[]? Result;

    public UserCallSync(ExposedFunction function, ReadOnlySpan<byte> arguments)
    {
        Function = function;
        Arguments = arguments;
        Result = null;
    }
}
