namespace LanguageCore.BBCode.Generator;

using Compiler;
using Parser;

public class UnusedFunctionManager
{
    readonly CompileLevel CompileLevel;
    readonly PrintCallback? Print;

    const int MaxIterations = 40;

    public UnusedFunctionManager(CompileLevel compileLevel, PrintCallback? printCallback)
    {
        CompileLevel = compileLevel;
        Print = printCallback;
    }

    void RemoveUnused<TFunction>(List<TFunction> functions, ref int functionsRemoved, Action<TFunction>? onRemove)
        where TFunction : ISameCheck, IReferenceable, IExportable, ISimpleReadable
    {
        static bool IsUsed(ISameCheck function, IEnumerable<Reference> references)
        {
            foreach (Reference reference in references)
            {
                if (reference.SourceContext == null ||
                    !function.IsSame(reference.SourceContext))
                { return true; }
            }
            return false;
        }

        for (int i = functions.Count - 1; i >= 0; i--)
        {
            TFunction function = functions[i];

            if (IsUsed(function, function.References))
            { continue; }

            if (CompileLevel == CompileLevel.All) continue;
            if (CompileLevel == CompileLevel.Exported && function.IsExport) continue;

            onRemove?.Invoke(function);

            functions.RemoveAt(i);
            functionsRemoved++;
        }
    }

    int DoTheThing(ref CompilerResult compilerResult)
    {
        Print?.Invoke($"  Remove unused functions ...", LogType.Debug);

        int functionsRemoved = 0;

        List<CompiledFunction> newFunctions = new(compilerResult.Functions);
        List<CompiledOperator> newOperators = new(compilerResult.Operators);
        List<CompiledGeneralFunction> newGeneralFunctions = new(compilerResult.GeneralFunctions);
        List<CompiledConstructor> newConstructors = new(compilerResult.Constructors);

        RemoveUnused(newFunctions, ref functionsRemoved, function => Print?.Invoke($"      Remove function {function.ToReadable()}", LogType.Debug));
        RemoveUnused(newOperators, ref functionsRemoved, function => Print?.Invoke($"      Remove operator {function.ToReadable()}", LogType.Debug));
        RemoveUnused(newGeneralFunctions, ref functionsRemoved, function => Print?.Invoke($"      Remove general function {function.ToReadable()}", LogType.Debug));
        RemoveUnused(newConstructors, ref functionsRemoved, function => Print?.Invoke($"      Remove constructor {function.ToReadable()}", LogType.Debug));

        compilerResult = new CompilerResult(
            newFunctions.ToArray(),
            compilerResult.Macros,
            newGeneralFunctions.ToArray(),
            newOperators.ToArray(),
            newConstructors.ToArray(),
            compilerResult.ExternalFunctions,
            compilerResult.Structs,
            compilerResult.Hashes,
            compilerResult.Enums,
            compilerResult.TopLevelStatements,
            compilerResult.File);

        return functionsRemoved;
    }

    public static void RemoveUnusedFunctions(
        ref CompilerResult compilerResult,
        PrintCallback? printCallback = null,
        CompileLevel level = CompileLevel.Minimal)
    {
        UnusedFunctionManager unusedFunctionManager = new(level, printCallback);

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            ReferenceCollector.CollectReferences(in compilerResult, printCallback);

            int functionsRemoved = unusedFunctionManager.DoTheThing(ref compilerResult);

            if (functionsRemoved == 0)
            {
                printCallback?.Invoke($"  Deletion of unused functions is complete", LogType.Debug);
                break;
            }

            printCallback?.Invoke($"  Removed {functionsRemoved} unused functions at iteration {iteration}", LogType.Debug);
        }

        ReferenceCollector.ClearReferences(in compilerResult);
    }
}
