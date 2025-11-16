using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledFunction : ISimpleReadable
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

    public string ToReadable(FindStatementType typeSearch) => Function.ToReadable(typeSearch);
    public string ToReadable() => Function.ToReadable();
    public override string? ToString() => Function.ToString() ?? base.ToString();
}
