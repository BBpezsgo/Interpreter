namespace LanguageCore.Compiler;

public class AddressAbsolute : Address
{
    public int Value { get; }

    public AddressAbsolute(int value)
    {
        Value = value;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => Value.ToString();
}
