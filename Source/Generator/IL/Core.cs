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
    readonly Type GlobalContextType;
    readonly FieldInfo GlobalContextType_Targets;
    readonly List<object> DelegateTargets = new();
    readonly Dictionary<CompiledVariableDeclaration, FieldInfo> EmittedGlobalVariables = new();

    public CodeGeneratorForIL(CompilerResult compilerResult, DiagnosticsCollection diagnostics, ILGeneratorSettings settings, ModuleBuilder? module) : base(compilerResult, diagnostics)
    {
        Builders = new();

        if (module is null)
        {
            AssemblyBuilder assemBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName()
            {
                Name = "BBLangGeneratedAssembly",
            }, AssemblyBuilderAccess.RunAndCollect);

            // Type daType = typeof(DebuggableAttribute);
            // ConstructorInfo daCtor = daType.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) })!;
            // CustomAttributeBuilder daBuilder = new(daCtor, new object[] {
            //     DebuggableAttribute.DebuggingModes.DisableOptimizations |
            //     DebuggableAttribute.DebuggingModes.Default });
            // assemBuilder.SetCustomAttribute(daBuilder);

            Module = assemBuilder.DefineDynamicModule("BBLangGeneratedModule");
        }
        else
        {
            Module = module;
        }

        TypeBuilder globalContextType = Module.DefineType("__GlobalContext", TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, typeof(object));

        HashSet<string> definedFields = new();

        string targetsFieldName = Utils.MakeUnique("targets", v => !definedFields.Contains(v));
        string memoryFieldName = Utils.MakeUnique("memory", v => !definedFields.Contains(v));

        globalContextType.DefineField(targetsFieldName, typeof(object[]), FieldAttributes.Assembly | FieldAttributes.Static);
        definedFields.Add(targetsFieldName);

        //globalContextType.DefineField(memoryFieldName, typeof(byte), FieldAttributes.Assembly);
        //definedFields.Add(memoryFieldName);

        Dictionary<CompiledVariableDeclaration, string> variableFieldMap = new();

        foreach (CompiledVariableDeclaration globalVariable in compilerResult.Statements.OfType<CompiledVariableDeclaration>())
        {
            if (!globalVariable.IsGlobal) continue;

            if (!ToType(globalVariable.Type, out Type? type, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(globalVariable));
                continue;
            }
            string fieldName = Utils.MakeUnique($"g_{globalVariable.Identifier}", v => !definedFields.Contains(v));
            variableFieldMap[globalVariable] = fieldName;
            globalContextType.DefineField(fieldName, type, FieldAttributes.Assembly | FieldAttributes.Static);
            definedFields.Add(fieldName);
        }

        GlobalContextType = globalContextType.CreateType();

        GlobalContextType_Targets = GlobalContextType.GetField(targetsFieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static) ?? throw new NullReferenceException();
        GlobalContextType_Targets.SetValue(null, Array.Empty<object>());

        foreach (KeyValuePair<CompiledVariableDeclaration, string> item in variableFieldMap)
        {
            EmittedGlobalVariables.Add(item.Key, GlobalContextType.GetField(item.Value, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static) ?? throw new NullReferenceException());
        }

        Settings = settings;
    }
}
