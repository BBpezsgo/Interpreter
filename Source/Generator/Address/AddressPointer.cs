namespace LanguageCore.Compiler;

public class AddressPointer : Address
{
    public Address PointerAddress { get; }

    public AddressPointer(Address pointerAddress)
    {
        PointerAddress = pointerAddress;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"*[{PointerAddress}]";
}
