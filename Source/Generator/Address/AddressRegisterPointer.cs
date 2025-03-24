using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public class AddressRegisterPointer : Address
{
    public Register Register { get; }

    public AddressRegisterPointer(Register register)
    {
        Register = register;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{Register}";
}
