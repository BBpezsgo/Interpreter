
using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode.Compiler
{
    using IngameCoding.BBCode.Analysis;
    using IngameCoding.BBCode.Parser;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    internal class UnusedFunctionManager : CodeGeneratorBase
    {
        #region Fields

        Compiler.CompileLevel CompileLevel;

        List<Information> Informations;

        #endregion

        internal UnusedFunctionManager() : base() { }

        #region GenerateCodeFor...

        /// <returns>The variable's size</returns>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateCodeForVariable(Statement_NewVariable newVariable, bool isGlobal)
        {
            if (newVariable.Type.Type == TypeTokenType.AUTO)
            {
                if (newVariable.InitialValue != null)
                {
                    if (newVariable.InitialValue is Statement_Literal literal)
                    {
                        newVariable.Type.Type = literal.Type.Type;
                        newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);
                    }
                    else if (newVariable.InitialValue is Statement_NewInstance newInstance)
                    {
                        newVariable.Type.Type = TypeTokenType.USER_DEFINED;
                        newVariable.Type.Content = newInstance.TypeName.Content;
                        newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);
                    }
                    else if (newVariable.InitialValue is Statement_ConstructorCall constructorCall)
                    {
                        newVariable.Type.Type = TypeTokenType.USER_DEFINED;
                        newVariable.Type.Content = constructorCall.TypeName.Content;
                        newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);
                    }
                    else
                    {
                        var initialTypeRaw = FindStatementType(newVariable.InitialValue);

                        var initialType = Parser.ParseType(initialTypeRaw.Name);
                        newVariable.Type.Type = initialType.Type;
                        newVariable.Type.ListOf = initialType.ListOf;
                        newVariable.Type.Content = initialType.Content;

                        newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);

                        GenerateCodeForVariable(newVariable, isGlobal);
                        return 1;
                    }
                }
                else
                { throw new CompilerException($"Initial value for 'var' variable declaration is requied", newVariable.Type); }

                if (newVariable.Type.Type == TypeTokenType.AUTO)
                { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }

                GenerateCodeForVariable(newVariable, isGlobal);
                return 1;
            }

            newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);

            compiledVariables.Add(newVariable.VariableName.Content, GetVariableInfo(newVariable, GetVariableSizesSum(), isGlobal));

            return 0;
        }

        int GetVariableSizesSum()
        {
            int sum = 0;
            for (int i = 0; i < compiledVariables.Count; i++)
            {
                var key = compiledVariables.ElementAt(i).Key;
                if (compiledVariables.Get(key).IsGlobal) continue;
                if (compiledVariables.Get(key).Type.IsClass) sum++;
                else sum += compiledVariables.Get(key).Type.Size;
            }
            return sum;
        }

        #endregion

        CompiledVariable GetVariableInfo(Statement_NewVariable newVariable, int memoryOffset, bool isGlobal)
        {
            newVariable.VariableName.Analysis.CompilerReached = true;
            newVariable.Type.Analysis.CompilerReached = true;

            if (Keywords.Contains(newVariable.VariableName.Content))
            { throw new CompilerException($"Illegal variable name '{newVariable.VariableName.Content}'", newVariable.VariableName, CurrentFile); }

            bool inHeap = GetCompiledClass(newVariable.Type.Content, out _);
            CompiledType type = new(newVariable.Type, GetCustomType);

            return new CompiledVariable(
                memoryOffset,
                type,
                isGlobal,
                inHeap,
                newVariable);
        }

        CompiledFunction[] AnalyzeFunctions(CompiledFunction[] functions, Statement[] topLevelStatements, Action<string, Output.LogType> printCallback = null)
        {
            printCallback?.Invoke($"  Remove unused functions ...", Output.LogType.Debug);

            // Remove unused functions
            {
                FunctionDefinition currentFunction = null;

                void AnalyzeNewVariable(Statement_NewVariable newVariable)
                {
                    if (newVariable.Type.Type == TypeTokenType.AUTO)
                    {
                        if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                newVariable.Type.Type = literal.Type.Type;
                            }
                            else if (newVariable.InitialValue is Statement_NewInstance newStruct)
                            {
                                newVariable.Type.Type = TypeTokenType.USER_DEFINED;
                                newVariable.Type.Content = newStruct.TypeName.Content;
                            }
                            else if (newVariable.InitialValue is Statement_ConstructorCall constructorCall)
                            {
                                newVariable.Type.Type = TypeTokenType.USER_DEFINED;
                                newVariable.Type.Content = constructorCall.TypeName.Content;
                            }
                            else
                            {
                                var initialTypeRaw = FindStatementType(newVariable.InitialValue);
                                var initialType = Parser.ParseType(initialTypeRaw.Name);

                                newVariable.Type.Type = initialType.Type;
                                newVariable.Type.ListOf = initialType.ListOf;
                                newVariable.Type.Content = initialType.Content;
                            }
                        }
                        else
                        { throw new CompilerException($"Initial value for 'var' variable declaration is requied", newVariable.Type); }

                        if (newVariable.Type.Type == TypeTokenType.AUTO)
                        { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }
                    }
                    this.compiledVariables.Add(newVariable.VariableName.Content, GetVariableInfo(newVariable, -1, false));
                }
                void AnalyzeStatements(IEnumerable<Statement> statements)
                {
                    int variablesAdded = 0;
                    foreach (var st in statements)
                    {
                        if (st is Statement_NewVariable newVar)
                        {
                            AnalyzeNewVariable(newVar);
                            variablesAdded++;
                        }
                        else if (st is Statement_ForLoop forLoop)
                        {
                            AnalyzeNewVariable(forLoop.VariableDeclaration);
                            variablesAdded++;
                        }
                    }

                    foreach (var st in statements)
                    {
                        AnalyzeStatement(st);
                        if (st is StatementParent pr)
                        {
                            AnalyzeStatements(pr.Statements);
                        }
                    }

                    for (int i = 0; i < variablesAdded; i++)
                    {
                        this.compiledVariables.Remove(this.compiledVariables.ElementAt(this.compiledVariables.Count - 1).Key);
                    }
                }

                void AnalyzeStatement(Statement st)
                {
                    if (st is Statement_ForLoop st0)
                    {
                        AnalyzeStatement(st0.VariableDeclaration);
                        AnalyzeStatement(st0.Condition);
                        AnalyzeStatement(st0.Expression);
                    }
                    else if (st is Statement_If st1)
                    {
                        foreach (var st2 in st1.Parts)
                        { AnalyzeStatement(st2); }
                    }
                    else if (st is Statement_If_If st2)
                    {
                        AnalyzeStatement(st2.Condition);
                        AnalyzeStatements(st2.Statements);
                    }
                    else if (st is Statement_If_ElseIf st3)
                    {
                        AnalyzeStatement(st3.Condition);
                        AnalyzeStatements(st3.Statements);
                    }
                    else if (st is Statement_If_Else st3a)
                    {
                        AnalyzeStatements(st3a.Statements);
                    }
                    else if (st is Statement_Index st4)
                    {
                        AnalyzeStatement(st4.Expression);
                    }
                    else if (st is Statement_NewVariable st5)
                    {
                        if (st5.InitialValue != null) AnalyzeStatement(st5.InitialValue);
                    }
                    else if (st is Statement_Operator st6)
                    {
                        if (st6.Left != null) AnalyzeStatement(st6.Left);
                        if (st6.Right != null) AnalyzeStatement(st6.Right);
                    }
                    else if (st is Statement_Setter setter)
                    {
                        AnalyzeStatement(setter.Left);
                        AnalyzeStatement(setter.Right);
                    }
                    else if (st is Statement_WhileLoop st7)
                    {
                        AnalyzeStatement(st7.Condition);
                    }
                    else if (st is Statement_FunctionCall st8)
                    {
                        foreach (var st9 in st8.Parameters)
                        { AnalyzeStatement(st9); }

                        if (st8.PrevStatement != null)
                        { AnalyzeStatement(st8.PrevStatement); }

                        if (!BuiltinFunctions.Contains(st8.FunctionName) && GetCompiledFunction(st8, out var cf))
                        {
                            if (currentFunction != null)
                            {
                                if (cf.CheckID(currentFunction))
                                {
                                    cf.TimesUsed++;
                                }
                            }
                            else
                            {
                                cf.TimesUsed++;
                            }
                            cf.TimesUsedTotal++;
                        }
                    }
                    else if (st is Statement_KeywordCall keywordCall)
                    {
                        foreach (var parameter in keywordCall.Parameters)
                        { AnalyzeStatement(parameter); }

                        if (!BuiltinFunctions.Contains(keywordCall.FunctionName))
                        {
                            if (keywordCall.Parameters.Length > 0)
                            {
                                CompiledClass @class = FindStatementType(keywordCall.Parameters[0]).Class ?? throw new NullReferenceException();
                                if (GetDestructor(@class, out CompiledGeneralFunction function))
                                {
                                    function.TimesUsed++;
                                    function.TimesUsedTotal++;
                                }
                            }
                        }
                    }
                    else if (st is Statement_Field st9)
                    { AnalyzeStatement(st9.PrevStatement); }
                    else if (st is Statement_Variable)
                    { }
                    else if (st is Statement_NewInstance)
                    { }
                    else if (st is Statement_ConstructorCall constructorCall)
                    {
                        foreach (StatementWithReturnValue parameter in constructorCall.Parameters)
                        { AnalyzeStatement(parameter); }

                        if (GetConstructor(constructorCall, out CompiledGeneralFunction function))
                        {
                            function.TimesUsed++;
                            function.TimesUsedTotal++;
                        }
                    }
                    else if (st is Statement_Literal)
                    { }
                    else if (st is Statement_As @as)
                    { AnalyzeStatement(@as.PrevStatement); }
                    else if (st is Statement_MemoryAddressGetter)
                    { }
                    else if (st is Statement_MemoryAddressFinder)
                    { }
                    else if (st is Statement_ListValue st10)
                    { AnalyzeStatements(st10.Values); }
                    else
                    { throw new CompilerException($"Unknown statement {st.GetType().Name}", st, CurrentFile); }
                }

                foreach (var f in functions)
                {
                    if (CompiledFunctions.GetDefinition(f, out CompiledFunction compiledFunction))
                    { compiledFunction.TimesUsed = 0; }
                }

                foreach (var f in functions)
                {
                    parameters.Clear();
                    foreach (ParameterDefinition parameter in f.Parameters)
                    { parameters.Add(new CompiledParameter(-1, -1, -1, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter)); }
                    CurrentFile = f.FilePath;

                    currentFunction = f;
                    AnalyzeStatements(f.Statements);

                    currentFunction = null;
                    CurrentFile = null;
                    parameters.Clear();
                }

                AnalyzeStatements(topLevelStatements);
            }

            printCallback?.Invoke($"   Processing ...", Output.LogType.Debug);

            int functionsRemoved = 0;

            List<CompiledFunction> newFunctions = new(functions);
            for (int i = newFunctions.Count - 1; i >= 0; i--)
            {
                var element = newFunctions.ElementAt(i);

                if (!this.CompiledFunctions.GetDefinition(element, out CompiledFunction f)) continue;
                if (f.TimesUsed > 0) continue;
                if (f.TimesUsedTotal > 0) continue;
                foreach (var attr in f.CompiledAttributes)
                {
                    if (attr.Key == "CodeEntry") goto JumpOut;
                    if (attr.Key == "Catch") goto JumpOut;
                }

                if (CompileLevel == Compiler.CompileLevel.All) continue;
                if (CompileLevel == Compiler.CompileLevel.Exported && f.IsExport) continue;

                string readableID = element.ReadableID();

                printCallback?.Invoke($"      Remove function '{readableID}' ...", Output.LogType.Debug);
                Informations.Add(new Information($"Unused function '{readableID}' is not compiled", element.Identifier, element.FilePath));

                bool _ = newFunctions.Remove(element.Key);
                functionsRemoved++;

            JumpOut:;
            }

            return newFunctions.ToArray();
        }

        CompiledFunction[] RemoveUnusedFunctions_(
            Compiler.Result compilerResult,
            int iterations,
            Action<string, Output.LogType> printCallback)
        {
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int totalFunctions = this.CompiledFunctions.Length;
                this.CompiledFunctions = AnalyzeFunctions(this.CompiledFunctions, compilerResult.TopLevelStatements, printCallback);
                int functionsRemoved = totalFunctions - this.CompiledFunctions.Length;
                if (functionsRemoved == 0)
                {
                    printCallback?.Invoke($"  Deletion of unused functions is complete", Output.LogType.Debug);
                    break;
                }

                printCallback?.Invoke($"  Removed {functionsRemoved} unused functions (iteration {iteration})", Output.LogType.Debug);
            }

            return this.CompiledFunctions;
        }

        public static CompiledFunction[] RemoveUnusedFunctions(
            Compiler.Result compilerResult,
            int iterations,
            Action<string, Output.LogType> printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
        {
            UnusedFunctionManager unusedFunctionManager = new()
            {
                CompileLevel = level,

                CompiledClasses = compilerResult.Classes,
                CompiledStructs = compilerResult.Structs,
                CompiledFunctions = compilerResult.Functions,

                Informations = new List<Information>(),
            };

            return unusedFunctionManager.RemoveUnusedFunctions_(
                compilerResult,
                iterations,
                printCallback
                );
        }
    }
}
