namespace LanguageCore.Runtime;

public delegate ExternalFunctionAsyncReturnChecker ExternalFunctionAsyncCallback(ref ProcessorState processor, ReadOnlySpan<byte> arguments);
public delegate bool ExternalFunctionAsyncReturnChecker(ref ProcessorState processor, Span<byte> returnValue);

public ref struct PendingExternalFunction
{
    public ExternalFunctionAsyncReturnChecker Checker;
    public Span<byte> ReturnValue;
}

public readonly struct ExternalFunctionAsync : IExternalFunction
{
    public string? Name { get; }
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public ExternalFunctionAsyncCallback Callback { get; }

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionAsync(ExternalFunctionAsyncCallback callback, int id, string? name, int parametersSize, int returnValueSize)
    {
        Callback = callback;
        Id = id;
        Name = name;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
    }

    public override string ToString() => $"<{ReturnValueSize}b> {Name ?? Id.ToString()}(<{ParametersSize}b>)";
}
