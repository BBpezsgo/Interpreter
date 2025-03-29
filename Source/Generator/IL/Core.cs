using System.Reflection;
using System.Reflection.Emit;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForIL : CodeGenerator
{
    public static readonly CompilerSettings DefaultCompilerSettings = new()
    {
        PointerSize = 8,
        ArrayLengthType = BuiltinType.I32,
        BooleanType = BuiltinType.U8,
        ExitCodeType = BuiltinType.I32,
        SizeofStatementType = BuiltinType.I32,
        DontOptimize = false,
        PreprocessorVariables = PreprocessorVariables.IL,
        ExternalConstants = ImmutableArray<ExternalConstant>.Empty,
        ExternalFunctions = ImmutableArray<IExternalFunction>.Empty,
        SourceProviders = ImmutableArray.Create<ISourceProvider>(
            FileSourceProvider.Instance
        ),
    };

    public override int PointerSize => 8;
    public override BuiltinType BooleanType => BuiltinType.U8;
    public override BuiltinType SizeofStatementType => BuiltinType.I32;
    public override BuiltinType ArrayLengthType => BuiltinType.I32;

    readonly ILGeneratorSettings Settings;

    public CodeGeneratorForIL(CompilerResult compilerResult, DiagnosticsCollection diagnostics, ILGeneratorSettings settings, ModuleBuilder? module) : base(compilerResult, diagnostics)
    {
        if (module is null)
        {
            AssemblyBuilder assemBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName()
            {
                Name = "BBLangGeneratedAssembly",
            }, AssemblyBuilderAccess.RunAndCollect);
            module = assemBuilder.DefineDynamicModule("BBLangGeneratedModule");
        }
        Settings = settings;
        Module = module;
    }
}
