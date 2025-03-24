namespace LanguageCore.Compiler;

public class CompiledFunction
{
    public ICompiledFunctionDefinition Function;
    public CompiledBlock Body;

    public CompiledFunction(ICompiledFunctionDefinition function, CompiledBlock body)
    {
        Function = function;
        Body = body;
    }

    public void Deconstruct(out ICompiledFunctionDefinition function, out CompiledBlock body)
    {
        function = Function;
        body = Body;
    }

    public override string? ToString() => Function.ToString() ?? base.ToString();
}
