namespace LanguageCore.Compiler;

public abstract class Address
{
    public static Address operator -(Address a, int b)
        => a + -b;
    public static Address operator +(Address a, int b)
    {
        if (a is AddressOffset addressOffset)
        {
            return new AddressOffset(addressOffset.Base, addressOffset.Offset + b);
        }
        else if (a is AddressAbsolute addressAbsolute)
        {
            return new AddressAbsolute(addressAbsolute.Value + b);
        }
        else
        {
            return new AddressOffset(a, b);
        }
    }
}
