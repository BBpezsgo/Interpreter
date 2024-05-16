namespace LanguageCore.BBLang.Generator;

using Compiler;
using Runtime;

public struct MainGeneratorSettings
{
    public bool GenerateComments;
    public bool PrintInstructions;
    public bool DontOptimize;
    public bool GenerateDebugInstructions;
    public bool ExternalFunctionsCache;
    public bool CheckNullPointers;
    public CompileLevel CompileLevel;

    public MainGeneratorSettings(MainGeneratorSettings other)
    {
        GenerateComments = other.GenerateComments;
        PrintInstructions = other.PrintInstructions;
        DontOptimize = other.DontOptimize;
        GenerateDebugInstructions = other.GenerateDebugInstructions;
        ExternalFunctionsCache = other.ExternalFunctionsCache;
        CheckNullPointers = other.CheckNullPointers;
        CompileLevel = other.CompileLevel;
    }

    public static MainGeneratorSettings Default => new()
    {
        GenerateComments = true,
        PrintInstructions = false,
        DontOptimize = false,
        GenerateDebugInstructions = true,
        ExternalFunctionsCache = false,
        CheckNullPointers = true,
        CompileLevel = CompileLevel.Minimal,
    };
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct CleanupItem
{
    public readonly int SizeOnStack;
    public readonly bool ShouldDeallocate;
    public readonly GeneralType? Type;

    public static CleanupItem Null => new(0, false, null);

    public CleanupItem(int size, bool shouldDeallocate, GeneralType? type)
    {
        SizeOnStack = size;
        ShouldDeallocate = shouldDeallocate;
        Type = type;
    }

    public override string ToString()
    {
        if (Type is null && SizeOnStack == 0 && !ShouldDeallocate) return "null";
        return $"({(ShouldDeallocate ? "temp " : string.Empty)}{Type} : {SizeOnStack} bytes)";
    }
    string GetDebuggerDisplay() => ToString();
}

public struct BBLangGeneratorResult
{
    public ImmutableArray<Instruction> Code;
    public DebugInformation? DebugInfo;
}

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

    readonly Stack<CleanupItem[]> CleanupStack;
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
        this.ExternalFunctions = compilerResult.ExternalFunctions.ToImmutableDictionary();
        this.GeneratedCode = new List<PreparationInstruction>();
        this.DebugInfo = new DebugInformation(compilerResult.Raw.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.Key, v.Value.Tokens)));
        this.CleanupStack = new Stack<CleanupItem[]>();
        this.ReturnInstructions = new Stack<List<int>>();
        this.BreakInstructions = new Stack<List<int>>();
        this.UndefinedFunctionOffsets = new List<UndefinedOffset<CompiledFunction>>();
        this.UndefinedOperatorFunctionOffsets = new List<UndefinedOffset<CompiledOperator>>();
        this.UndefinedGeneralFunctionOffsets = new List<UndefinedOffset<CompiledGeneralFunction>>();
        this.UndefinedConstructorOffsets = new List<UndefinedOffset<CompiledConstructor>>();
        this.TagCount = new Stack<int>();
        this.Settings = settings;
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

    BBLangGeneratorResult GenerateCode(CompilerResult compilerResult, MainGeneratorSettings settings)
    {
        if (settings.ExternalFunctionsCache)
        { throw new NotImplementedException(); }

        Print?.Invoke("Generating code ...", LogType.Debug);

        List<string> usedExternalFunctions = new();

        foreach (CompiledFunction function in this.CompiledFunctions)
        {
            if (function.IsExternal)
            { usedExternalFunctions.Add(function.ExternalFunctionName); }
        }

        foreach (CompiledOperator @operator in this.CompiledOperators)
        {
            if (@operator.IsExternal)
            { usedExternalFunctions.Add(@operator.ExternalFunctionName); }
        }

        foreach ((ImmutableArray<Parser.Statement.Statement> statements, _) in compilerResult.TopLevelStatements)
        {
            CompileGlobalConstants(statements);
        }

        for (int i = 0; i < compilerResult.TopLevelStatements.Length - 1; i++)
        {
            (ImmutableArray<Parser.Statement.Statement> statements, Uri? file) = compilerResult.TopLevelStatements[i];
            CurrentFile = file;
#if DEBUG
            if (CurrentFile == null)
            { Debugger.Break(); }
#endif
            Print?.Invoke($"Generating top level statements for file {file?.ToString() ?? "null"} ...", LogType.Debug);
            GenerateCodeForTopLevelStatements(statements, false);

            CurrentFile = null;
        }

        CurrentFile = compilerResult.File;

        AddInstruction(Opcode.Push, new DataItem(0));

#if DEBUG
        if (CurrentFile == null)
        { Debugger.Break(); }
#endif
        if (compilerResult.TopLevelStatements.Length > 0)
        {
            Print?.Invoke($"Generating top level statements for file {compilerResult.TopLevelStatements[^1].File?.ToString() ?? "null"} ...", LogType.Debug);
            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements[^1].Statements, true);
            AddInstruction(Opcode.Exit);
        }

        while (true)
        {
            bool generatedAnything = false;

            generatedAnything = GenerateCodeForFunctions(CompiledFunctions) || generatedAnything;
            generatedAnything = GenerateCodeForFunctions(CompiledOperators) || generatedAnything;
            generatedAnything = GenerateCodeForFunctions(CompiledGeneralFunctions) || generatedAnything;
            generatedAnything = GenerateCodeForFunctions(CompiledConstructors) || generatedAnything;

            generatedAnything = GenerateCodeForCompilableFunctions(CompilableFunctions) || generatedAnything;
            generatedAnything = GenerateCodeForCompilableFunctions(CompilableConstructors) || generatedAnything;
            generatedAnything = GenerateCodeForCompilableFunctions(CompilableOperators) || generatedAnything;
            generatedAnything = GenerateCodeForCompilableFunctions(CompilableGeneralFunctions) || generatedAnything;

            if (!generatedAnything) break;
        }

        SetUndefinedFunctionOffsets(UndefinedFunctionOffsets);
        SetUndefinedFunctionOffsets(UndefinedConstructorOffsets);
        SetUndefinedFunctionOffsets(UndefinedOperatorFunctionOffsets);
        SetUndefinedFunctionOffsets(UndefinedGeneralFunctionOffsets);

        Print?.Invoke("Code generated", LogType.Debug);

        return new BBLangGeneratorResult()
        {
            Code = GeneratedCode.Select(v => new Instruction(v)).ToImmutableArray(),
            DebugInfo = DebugInfo,
        };
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
