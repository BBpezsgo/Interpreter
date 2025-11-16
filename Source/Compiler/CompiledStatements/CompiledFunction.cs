namespace LanguageCore.Compiler;

public class CompiledFunction : ISimpleReadable
{
    public ICompiledFunctionDefinition Function;
    public CompiledBlock Body;
    public bool CapturesGlobalVariables;

    public CompiledFunction(ICompiledFunctionDefinition function, CompiledBlock body, bool capturesGlobalVariables)
    {
        Function = function;
        Body = body;
        CapturesGlobalVariables = capturesGlobalVariables;
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
