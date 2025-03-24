using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public interface ICompiledFunctionDefinition :
    IHaveCompiledType,
    IInFile,
    IHaveInstructionOffset
{
    bool ReturnSomething { get; }
    Block? Block { get; }
    IReadOnlyList<ParameterDefinition> Parameters { get; }
    IReadOnlyList<GeneralType> ParameterTypes { get; }
}
