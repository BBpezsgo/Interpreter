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
    ImmutableArray<ParameterDefinition> Parameters { get; }
    ImmutableArray<GeneralType> ParameterTypes { get; }
}
