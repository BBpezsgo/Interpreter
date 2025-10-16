using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public interface ICompiledFunctionDefinition :
    IHaveCompiledType,
    IInFile,
    IHaveInstructionOffset,
    ISimpleReadable,
    IMsilCompatible
{
    bool ReturnSomething { get; }
    IReadOnlyList<ParameterDefinition> Parameters { get; }
    IReadOnlyList<GeneralType> ParameterTypes { get; }
}
