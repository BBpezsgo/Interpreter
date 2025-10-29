using LanguageCore.Compiler;

namespace LanguageCore.Native.Generator;

public partial class CodeGeneratorForNative : CodeGenerator
{
    public CodeGeneratorForNative(CompilerResult compilerResult, DiagnosticsCollection diagnostics) : base(compilerResult, diagnostics)
    {

    }
}
