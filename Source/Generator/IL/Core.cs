using LanguageCore.Compiler;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    public override int PointerSize => 8;
    public override BuiltinType BooleanType => BuiltinType.U8;
    public override BuiltinType SizeofStatementType => BuiltinType.I32;
    public override BuiltinType ArrayLengthType => BuiltinType.I32;

    public CodeGeneratorForMain(CompilerResult compilerResult, DiagnosticsCollection diagnostics, PrintCallback? print) : base(compilerResult, diagnostics, print)
    { }

    public static Func<int> Generate(
        CompilerResult compilerResult,
        PrintCallback? printCallback,
        DiagnosticsCollection diagnostics)
    {
        CodeGeneratorForMain generator = new(compilerResult, diagnostics, printCallback);
        return generator.GenerateCode(compilerResult);
    }
}
