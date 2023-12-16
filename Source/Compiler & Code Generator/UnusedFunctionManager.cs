using System.Collections.Generic;

namespace LanguageCore.BBCode.Generator
{
    using LanguageCore.Compiler;

    public class UnusedFunctionManager
    {
        #region Fields

        readonly CompileLevel CompileLevel;

        readonly List<Information> Informations;
        readonly PrintCallback? PrintCallback;

        #endregion

        public UnusedFunctionManager(CompileLevel compileLevel, PrintCallback? printCallback) : base()
        {
            CompileLevel = compileLevel;
            PrintCallback = printCallback;

            Informations = new List<Information>();
        }

        int DoTheThing(ref CompilerResult compilerResult)
        {
            PrintCallback?.Invoke($"  Remove unused functions ...", LogType.Debug);

            int functionsRemoved = 0;

            List<CompiledFunction> newFunctions;
            List<CompiledOperator> newOperators;
            List<CompiledGeneralFunction> newGeneralFunctions;

            {
                newFunctions = new(compilerResult.Functions);

                for (int i = newFunctions.Count - 1; i >= 0; i--)
                {
                    CompiledFunction function = newFunctions[i];

                    if (function.TimesUsed > 0) continue;

                    if (function.CompiledAttributes.ContainsKey("Catch")) continue;

                    if (CompileLevel == CompileLevel.All) continue;
                    if (CompileLevel == CompileLevel.Exported && function.IsExport) continue;

                    string readableID = function.ReadableID();

                    PrintCallback?.Invoke($"      Remove function {readableID}", LogType.Debug);
                    Informations.Add(new Information($"Unused function {readableID} is not compiled", function.Identifier, function.FilePath));

                    newFunctions.RemoveAt(i);
                    functionsRemoved++;
                }
            }

            {
                newOperators = new(compilerResult.Operators);

                for (int i = newOperators.Count - 1; i >= 0; i--)
                {
                    CompiledOperator @operator = newOperators[i];

                    if (@operator.TimesUsed > 0) continue;

                    if (CompileLevel == CompileLevel.All) continue;
                    if (CompileLevel == CompileLevel.Exported && @operator.IsExport) continue;

                    string readableID = @operator.ReadableID();

                    PrintCallback?.Invoke($"      Remove operator {readableID}", LogType.Debug);
                    Informations.Add(new Information($"Unused operator {readableID} is not compiled", @operator.Identifier, @operator.FilePath));

                    newOperators.RemoveAt(i);
                    functionsRemoved++;
                }
            }

            {
                newGeneralFunctions = new(compilerResult.GeneralFunctions);

                for (int i = newGeneralFunctions.Count - 1; i >= 0; i--)
                {
                    CompiledGeneralFunction generalFunction = newGeneralFunctions[i];

                    if (generalFunction.TimesUsed > 0) continue;

                    if (CompileLevel == CompileLevel.All) continue;
                    if (CompileLevel == CompileLevel.Exported && generalFunction.IsExport) continue;

                    string readableID = generalFunction.ReadableID();

                    PrintCallback?.Invoke($"      Remove general function {readableID}", LogType.Debug);
                    Informations.Add(new Information($"Unused general function  {readableID} is not compiled", generalFunction.Identifier, generalFunction.FilePath));

                    newGeneralFunctions.RemoveAt(i);
                    functionsRemoved++;
                }
            }

            compilerResult = new CompilerResult(
                newFunctions.ToArray(),
                compilerResult.Macros,
                newGeneralFunctions.ToArray(),
                newOperators.ToArray(),
                compilerResult.ExternalFunctions,
                compilerResult.Structs,
                compilerResult.Classes,
                compilerResult.Hashes,
                compilerResult.Enums,
                compilerResult.Errors,
                compilerResult.Warnings,
                compilerResult.TopLevelStatements);

            return functionsRemoved;
        }

        public static (CompiledFunction[] functions, CompiledOperator[] operators, CompiledGeneralFunction[] generalFunctions) RemoveUnusedFunctions(
            CompilerResult compilerResult,
            int iterations,
            PrintCallback? printCallback = null,
            CompileLevel level = CompileLevel.Minimal)
        {
            UnusedFunctionManager unusedFunctionManager = new(level, printCallback);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                ReferenceCollector.CollectReferences(compilerResult, printCallback);

                int functionsRemoved = unusedFunctionManager.DoTheThing(ref compilerResult);

                if (functionsRemoved == 0)
                {
                    printCallback?.Invoke($"  Deletion of unused functions is complete", LogType.Debug);
                    break;
                }

                printCallback?.Invoke($"  Removed {functionsRemoved} unused functions at iteration {iteration}", LogType.Debug);
            }

            ReferenceCollector.ClearReferences(compilerResult);

            return (compilerResult.Functions, compilerResult.Operators, compilerResult.GeneralFunctions);
        }
    }
}
