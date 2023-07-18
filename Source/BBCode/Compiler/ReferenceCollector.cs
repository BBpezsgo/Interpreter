using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.BBCode.Compiler
{
    using ProgrammingLanguage.BBCode.Parser;
    using ProgrammingLanguage.BBCode.Parser.Statement;
    using ProgrammingLanguage.Core;
    using ProgrammingLanguage.Errors;

    internal class ReferenceCollector : CodeGeneratorBase
    {
        #region Fields

        readonly List<KeyValuePair<string, CompiledVariable>> compiledVariables;
        readonly List<CompiledParameter> parameters;

        IFunctionThing CurrentFunction;

        #endregion

        internal ReferenceCollector() : base()
        {
            compiledVariables = new List<KeyValuePair<string, CompiledVariable>>();
            parameters = new List<CompiledParameter>();
            CurrentFunction = null;
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

        CompiledVariable GetVariableInfo(VariableDeclaretion newVariable)
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

        void AnalyzeNewVariable(VariableDeclaretion newVariable)
        {
            if (newVariable.Type == "var")
            {
                if (newVariable.InitialValue == null)
                { throw new CompilerException($"Initial value for \"var\" variable declaration is requied", newVariable, CurrentFile); }

                if (newVariable.InitialValue is BBCode.Parser.Statement.Literal literal)
                {
                    newVariable.Type = TypeInstance.CreateAnonymous(literal.Type.ToStringRepresentation(), TypeDefinitionReplacer);
                }
                else if (newVariable.InitialValue is NewInstance newStruct)
                {
                    newVariable.Type = TypeInstance.CreateAnonymous(newStruct.TypeName.Content, TypeDefinitionReplacer);
                }
                else if (newVariable.InitialValue is ConstructorCall constructorCall)
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
                if (st is VariableDeclaretion newVar)
                {
                    AnalyzeNewVariable(newVar);
                    variablesAdded++;
                }
                else if (st is ForLoop forLoop)
                {
                    AnalyzeNewVariable(forLoop.VariableDeclaration);
                    variablesAdded++;
                }
            }

            foreach (var st in statements)
            {
                AnalyzeStatement(st);
                if (st is StatementWithBlock pr)
                {
                    AnalyzeStatements(pr.Block.Statements);
                }
            }

            for (int i = 0; i < variablesAdded; i++)
            {
                this.compiledVariables.Remove(this.compiledVariables.ElementAt(this.compiledVariables.Count - 1).Key);
            }
        }

        void AnalyzeStatement(Statement statement)
        {
            if (statement is ShortOperatorCall shortOperatorCall)
            {
                AnalyzeStatement(shortOperatorCall.ToAssignment());
            }
            else if (statement is ForLoop forLoop)
            {
                AnalyzeStatement(forLoop.VariableDeclaration);
                AnalyzeStatement(forLoop.Condition);
                AnalyzeStatement(forLoop.Expression);
            }
            else if (statement is IfContainer @if)
            {
                AnalyzeStatements(@if.Parts);
            }
            else if (statement is IfBranch ifIf)
            {
                AnalyzeStatement(ifIf.Condition);
            }
            else if (statement is ElseIfBranch ifElseIf)
            {
                AnalyzeStatement(ifElseIf.Condition);
            }
            else if (statement is ElseBranch)
            {

            }
            else if (statement is IndexCall index)
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
            else if (statement is VariableDeclaretion newVariable)
            {
                if (newVariable.InitialValue != null) AnalyzeStatement(newVariable.InitialValue);
            }
            else if (statement is OperatorCall @operator)
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
            else if (statement is CompoundAssignment compoundAssignment)
            {
                if (compoundAssignment.Left is IndexCall indexSetter)
                {
                    AnalyzeStatement(indexSetter.PrevStatement);
                    AnalyzeStatement(indexSetter.Expression);

                    CompiledType prevType = FindStatementType(indexSetter.PrevStatement);
                    CompiledType valueType = FindStatementType(compoundAssignment.Right);

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
                { AnalyzeStatement(compoundAssignment.Left); }
                AnalyzeStatement(compoundAssignment.Right);
            }
            else if (statement is Assignment setter)
            {
                if (setter.Left is IndexCall indexSetter)
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
            else if (statement is WhileLoop whileLoop)
            {
                AnalyzeStatement(whileLoop.Condition);
            }
            else if (statement is FunctionCall functionCall)
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
            else if (statement is KeywordCall keywordCall)
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

                if (keywordCall.FunctionName == "out")
                {
                    AnalyzeStatements(keywordCall.Parameters);

                    if (keywordCall.Parameters.Length != 1)
                    { return; }

                    if (!GetOutputWriter(FindStatementType(keywordCall.Parameters[0]), out var function))
                    { return; }

                    function.AddReference(keywordCall);

                    if (CurrentFunction == null || !function.IsSame(CurrentFunction))
                    { function.TimesUsed++; }
                    function.TimesUsedTotal++;
                }

            }
            else if (statement is Field field)
            { AnalyzeStatement(field.PrevStatement); }
            else if (statement is Identifier variable)
            {
                if (GetFunction(variable, out var function))
                {
                    // function.AddReference(variable);

                    if (CurrentFunction == null || !function.IsSame(CurrentFunction))
                    { function.TimesUsed++; }
                    function.TimesUsedTotal++;
                }
            }
            else if (statement is NewInstance)
            { }
            else if (statement is ConstructorCall constructorCall)
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
            else if (statement is BBCode.Parser.Statement.Literal)
            { }
            else if (statement is TypeCast @as)
            { AnalyzeStatement(@as.PrevStatement); }
            else if (statement is AddressGetter memoryAddressGetter)
            { AnalyzeStatement(memoryAddressGetter.PrevStatement); }
            else if (statement is Pointer memoryAddressFinder)
            { AnalyzeStatement(memoryAddressFinder.PrevStatement); }
            else if (statement is LiteralList listValue)
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
                ((IReferenceable<FunctionCall>)this.CompiledFunctions[i]).ClearReferences();
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
                ((IReferenceable<OperatorCall>)this.CompiledOperators[i]).ClearReferences();
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
