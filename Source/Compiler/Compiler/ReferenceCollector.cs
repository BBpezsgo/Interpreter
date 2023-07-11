using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.BBCode.Compiler
{
    using ProgrammingLanguage.BBCode.Parser;
    using ProgrammingLanguage.BBCode.Parser.Statements;
    using ProgrammingLanguage.Core;
    using ProgrammingLanguage.Errors;

    internal class ReferenceCollector : CodeGeneratorBase
    {
        #region Fields

        IFunctionThing CurrentFunction;

        #endregion

        internal ReferenceCollector() : base() { }

        CompiledVariable GetVariableInfo(Statement_NewVariable newVariable)
        {
            if (Constants.Keywords.Contains(newVariable.VariableName.Content))
            { throw new CompilerException($"Identifier \"{newVariable.VariableName.Content}\" reserved as a keyword, do not use it as a variable name", newVariable.VariableName, CurrentFile); }

            bool inHeap = GetClass(newVariable.Type.Identifier.Content, out _);
            CompiledType type = new(newVariable.Type, GetCustomType);

            return new CompiledVariable(
                -1,
                type,
                false,
                inHeap,
                newVariable);
        }

        void AnalyzeNewVariable(Statement_NewVariable newVariable)
        {
            if (newVariable.Type == "var")
            {
                if (newVariable.InitialValue == null)
                { throw new CompilerException($"Initial value for \"var\" variable declaration is requied", newVariable.Type.Identifier); }

                if (newVariable.InitialValue is Statement_Literal literal)
                {
                    newVariable.Type = TypeInstance.CreateAnonymous(literal.Type.ToStringRepresentation(), TypeDefinitionReplacer);
                }
                else if (newVariable.InitialValue is Statement_NewInstance newStruct)
                {
                    newVariable.Type = TypeInstance.CreateAnonymous(newStruct.TypeName.Content, TypeDefinitionReplacer);
                }
                else if (newVariable.InitialValue is Statement_ConstructorCall constructorCall)
                {
                    newVariable.Type = TypeInstance.CreateAnonymous(constructorCall.TypeName.Content, TypeDefinitionReplacer);
                }
                else
                {
                    CompiledType initialTypeRaw = FindStatementType(newVariable.InitialValue);
                    newVariable.Type = TypeInstance.CreateAnonymous(initialTypeRaw);
                }
            }

            if (newVariable.Type == "var")
            { throw new CompilerException("Invalid or unimplemented initial value", newVariable.InitialValue, newVariable.FilePath); }

            this.compiledVariables.Add(newVariable.VariableName.Content, GetVariableInfo(newVariable));
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

        void AnalyzeStatement(Statement statement)
        {
            if (statement is Statement_ForLoop forLoop)
            {
                AnalyzeStatement(forLoop.VariableDeclaration);
                AnalyzeStatement(forLoop.Condition);
                AnalyzeStatement(forLoop.Expression);
            }
            else if (statement is Statement_If @if)
            {
                AnalyzeStatements(@if.Parts);
            }
            else if (statement is Statement_If_If ifIf)
            {
                AnalyzeStatement(ifIf.Condition);
            }
            else if (statement is Statement_If_ElseIf ifElseIf)
            {
                AnalyzeStatement(ifElseIf.Condition);
            }
            else if (statement is Statement_If_Else)
            {

            }
            else if (statement is Statement_Index index)
            {
                AnalyzeStatement(index.PrevStatement);
                AnalyzeStatement(index.Expression);

                CompiledType prevType = FindStatementType(index.PrevStatement);

                if (!prevType.IsClass)
                { return; }

                if (!GetIndexGetter(prevType.Class, out CompiledFunction indexer))
                { return; }

                indexer.AddReference(index);

                if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                { indexer.TimesUsed++; }
                indexer.TimesUsedTotal++;
            }
            else if (statement is Statement_NewVariable newVariable)
            {
                if (newVariable.InitialValue != null) AnalyzeStatement(newVariable.InitialValue);
            }
            else if (statement is Statement_Operator @operator)
            {
                if (@operator.Left != null) AnalyzeStatement(@operator.Left);
                if (@operator.Right != null) AnalyzeStatement(@operator.Right);

                if (GetOperator(@operator, out CompiledOperator operatorDefinition))
                {
                    operatorDefinition.AddReference(@operator);

                    if (CurrentFunction == null || !operatorDefinition.IsSame(CurrentFunction))
                    { operatorDefinition.TimesUsed++; }
                    operatorDefinition.TimesUsedTotal++;
                }
            }
            else if (statement is Statement_Setter setter)
            {
                if (setter.Left is Statement_Index indexSetter)
                {
                    AnalyzeStatement(indexSetter.PrevStatement);
                    AnalyzeStatement(indexSetter.Expression);

                    CompiledType prevType = FindStatementType(indexSetter.PrevStatement);
                    CompiledType valueType = FindStatementType(setter.Right);

                    if (!prevType.IsClass)
                    { return; }

                    if (!GetIndexSetter(prevType.Class, valueType, out CompiledFunction indexer))
                    { return; }

                    indexer.AddReference(indexSetter);

                    if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                    { indexer.TimesUsed++; }
                    indexer.TimesUsedTotal++;
                }
                else
                { AnalyzeStatement(setter.Left); }
                AnalyzeStatement(setter.Right);
            }
            else if (statement is Statement_WhileLoop whileLoop)
            {
                AnalyzeStatement(whileLoop.Condition);
            }
            else if (statement is Statement_FunctionCall functionCall)
            {
                AnalyzeStatements(functionCall.Parameters);

                if (functionCall.PrevStatement != null)
                { AnalyzeStatement(functionCall.PrevStatement); }

                if (GetFunction(functionCall, out var function))
                {
                    function.AddReference(functionCall);

                    if (CurrentFunction == null || !function.IsSame(CurrentFunction))
                    { function.TimesUsed++; }
                    function.TimesUsedTotal++;
                }
            }
            else if (statement is Statement_KeywordCall keywordCall)
            {
                AnalyzeStatements(keywordCall.Parameters);

                if (keywordCall.FunctionName == "return")
                { return; }

                if (keywordCall.FunctionName == "throw")
                { return; }

                if (keywordCall.FunctionName == "break")
                { return; }

                if (keywordCall.FunctionName == "sizeof")
                { return; }

                if (keywordCall.FunctionName == "delete")
                {
                    if (keywordCall.Parameters.Length != 1)
                    { return; }

                    var paramType = FindStatementType(keywordCall.Parameters[0]);

                    if (!paramType.IsClass)
                    { return; }

                    if (!GetDestructor(paramType.Class, out var destructor))
                    { return; }

                    if (!destructor.CanUse(CurrentFile))
                    { return; }

                    destructor.AddReference(keywordCall);

                    if (CurrentFunction == null || !destructor.IsSame(CurrentFunction))
                    { destructor.TimesUsed++; }
                    destructor.TimesUsedTotal++;

                    return;
                }

                if (keywordCall.FunctionName == "clone")
                {
                    if (keywordCall.Parameters.Length != 1)
                    { return; }

                    var paramType = FindStatementType(keywordCall.Parameters[0]);

                    if (!paramType.IsClass)
                    { return; }

                    if (!GetCloner(paramType.Class, out var cloner))
                    { return; }

                    if (!cloner.CanUse(CurrentFile))
                    { return; }

                    cloner.AddReference(keywordCall);

                    if (CurrentFunction == null || !cloner.IsSame(CurrentFunction))
                    { cloner.TimesUsed++; }
                    cloner.TimesUsedTotal++;

                    return;
                }
            }
            else if (statement is Statement_Field field)
            { AnalyzeStatement(field.PrevStatement); }
            else if (statement is Statement_Variable variable)
            {
                if (GetFunction(variable, out var function))
                {
                    // function.AddReference(variable);

                    if (CurrentFunction == null || !function.IsSame(CurrentFunction))
                    { function.TimesUsed++; }
                    function.TimesUsedTotal++;
                }
            }
            else if (statement is Statement_NewInstance)
            { }
            else if (statement is Statement_ConstructorCall constructorCall)
            {
                AnalyzeStatements(constructorCall.Parameters);

                if (GetConstructor(constructorCall, out CompiledGeneralFunction function))
                {
                    function.AddReference(constructorCall);

                    if (CurrentFunction == null || !function.IsSame(CurrentFunction))
                    { function.TimesUsed++; }
                    function.TimesUsedTotal++;
                }
            }
            else if (statement is Statement_Literal)
            { }
            else if (statement is Statement_As @as)
            { AnalyzeStatement(@as.PrevStatement); }
            else if (statement is Statement_MemoryAddressGetter memoryAddressGetter)
            { AnalyzeStatement(memoryAddressGetter.PrevStatement); }
            else if (statement is Statement_MemoryAddressFinder memoryAddressFinder)
            { AnalyzeStatement(memoryAddressFinder.PrevStatement); }
            else if (statement is Statement_ListValue listValue)
            { AnalyzeStatements(listValue.Values); }
            else
            { throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile); }
        }

        void SearchForReferences(Statement[] topLevelStatements)
        {
            CurrentFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { parameters.Add(new CompiledParameter(-1, -1, -1, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter)); }
                CurrentFile = function.FilePath;
                CurrentFunction = function;

                AnalyzeStatements(function.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                parameters.Clear();
            }

            for (int i = 0; i < this.CompiledOperators.Length; i++)
            {
                CompiledOperator function = this.CompiledOperators[i];

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { parameters.Add(new CompiledParameter(-1, -1, -1, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter)); }
                CurrentFile = function.FilePath;
                CurrentFunction = function;

                AnalyzeStatements(function.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                parameters.Clear();
            }

            for (int i = 0; i < this.CompiledGeneralFunctions.Length; i++)
            {
                CompiledGeneralFunction function = this.CompiledGeneralFunctions[i];

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { parameters.Add(new CompiledParameter(-1, -1, -1, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter)); }
                CurrentFile = function.FilePath;
                CurrentFunction = function;

                AnalyzeStatements(function.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                parameters.Clear();
            }

            AnalyzeStatements(topLevelStatements);
        }

        void ResetReferences()
        {
            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                ((IReferenceable<Statement_FunctionCall>)this.CompiledFunctions[i]).ClearReferences();
                this.CompiledFunctions[i].TimesUsed = 0;
                this.CompiledFunctions[i].TimesUsedTotal = 0;
            }

            for (int i = 0; i < this.CompiledGeneralFunctions.Length; i++)
            {
                this.CompiledGeneralFunctions[i].ClearReferences();
                this.CompiledGeneralFunctions[i].TimesUsed = 0;
                this.CompiledGeneralFunctions[i].TimesUsedTotal = 0;
            }

            for (int i = 0; i < this.CompiledOperators.Length; i++)
            {
                ((IReferenceable<Statement_Operator>)this.CompiledOperators[i]).ClearReferences();
                this.CompiledOperators[i].TimesUsed = 0;
                this.CompiledOperators[i].TimesUsedTotal = 0;
            }
        }

        void AnalyzeFunctions(Statement[] topLevelStatements, Action<string, Output.LogType> printCallback = null)
        {
            printCallback?.Invoke($"  Collect references ...", Output.LogType.Debug);

            ResetReferences();

            SearchForReferences(topLevelStatements);
        }

        public static void CollectReferences(
            Compiler.Result compilerResult,
            Action<string, Output.LogType> printCallback = null)
        {
            ReferenceCollector referenceCollector = new()
            {
                CompiledClasses = compilerResult.Classes,
                CompiledStructs = compilerResult.Structs,

                CompiledFunctions = compilerResult.Functions,
                CompiledOperators = compilerResult.Operators,
                CompiledGeneralFunctions = compilerResult.GeneralFunctions,

                CompiledEnums = compilerResult.Enums,
            };

            referenceCollector.AnalyzeFunctions(compilerResult.TopLevelStatements, printCallback);
        }
    }
}
