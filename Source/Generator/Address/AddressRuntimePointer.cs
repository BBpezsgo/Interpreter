namespace LanguageCore.Compiler;

public class AddressRuntimePointer : Address
{
    public CompiledStatementWithValue PointerValue { get; }

    public AddressRuntimePointer(CompiledStatementWithValue pointerValue)
    {
        PointerValue = pointerValue;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"*[{PointerValue}]";
}
