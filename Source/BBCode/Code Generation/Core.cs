namespace LanguageCore.BBLang.Generator;

using Compiler;
using Runtime;

public partial class CodeGeneratorForMain : CodeGenerator
{
    /*
     *      
     *      === Stack Structure ===
     *      
     *        -- ENTRY --
     *      
     *        ? ... pointers ... (external function cache) > ExternalFunctionsCache.Count
     *      
     *        ? ... variables ... (global variables)
     *        
     *        -- CALL --
     *      
     *   -5    return value
     *      
     *   -4    ? parameter "this"    \ ParametersSize()
     *   -3    ? ... parameters ...  /
     *      
     *   -2    saved code pointer
     *   -1    saved base pointer
     *   
     *   >> 
     *   
     *   0    return flag
     *   
     *   1    ? ... variables ... (locals)
     *   
     */

    #region Fields

    readonly ImmutableDictionary<int, ExternalFunctionBase> ExternalFunctions;

    readonly Stack<ImmutableArray<CleanupItem>> CleanupStack;
    ISameCheck? CurrentContext;

    readonly Stack<List<int>> ReturnInstructions;
    readonly Stack<List<int>> BreakInstructions;

    readonly List<PreparationInstruction> GeneratedCode;

    readonly List<UndefinedOffset<CompiledFunction>> UndefinedFunctionOffsets;
    readonly List<UndefinedOffset<CompiledOperator>> UndefinedOperatorFunctionOffsets;
    readonly List<UndefinedOffset<CompiledGeneralFunction>> UndefinedGeneralFunctionOffsets;
    readonly List<UndefinedOffset<CompiledConstructor>> UndefinedConstructorOffsets;

    bool CanReturn;

    readonly Stack<ScopeInformations> CurrentScopeDebug = new();
    CompileLevel CompileLevel => Settings.CompileLevel;
    readonly MainGeneratorSettings Settings;

    #endregion

    public CodeGeneratorForMain(CompilerResult compilerResult, MainGeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, analysisCollection, print)
    {
        ExternalFunctions = compilerResult.ExternalFunctions.ToImmutableDictionary();
        GeneratedCode = new List<PreparationInstruction>();
        DebugInfo = new DebugInformation(compilerResult.Raw.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.Key, v.Value.Tokens)));
        CleanupStack = new Stack<ImmutableArray<CleanupItem>>();
        ReturnInstructions = new Stack<List<int>>();
        BreakInstructions = new Stack<List<int>>();
        UndefinedFunctionOffsets = new List<UndefinedOffset<CompiledFunction>>();
        UndefinedOperatorFunctionOffsets = new List<UndefinedOffset<CompiledOperator>>();
        UndefinedGeneralFunctionOffsets = new List<UndefinedOffset<CompiledGeneralFunction>>();
        UndefinedConstructorOffsets = new List<UndefinedOffset<CompiledConstructor>>();
        TagCount = new Stack<int>();
        Settings = settings;
    }

    void SetUndefinedFunctionOffsets<TFunction>(IEnumerable<UndefinedOffset<TFunction>> undefinedOffsets)
        where TFunction : IHaveInstructionOffset
    {
        foreach (UndefinedOffset<TFunction> item in undefinedOffsets)
        {
            if (item.Called.InstructionOffset == -1)
            {
                if (item.Called is Parser.GeneralFunctionDefinition generalFunction)
                {
                    throw generalFunction.Identifier.Content switch
                    {
                        BuiltinFunctionIdentifiers.Destructor => new InternalException($"Destructor for \"{generalFunction.Context}\" does not have instruction offset", item.CallerPosition, item.CurrentFile),
                        BuiltinFunctionIdentifiers.IndexerGet => new InternalException($"Index getter for \"{generalFunction.Context}\" does not have instruction offset", item.CallerPosition, item.CurrentFile),
                        BuiltinFunctionIdentifiers.IndexerSet => new InternalException($"Index setter for \"{generalFunction.Context}\" does not have instruction offset", item.CallerPosition, item.CurrentFile),
                        _ => new NotImplementedException(),
                    };
                }

                string thingName = item.Called switch
                {
                    CompiledOperator => "Operator",
                    CompiledConstructor => "Constructor",
                    _ => "Function",
                };

                if (item.Called is ISimpleReadable simpleReadable)
                { throw new InternalException($"{thingName} {simpleReadable.ToReadable()} does not have instruction offset", item.CallerPosition, item.CurrentFile); }

                throw new InternalException($"{thingName} {item.Called} does not have instruction offset", item.CallerPosition, item.CurrentFile);
            }

            int offset = item.IsAbsoluteAddress ? item.Called.InstructionOffset : item.Called.InstructionOffset - item.InstructionIndex;
            GeneratedCode[item.InstructionIndex].Parameter = offset;
        }
    }

    public static BBLangGeneratorResult Generate(
        CompilerResult compilerResult,
        MainGeneratorSettings settings,
        PrintCallback? printCallback = null,
        AnalysisCollection? analysisCollection = null)
    {
        CodeGeneratorForMain generator = new(compilerResult, settings, analysisCollection, printCallback);
        return generator.GenerateCode(compilerResult, settings);
    }
}
