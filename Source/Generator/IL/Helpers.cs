using LanguageCore.Compiler;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    protected override bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new NotImplementedException();
    protected override bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new NotImplementedException();
}
