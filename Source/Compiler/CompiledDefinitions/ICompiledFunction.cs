namespace LanguageCore.Compiler;

public interface ICompiledFunctionDefinition :
    IHaveCompiledType,
    IInFile,
    IHaveInstructionOffset,
    ISimpleReadable,
    IMsilCompatible
{
    bool ReturnSomething { get; }
    ImmutableArray<CompiledParameter> Parameters { get; }
}
