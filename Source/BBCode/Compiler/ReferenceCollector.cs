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

            CompiledType type;

            if (newVariable.Type == "var")
            {
                if (newVariable.InitialValue == null)
                { throw new CompilerException($"Initial value is requied for variable declaration \"var\"", newVariable, newVariable.FilePath); }

                CompiledType initialValueType = FindStatementType(newVariable.InitialValue);

                type = initialValueType;
            }
            else
            {
                type = new(newVariable.Type, FindType);
            }

            return new CompiledVariable(
                -1,
                type,
                false,
                type.InHEAP,
                newVariable);
        }

        void AnalyzeNewVariable(VariableDeclaretion newVariable)
        {
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

                if (GetIndexGetter(prevType, out CompiledFunction indexer))
                {
                    indexer.AddReference(index);

                    if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                    { indexer.TimesUsed++; }
                    indexer.TimesUsedTotal++;
                }
                else if (GetIndexGetterTemplate(prevType, out CompileableTemplate<CompiledFunction> indexerTemplate))
                {
                    indexerTemplate = AddCompilable(indexerTemplate);

                    indexerTemplate.OriginalFunction.AddReference(index);
                    indexerTemplate.Function.AddReference(index);

                    if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                    {
                        indexerTemplate.OriginalFunction.TimesUsed++;
                        indexerTemplate.Function.TimesUsed++;
                    }
                    indexerTemplate.OriginalFunction.TimesUsedTotal++;
                    indexerTemplate.Function.TimesUsedTotal++;
                }
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
                else if (GetOperatorTemplate(@operator, out var compilableOperator))
                {
                    compilableOperator.Function.AddReference(@operator);

                    if (CurrentFunction == null || !compilableOperator.Function.IsSame(CurrentFunction))
                    { compilableOperator.Function.TimesUsed++; }
                    compilableOperator.Function.TimesUsedTotal++;

                    compilableOperator = AddCompilable(compilableOperator);
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

                    if (GetIndexSetter(prevType, valueType, out CompiledFunction indexer))
                    {
                        indexer.AddReference(indexSetter);

                        if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                        { indexer.TimesUsed++; }
                        indexer.TimesUsedTotal++;
                    }
                    else if (GetIndexSetterTemplate(prevType, valueType, out CompileableTemplate<CompiledFunction> indexerTemplate))
                    {
                        indexerTemplate = AddCompilable(indexerTemplate);

                        indexerTemplate.OriginalFunction.AddReference(indexSetter);
                        indexerTemplate.Function.AddReference(indexSetter);

                        if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                        {
                            indexerTemplate.OriginalFunction.TimesUsed++;
                            indexerTemplate.Function.TimesUsed++;
                        }
                        indexerTemplate.OriginalFunction.TimesUsedTotal++;
                        indexerTemplate.Function.TimesUsedTotal++;
                    }
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

                    if (GetIndexSetter(prevType, valueType, out CompiledFunction indexer))
                    {
                        indexer.AddReference(indexSetter);

                        if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                        { indexer.TimesUsed++; }
                        indexer.TimesUsedTotal++;
                    }
                    else if (GetIndexSetterTemplate(prevType, valueType, out CompileableTemplate<CompiledFunction> indexerTemplate))
                    {
                        indexerTemplate = AddCompilable(indexerTemplate);

                        indexerTemplate.OriginalFunction.AddReference(indexSetter);
                        indexerTemplate.Function.AddReference(indexSetter);

                        if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                        {
                            indexerTemplate.OriginalFunction.TimesUsed++;
                            indexerTemplate.Function.TimesUsed++;
                        }
                        indexerTemplate.OriginalFunction.TimesUsedTotal++;
                        indexerTemplate.Function.TimesUsedTotal++;
                    }
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

                if (functionCall.FunctionName == "sizeof")
                { return; }

                if (functionCall.FunctionName == "Alloc")
                { return; }

                if (GetFunction(functionCall, out CompiledFunction function))
                {
                    function.AddReference(functionCall);

                    if (CurrentFunction == null || !function.IsSame(CurrentFunction))
                    { function.TimesUsed++; }
                    function.TimesUsedTotal++;
                }
                else if (GetFunctionTemplate(functionCall, out var compilableFunction))
                {
                    compilableFunction.OriginalFunction.AddReference(functionCall);

                    if (CurrentFunction == null || !compilableFunction.OriginalFunction.IsSame(CurrentFunction))
                    {
                        compilableFunction.OriginalFunction.TimesUsed++;
                        compilableFunction.Function.TimesUsed++;
                    }
                    compilableFunction.OriginalFunction.TimesUsedTotal++;
                    compilableFunction.Function.TimesUsedTotal++;

                    compilableFunction = AddCompilable(compilableFunction);
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

                    if (GetGeneralFunction(paramType.Class, FindStatementTypes(keywordCall.Parameters), FunctionNames.Destructor, out var destructor))
                    {
                        if (!destructor.CanUse(CurrentFile))
                        { return; }

                        destructor.AddReference(keywordCall);

                        if (CurrentFunction == null || !destructor.IsSame(CurrentFunction))
                        { destructor.TimesUsed++; }
                        destructor.TimesUsedTotal++;

                        return;
                    }
                    else if (GetGeneralFunctionTemplate(paramType.Class, FindStatementTypes(keywordCall.Parameters), FunctionNames.Destructor, out var compilableGeneralFunction))
                    {
                        if (!compilableGeneralFunction.OriginalFunction.CanUse(CurrentFile))
                        { return; }

                        compilableGeneralFunction.OriginalFunction.AddReference(keywordCall);

                        if (CurrentFunction == null || !compilableGeneralFunction.OriginalFunction.IsSame(CurrentFunction))
                        {
                            compilableGeneralFunction.OriginalFunction.TimesUsed++;
                            compilableGeneralFunction.Function.TimesUsed++;
                        }
                        compilableGeneralFunction.OriginalFunction.TimesUsedTotal++;
                        compilableGeneralFunction.Function.TimesUsedTotal++;

                        compilableGeneralFunction = AddCompilable(compilableGeneralFunction);
                    }
                }

                if (keywordCall.FunctionName == "clone")
                {
                    if (keywordCall.Parameters.Length != 1)
                    { return; }

                    var paramType = FindStatementType(keywordCall.Parameters[0]);

                    if (!paramType.IsClass)
                    { return; }

                    if (!GetGeneralFunction(paramType.Class, FunctionNames.Cloner, out var cloner))
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
                if (GetFunction(variable.Name, out var function))
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

                if (!GetClass(constructorCall, out var @class))
                { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

                if (GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), FunctionNames.Constructor, out CompiledGeneralFunction constructor))
                {
                    constructor.AddReference(constructorCall);

                    if (CurrentFunction == null || !constructor.IsSame(CurrentFunction))
                    { constructor.TimesUsed++; }
                    constructor.TimesUsedTotal++;
                }
                else if (GetConstructorTemplate(@class, constructorCall, out var compilableGeneralFunction))
                {
                    compilableGeneralFunction.OriginalFunction.AddReference(constructorCall);

                    if (CurrentFunction == null || !compilableGeneralFunction.OriginalFunction.IsSame(CurrentFunction))
                    {
                        compilableGeneralFunction.OriginalFunction.TimesUsed++;
                        compilableGeneralFunction.Function.TimesUsed++;
                    }
                    compilableGeneralFunction.OriginalFunction.TimesUsedTotal++;
                    compilableGeneralFunction.Function.TimesUsedTotal++;

                    compilableGeneralFunction = AddCompilable(compilableGeneralFunction);
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

                if (function.IsTemplate)
                { continue; }

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { parameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
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

                if (function.IsTemplate)
                { continue; }

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { parameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
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

                if (function.IsTemplate)
                { continue; }

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { parameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.FilePath;
                CurrentFunction = function;

                AnalyzeStatements(function.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                parameters.Clear();
            }

            AnalyzeStatements(topLevelStatements);

            for (int i = 0; i < this.CompilableFunctions.Count; i++)
            {
                CompileableTemplate<CompiledFunction> function = this.CompilableFunctions[i];

                AddTypeArguments(function.TypeArguments);

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Function.Parameters)
                { parameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.Function.FilePath;
                CurrentFunction = function.Function;

                AnalyzeStatements(function.Function.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                parameters.Clear();
                TypeArguments.Clear();
            }

            for (int i = 0; i < this.CompilableOperators.Count; i++)
            {
                CompileableTemplate<CompiledOperator> function = this.CompilableOperators[i];

                AddTypeArguments(function.TypeArguments);

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Function.Parameters)
                { parameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.Function.FilePath;
                CurrentFunction = function.Function;

                AnalyzeStatements(function.Function.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                parameters.Clear();
                TypeArguments.Clear();
            }

            for (int i = 0; i < this.CompilableGeneralFunctions.Count; i++)
            {
                CompileableTemplate<CompiledGeneralFunction> function = this.CompilableGeneralFunctions[i];

                AddTypeArguments(function.TypeArguments);

                parameters.Clear();
                foreach (ParameterDefinition parameter in function.Function.Parameters)
                { parameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.Function.FilePath;
                CurrentFunction = function.Function;

                AnalyzeStatements(function.Function.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                parameters.Clear();
                TypeArguments.Clear();
            }
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
