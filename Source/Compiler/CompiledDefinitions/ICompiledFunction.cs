using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public interface ICompiledFunctionDefinition :
    IHaveCompiledType,
    IInFile,
    IHaveInstructionOffset,
    ISimpleReadable
{
    bool ReturnSomething { get; }
    IReadOnlyList<ParameterDefinition> Parameters { get; }
    IReadOnlyList<GeneralType> ParameterTypes { get; }
}
