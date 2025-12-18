using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public class AddressOffset : Address
{
    public Address Base { get; }
    public int Offset { get; }

    public AddressOffset(Register register, int offset)
    {
        Base = new AddressRegisterPointer(register);
        Offset = offset;
    }

    public AddressOffset(Address @base, int offset)
    {
        Base = @base;
        Offset = offset;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => Offset switch
    {
        > 0 => $"{Base} + {Offset}",
        < 0 => $"{Base} - {-Offset}",
        _ => $"{Base} + 0"
    };
}
