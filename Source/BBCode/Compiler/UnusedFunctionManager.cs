using System;
using System.Collections.Generic;

namespace ProgrammingLanguage.BBCode.Compiler
{
    using ProgrammingLanguage.Errors;

    internal class UnusedFunctionManager : CodeGeneratorBase
    {
        #region Fields

        readonly List<KeyValuePair<string, CompiledVariable>> compiledVariables;
        readonly List<CompiledParameter> parameters;

        readonly Compiler.CompileLevel CompileLevel;

        readonly List<Information> Informations;

        #endregion

        internal UnusedFunctionManager(Compiler.CompileLevel compileLevel) : base()
        {
            compiledVariables = new List<KeyValuePair<string, CompiledVariable>>();
            parameters = new List<CompiledParameter>();

            CompileLevel = compileLevel;

            Informations = new List<Information>();
        }

        protected override bool GetLocalSymbolType(string symbolName, out CompiledType type)
        {
            if (GetVariable(symbolName, out CompiledVariable variable))
            {
                type = variable.Type;
                return true;
            }

            if (GetParameter(symbolName, out CompiledParameter parameter))
            {
                type = parameter.Type;
                return true;
            }

            type = null;
            return false;
        }

        bool GetVariable(string variableName, out CompiledVariable compiledVariable)
            => compiledVariables.TryGetValue(variableName, out compiledVariable);

        bool GetParameter(string parameterName, out CompiledParameter parameter)
            => parameters.TryGetValue(parameterName, out parameter);

        int DoTheThing(Output.PrintCallback printCallback = null)
        {
            printCallback?.Invoke($"  Remove unused functions ...", Output.LogType.Debug);

            int functionsRemoved = 0;

            {
                List<CompiledFunction> newFunctions = new(this.CompiledFunctions);

                for (int i = newFunctions.Count - 1; i >= 0; i--)
                {
                    CompiledFunction function = newFunctions[i];

                    if (function.TimesUsed > 0) continue;

                    if (function.CompiledAttributes.ContainsKey("CodeEntry")) continue;
                    if (function.CompiledAttributes.ContainsKey("Catch")) continue;

                    if (CompileLevel == Compiler.CompileLevel.All) continue;
                    if (CompileLevel == Compiler.CompileLevel.Exported && function.IsExport) continue;

                    string readableID = function.ReadableID();

                    printCallback?.Invoke($"      Remove function {readableID}", Output.LogType.Debug);
                    Informations.Add(new Information($"Unused function {readableID} is not compiled", function.Identifier, function.FilePath));

                    newFunctions.RemoveAt(i);
                    functionsRemoved++;
                }

                this.CompiledFunctions = newFunctions.ToArray();
            }

            {
                List<CompiledOperator> newOperators = new(this.CompiledOperators);

                for (int i = newOperators.Count - 1; i >= 0; i--)
                {
                    CompiledOperator @operator = newOperators[i];

                    if (@operator.TimesUsed > 0) continue;

                    if (CompileLevel == Compiler.CompileLevel.All) continue;
                    if (CompileLevel == Compiler.CompileLevel.Exported && @operator.IsExport) continue;

                    string readableID = @operator.ReadableID();

                    printCallback?.Invoke($"      Remove operator {readableID}", Output.LogType.Debug);
                    Informations.Add(new Information($"Unused operator {readableID} is not compiled", @operator.Identifier, @operator.FilePath));

                    newOperators.RemoveAt(i);
                    functionsRemoved++;
                }

                this.CompiledOperators = newOperators.ToArray();
            }

            {
                List<CompiledGeneralFunction> newGeneralFunctions = new(this.CompiledGeneralFunctions);

                for (int i = newGeneralFunctions.Count - 1; i >= 0; i--)
                {
                    CompiledGeneralFunction generalFunction = newGeneralFunctions[i];

                    if (generalFunction.TimesUsed > 0) continue;

                    if (CompileLevel == Compiler.CompileLevel.All) continue;
                    if (CompileLevel == Compiler.CompileLevel.Exported && generalFunction.IsExport) continue;

                    string readableID = generalFunction.ReadableID();

                    printCallback?.Invoke($"      Remove general function {readableID}", Output.LogType.Debug);
                    Informations.Add(new Information($"Unused general function  {readableID} is not compiled", generalFunction.Identifier, generalFunction.FilePath));

                    newGeneralFunctions.RemoveAt(i);
                    functionsRemoved++;
                }

                this.CompiledGeneralFunctions = newGeneralFunctions.ToArray();
            }

            return functionsRemoved;
        }

        public static (CompiledFunction[] functions, CompiledOperator[] operators, CompiledGeneralFunction[] generalFunctions) RemoveUnusedFunctions(
            Compiler.Result compilerResult,
            int iterations,
            Output.PrintCallback printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
        {
            UnusedFunctionManager unusedFunctionManager = new(level)
            {
                CompiledClasses = compilerResult.Classes,
                CompiledStructs = compilerResult.Structs,

                CompiledFunctions = compilerResult.Functions,
                CompiledMacros = compilerResult.Macros,
                CompiledOperators = compilerResult.Operators,
                CompiledGeneralFunctions = compilerResult.GeneralFunctions,

                CompiledEnums = compilerResult.Enums,
            };

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                ReferenceCollector.CollectReferences(compilerResult, printCallback);

                int functionsRemoved = unusedFunctionManager.DoTheThing(printCallback);
                if (functionsRemoved == 0)
                {
                    printCallback?.Invoke($"  Deletion of unused functions is complete", Output.LogType.Debug);
                    break;
                }

                printCallback?.Invoke($"  Removed {functionsRemoved} unused functions at iteration {iteration}", Output.LogType.Debug);

                compilerResult.Functions = unusedFunctionManager.CompiledFunctions;
                compilerResult.Operators = unusedFunctionManager.CompiledOperators;
                compilerResult.GeneralFunctions = unusedFunctionManager.CompiledGeneralFunctions;
            }

            return (unusedFunctionManager.CompiledFunctions, unusedFunctionManager.CompiledOperators, unusedFunctionManager.CompiledGeneralFunctions);
        }
    }
}
