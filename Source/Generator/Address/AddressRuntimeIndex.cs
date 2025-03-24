namespace LanguageCore.Compiler;

public class AddressRuntimeIndex : Address
{
    public Address Base { get; }
    public CompiledStatementWithValue IndexValue { get; }
    public int ElementSize { get; }

    public AddressRuntimeIndex(Address @base, CompiledStatementWithValue indexValue, int elementSize)
    {
        Base = @base;
        IndexValue = indexValue;
        ElementSize = elementSize;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"({Base} + {IndexValue} * {ElementSize})";
}
