namespace LanguageCore.Compiler;

public class AddressRuntimePointer : Address
{
    public CompiledExpression PointerValue { get; }

    public AddressRuntimePointer(CompiledExpression pointerValue)
    {
        PointerValue = pointerValue;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"*[{PointerValue}]";
}
