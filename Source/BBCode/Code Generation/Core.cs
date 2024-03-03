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
    public Instruction[] Code;
    public DebugInformation DebugInfo;
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

    readonly ImmutableDictionary<string, ExternalFunctionBase> ExternalFunctions;
    readonly Dictionary<string, int> ExternalFunctionsCache;

    readonly Stack<CleanupItem[]> CleanupStack;
    ISameCheck? CurrentContext;

    readonly Stack<List<int>> ReturnInstructions;
    readonly Stack<List<int>> BreakInstructions;
    readonly Stack<bool> InMacro;

    readonly List<Instruction> GeneratedCode;

    readonly List<UndefinedOffset<CompiledFunction>> UndefinedFunctionOffsets;
    readonly List<UndefinedOffset<CompiledOperator>> UndefinedOperatorFunctionOffsets;
    readonly List<UndefinedOffset<CompiledGeneralFunction>> UndefinedGeneralFunctionOffsets;
    readonly List<UndefinedOffset<CompiledConstructor>> UndefinedConstructorOffsets;

    readonly bool TrimUnreachableCode = true;

    bool CanReturn;

    readonly DebugInformation GeneratedDebugInfo;
    readonly Stack<ScopeInformations> CurrentScopeDebug = new();
    readonly CompileLevel CompileLevel;

    #endregion

    public CodeGeneratorForMain(CompilerResult compilerResult, GeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, settings, analysisCollection, print)
    {
        this.ExternalFunctions = compilerResult.ExternalFunctions.ToImmutableDictionary();
        this.GeneratedCode = new List<Instruction>();
        this.ExternalFunctionsCache = new Dictionary<string, int>();
        this.GeneratedDebugInfo = new DebugInformation();
        this.CleanupStack = new Stack<CleanupItem[]>();
        this.ReturnInstructions = new Stack<List<int>>();
        this.BreakInstructions = new Stack<List<int>>();
        this.UndefinedFunctionOffsets = new List<UndefinedOffset<CompiledFunction>>();
        this.UndefinedOperatorFunctionOffsets = new List<UndefinedOffset<CompiledOperator>>();
        this.UndefinedGeneralFunctionOffsets = new List<UndefinedOffset<CompiledGeneralFunction>>();
        this.UndefinedConstructorOffsets = new List<UndefinedOffset<CompiledConstructor>>();
        this.InMacro = new Stack<bool>();
        this.TagCount = new Stack<int>();
        this.CompileLevel = settings.CompileLevel;
    }

    void GenerateExternalFunctionsCache(IEnumerable<string> usedExternalFunctions)
    {
        int offset = 0;
        AddComment($"Create external functions cache {{");
        foreach (string function in usedExternalFunctions)
        {
            AddComment($"Create string \"{function}\" {{");

            AddInstruction(Opcode.PUSH_VALUE, function.Length + 1);
            AddInstruction(Opcode.HEAP_ALLOC);

            ExternalFunctionsCache.Add(function, ExternalFunctionsCache.Count + 1);
            offset += function.Length;

            for (int i = 0; i < function.Length; i++)
            {
                // Prepare value
                AddInstruction(Opcode.PUSH_VALUE, new DataItem(function[i]));

                // Calculate pointer
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -2);
                AddInstruction(Opcode.PUSH_VALUE, i);
                AddInstruction(Opcode.MATH_ADD);

                // Set value
                AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
            }

            {
                // Prepare value
                AddInstruction(Opcode.PUSH_VALUE, new DataItem('\0'));

                // Calculate pointer
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -2);
                AddInstruction(Opcode.PUSH_VALUE, function.Length);
                AddInstruction(Opcode.MATH_ADD);

                // Set value
                AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
            }

            AddComment("}");
        }
        AddComment("}");
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

        AddInstruction(Opcode.PUSH_VALUE, new DataItem(0));

        if (settings.ExternalFunctionsCache)
        { GenerateExternalFunctionsCache(usedExternalFunctions); }

        CurrentFile = compilerResult.File;
#if DEBUG
        if (CurrentFile == null)
        { Debugger.Break(); }
#endif
        GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements);

        if (ExternalFunctionsCache.Count > 0)
        {
            AddComment("Clear external functions cache {");
            for (int i = 0; i < ExternalFunctionsCache.Count; i++)
            { AddInstruction(Opcode.HEAP_FREE); }
            AddComment("}");
        }

        AddInstruction(Opcode.EXIT);

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
            Code = GeneratedCode.ToArray(),
            DebugInfo = GeneratedDebugInfo,
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
