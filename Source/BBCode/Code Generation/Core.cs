namespace LanguageCore.BBCode.Generator;

using Compiler;
using Runtime;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct CleanupItem
{
    public readonly int SizeOnStack;
    public readonly bool ShouldDeallocate;
    public readonly GeneralType? Type;

    public CleanupItem(int size, bool shouldDeallocate, GeneralType? type)
    {
        SizeOnStack = size;
        ShouldDeallocate = shouldDeallocate;
        Type = type;
    }

    public static CleanupItem Null => new(0, false, null);

    public override string ToString()
    {
        if (Type is null && SizeOnStack == 0 && !ShouldDeallocate) return "null";
        return $"({(ShouldDeallocate ? "temp " : string.Empty)}{Type} : {SizeOnStack} bytes)";
    }
    string GetDebuggerDisplay() => ToString();
}

public struct BBCodeGeneratorResult
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
    readonly CompileLevel CompileLevel;

    #endregion

    public CodeGeneratorForMain(CompilerResult compilerResult, GeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, settings, analysisCollection, print)
    {
        this.ExternalFunctions = compilerResult.ExternalFunctions.ToImmutableDictionary();
        this.GeneratedCode = new List<PreparationInstruction>();
        this.DebugInfo = new DebugInformation(compilerResult.Tokens);
        this.CleanupStack = new Stack<CleanupItem[]>();
        this.ReturnInstructions = new Stack<List<int>>();
        this.BreakInstructions = new Stack<List<int>>();
        this.UndefinedFunctionOffsets = new List<UndefinedOffset<CompiledFunction>>();
        this.UndefinedOperatorFunctionOffsets = new List<UndefinedOffset<CompiledOperator>>();
        this.UndefinedGeneralFunctionOffsets = new List<UndefinedOffset<CompiledGeneralFunction>>();
        this.UndefinedConstructorOffsets = new List<UndefinedOffset<CompiledConstructor>>();
        this.TagCount = new Stack<int>();
        this.CompileLevel = settings.CompileLevel;
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
                        BuiltinFunctionNames.Destructor => new InternalException($"Destructor for \"{generalFunction.Context}\" does not have instruction offset", item.CallerPosition, item.CurrentFile),
                        BuiltinFunctionNames.IndexerGet => new InternalException($"Index getter for \"{generalFunction.Context}\" does not have instruction offset", item.CallerPosition, item.CurrentFile),
                        BuiltinFunctionNames.IndexerSet => new InternalException($"Index setter for \"{generalFunction.Context}\" does not have instruction offset", item.CallerPosition, item.CurrentFile),
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

    BBCodeGeneratorResult GenerateCode(CompilerResult compilerResult, GeneratorSettings settings)
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
            CurrentFile = compilerResult.File;
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
        Print?.Invoke($"Generating top level statements for file {compilerResult.TopLevelStatements[^1].File?.ToString() ?? "null"} ...", LogType.Debug);
        GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements[^1].Statements, true);

        AddInstruction(Opcode.Exit);

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

        return new BBCodeGeneratorResult()
        {
            Code = GeneratedCode.Select(v => new Instruction(v)).ToImmutableArray(),
            DebugInfo = DebugInfo,
        };
    }

    public static BBCodeGeneratorResult Generate(
        CompilerResult compilerResult,
        GeneratorSettings settings,
        PrintCallback? printCallback = null,
        AnalysisCollection? analysisCollection = null)
    {
        // UnusedFunctionManager.RemoveUnusedFunctions(
        //     ref compilerResult,
        //     printCallback,
        //     settings.CompileLevel);

        return new CodeGeneratorForMain(compilerResult, settings, analysisCollection, printCallback).GenerateCode(compilerResult, settings);
    }
}
