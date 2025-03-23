using LanguageCore.Compiler;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    protected override unsafe bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = sizeof(nint);
        error = null;
        return true;
    }

    protected override unsafe bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = sizeof(nint);
        error = null;
        return true;
    }
}
