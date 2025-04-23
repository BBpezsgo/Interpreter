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
    // readonly Type GlobalScopeType;

    public CodeGeneratorForIL(CompilerResult compilerResult, DiagnosticsCollection diagnostics, ILGeneratorSettings settings, ModuleBuilder? module) : base(compilerResult, diagnostics)
    {
        if (module is null)
        {
            AssemblyBuilder assemBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName()
            {
                Name = "BBLangGeneratedAssembly",
            }, AssemblyBuilderAccess.RunAndCollect);

            Type daType = typeof(DebuggableAttribute);
            ConstructorInfo daCtor = daType.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) })!;
            CustomAttributeBuilder daBuilder = new(daCtor, new object[] {
                DebuggableAttribute.DebuggingModes.DisableOptimizations |
                DebuggableAttribute.DebuggingModes.Default });
            assemBuilder.SetCustomAttribute(daBuilder);

            module = assemBuilder.DefineDynamicModule("BBLangGeneratedModule");
        }

        // TypeBuilder globalScopeTypeBuilder = module.DefineType(MakeUnique("global"));
        // globalScopeTypeBuilder.DefineField("__memory", typeof(byte), FieldAttributes.Assembly);
        // foreach (CompiledVariableDeclaration globalVariable in CompiledGlobalVariables)
        // {
        //     if (!ToType(globalVariable.Type, out Type? type, out PossibleDiagnostic? typeError))
        //     {
        //         Diagnostics.Add(typeError.ToError(globalVariable));
        //         continue;
        //     }
        //     globalScopeTypeBuilder.DefineField(globalVariable.Identifier, type, FieldAttributes.Assembly);
        // }
        // GlobalScopeType = globalScopeTypeBuilder.CreateType();

        Settings = settings;
        Module = module;
        Builders = new();
    }
}
