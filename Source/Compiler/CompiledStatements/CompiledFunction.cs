namespace LanguageCore.Compiler;

[Flags]
public enum FunctionFlags
{
    CapturesGlobalVariables = 0x1,
}

public class CompiledFunction : ISimpleReadable
{
    public ICompiledFunctionDefinition Function;
    public CompiledBlock Body;
    public FunctionFlags Flags;
    public ImmutableArray<CapturedLocal> CapturedLocals;

    public CompiledFunction(ICompiledFunctionDefinition function, CompiledBlock body, ImmutableArray<CapturedLocal> capturedLocals)
    {
        Function = function;
        Body = body;
        CapturedLocals = capturedLocals;
        Flags = FunctionFlags.CapturesGlobalVariables;
    }

    public void Deconstruct(out ICompiledFunctionDefinition function, out CompiledBlock body)
    {
        function = Function;
        body = Body;
    }

    public string ToReadable(FindStatementType typeSearch) => Function.ToReadable(typeSearch);
    public string ToReadable() => Function.ToReadable();
    public override string? ToString() => Function.ToString() ?? base.ToString();
}
