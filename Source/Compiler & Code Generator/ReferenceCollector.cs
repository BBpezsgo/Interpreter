using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.Compiler
{
    using BBCode.Generator;
    using Parser;
    using Parser.Statement;

    public class ReferenceCollector : CodeGeneratorNonGeneratorBase
    {
        #region Fields

        ISameCheck? CurrentFunction;

        #endregion

        public ReferenceCollector() : base()
        {
            CurrentFunction = null;
        }

        CompiledVariable GetVariableInfo(VariableDeclaration newVariable)
        {
            if (LanguageConstants.Keywords.Contains(newVariable.VariableName.Content))
            { throw new CompilerException($"Identifier \"{newVariable.VariableName.Content}\" reserved as a keyword, do not use it as a variable name", newVariable.VariableName, CurrentFile); }

            CompiledType type;

            if (newVariable.Type == "var")
            {
                if (newVariable.InitialValue == null)
                { throw new CompilerException($"Initial value is required for variable declaration \"var\"", newVariable, newVariable.FilePath); }

                CompiledType initialValueType = FindStatementType(newVariable.InitialValue);

                type = initialValueType;
            }
            else
            {
                type = new(newVariable.Type, FindType, TryCompute);
            }

            return new CompiledVariable(-1, type, newVariable);
        }

        void AnalyzeNewVariable(VariableDeclaration newVariable)
        {
            this.CompiledVariables.Add(GetVariableInfo(newVariable));
        }

        int AnalyzeNewVariables(IEnumerable<Statement>? statements)
        {
            if (statements == null) return 0;

            int variablesAdded = 0;
            foreach (Statement st in statements)
            {
                if (st is VariableDeclaration newVar)
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
            return variablesAdded;
        }

        void AnalyzeStatements(IEnumerable<Statement>? statements)
        {
            if (statements == null) return;

            int variablesAdded = AnalyzeNewVariables(statements);

            foreach (Statement statement in statements)
            {
                AnalyzeStatement(statement);

                if (statement is StatementWithBlock blockStatement)
                { AnalyzeStatements(blockStatement.Block.Statements); }

                if (statement is StatementWithAnyBlock anyBlockStatement &&
                    anyBlockStatement.Block is Block block)
                { AnalyzeStatements(block.Statements); }
            }

            for (int i = 0; i < variablesAdded; i++)
            { this.CompiledVariables.RemoveAt(this.CompiledVariables.Count - 1); }
        }

        void AnalyzeStatements(Statement[] statements, CompiledType[] expectedTypes)
        {
            int variablesAdded = AnalyzeNewVariables(statements);

            for (int i = 0; i < statements.Length; i++)
            {
                CompiledType? expectedType = null;
                if (i < expectedTypes.Length) expectedType = expectedTypes[i];

                AnalyzeStatement(statements[i], expectedType);

                if (statements[i] is StatementWithBlock blockStatement)
                { AnalyzeStatements(blockStatement.Block.Statements); }

                if (statements[i] is StatementWithAnyBlock anyBlockStatement &&
                    anyBlockStatement.Block is Block block)
                { AnalyzeStatements(block.Statements); }
            }

            for (int i = 0; i < variablesAdded; i++)
            { this.CompiledVariables.RemoveAt(this.CompiledVariables.Count - 1); }
        }

        void AnalyzeStatement(Statement? statement, CompiledType? expectedType = null)
        {
            if (statement == null) return;

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

                if (GetIndexGetter(prevType, out CompiledFunction? indexer))
                {
                    indexer.AddReference(index, CurrentFile);

                    if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                    { indexer.TimesUsed++; }
                    indexer.TimesUsedTotal++;
                }
                else if (GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                {
                    indexerTemplate = AddCompilable(indexerTemplate);

                    indexerTemplate.OriginalFunction.AddReference(index, CurrentFile);
                    indexerTemplate.Function.AddReference(index, CurrentFile);

                    if (CurrentFunction == null || !indexerTemplate.Function.IsSame(CurrentFunction))
                    {
                        indexerTemplate.OriginalFunction.TimesUsed++;
                        indexerTemplate.Function.TimesUsed++;
                    }
                    indexerTemplate.OriginalFunction.TimesUsedTotal++;
                    indexerTemplate.Function.TimesUsedTotal++;
                }
            }
            else if (statement is VariableDeclaration newVariable)
            {
                if (newVariable.InitialValue != null) AnalyzeStatement(newVariable.InitialValue);
            }
            else if (statement is OperatorCall @operator)
            {
                if (@operator.Left != null) AnalyzeStatement(@operator.Left);
                if (@operator.Right != null) AnalyzeStatement(@operator.Right);

                if (GetOperator(@operator, out CompiledOperator? operatorDefinition))
                {
                    operatorDefinition.AddReference(@operator, CurrentFile);

                    if (CurrentFunction == null || !operatorDefinition.IsSame(CurrentFunction))
                    { operatorDefinition.TimesUsed++; }
                    operatorDefinition.TimesUsedTotal++;
                }
                else if (GetOperatorTemplate(@operator, out CompliableTemplate<CompiledOperator> compilableOperator))
                {
                    compilableOperator.Function.AddReference(@operator, CurrentFile);

                    if (CurrentFunction == null || !compilableOperator.Function.IsSame(CurrentFunction))
                    { compilableOperator.Function.TimesUsed++; }
                    compilableOperator.Function.TimesUsedTotal++;

                    AddCompilable(compilableOperator);
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

                    if (GetIndexSetter(prevType, valueType, out CompiledFunction? indexer))
                    {
                        indexer.AddReference(indexSetter, CurrentFile);

                        if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                        { indexer.TimesUsed++; }
                        indexer.TimesUsedTotal++;
                    }
                    else if (GetIndexSetterTemplate(prevType, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                    {
                        indexerTemplate = AddCompilable(indexerTemplate);

                        indexerTemplate.OriginalFunction.AddReference(indexSetter, CurrentFile);
                        indexerTemplate.Function.AddReference(indexSetter, CurrentFile);

                        if (CurrentFunction == null || !indexerTemplate.OriginalFunction.IsSame(CurrentFunction))
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

                    if (GetIndexSetter(prevType, valueType, out CompiledFunction? indexer))
                    {
                        indexer.AddReference(indexSetter, CurrentFile);

                        if (CurrentFunction == null || !indexer.IsSame(CurrentFunction))
                        { indexer.TimesUsed++; }
                        indexer.TimesUsedTotal++;
                    }
                    else if (GetIndexSetterTemplate(prevType, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                    {
                        indexerTemplate = AddCompilable(indexerTemplate);

                        indexerTemplate.OriginalFunction.AddReference(indexSetter, CurrentFile);
                        indexerTemplate.Function.AddReference(indexSetter, CurrentFile);

                        if (CurrentFunction == null || !indexerTemplate.OriginalFunction.IsSame(CurrentFunction))
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
                if (TryGetFunction(functionCall.Identifier, functionCall.MethodParameters.Length, out CompiledFunction? possibleFunction))
                {
                    AnalyzeStatements(functionCall.Parameters, possibleFunction.ParameterTypes);
                }
                else
                {
                    AnalyzeStatements(functionCall.Parameters);
                }

                if (functionCall.PrevStatement != null)
                { AnalyzeStatement(functionCall.PrevStatement); }

                if (functionCall.FunctionName == "sizeof")
                { return; }

                if (GetParameter(functionCall.Identifier.Content, out _))
                { return; }

                if (GetVariable(functionCall.Identifier.Content, out _))
                { return; }

                if (TryGetMacro(functionCall, out MacroDefinition? macro))
                {
                    Statement inlinedMacro = InlineMacro(macro, functionCall.Parameters);
                    if (inlinedMacro is Block block)
                    { AnalyzeStatements(block.Statements); }
                    else
                    { AnalyzeStatement(inlinedMacro, expectedType); }
                    return;
                }

                if (GetFunction(functionCall, out CompiledFunction? function))
                {
                    function.AddReference(functionCall, CurrentFile);

                    if (CurrentFunction == null || !function.IsSame(CurrentFunction))
                    { function.TimesUsed++; }
                    function.TimesUsedTotal++;
                }
                else if (GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
                {
                    compilableFunction.OriginalFunction.AddReference(functionCall, CurrentFile);

                    if (CurrentFunction == null || !compilableFunction.OriginalFunction.IsSame(CurrentFunction))
                    {
                        compilableFunction.OriginalFunction.TimesUsed++;
                        compilableFunction.Function.TimesUsed++;
                    }
                    compilableFunction.OriginalFunction.TimesUsedTotal++;
                    compilableFunction.Function.TimesUsedTotal++;

                    AddCompilable(compilableFunction);
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

                    CompiledType paramType = FindStatementType(keywordCall.Parameters[0]);

                    if (paramType == Type.Integer)
                    {
                        if (TryGetBuiltinFunction("free", out CompiledFunction? function))
                        {
                            function.AddReference(keywordCall, CurrentFile);

                            if (CurrentFunction == null || !function.IsSame(CurrentFunction))
                            { function.TimesUsed++; }
                            function.TimesUsedTotal++;
                        }
                        return;
                    }

                    if (!paramType.IsClass)
                    { return; }

                    if (GetGeneralFunction(paramType.Class, FindStatementTypes(keywordCall.Parameters), BuiltinFunctionNames.Destructor, out CompiledGeneralFunction? destructor))
                    {
                        if (!destructor.CanUse(CurrentFile))
                        { return; }

                        destructor.AddReference(keywordCall, CurrentFile);

                        if (CurrentFunction == null || !destructor.IsSame(CurrentFunction))
                        { destructor.TimesUsed++; }
                        destructor.TimesUsedTotal++;

                        return;
                    }
                    else if (GetGeneralFunctionTemplate(paramType.Class, FindStatementTypes(keywordCall.Parameters), BuiltinFunctionNames.Destructor, out CompliableTemplate<CompiledGeneralFunction> compilableGeneralFunction))
                    {
                        if (!compilableGeneralFunction.OriginalFunction.CanUse(CurrentFile))
                        { return; }

                        compilableGeneralFunction.OriginalFunction.AddReference(keywordCall, CurrentFile);

                        if (CurrentFunction == null || !compilableGeneralFunction.OriginalFunction.IsSame(CurrentFunction))
                        {
                            compilableGeneralFunction.OriginalFunction.TimesUsed++;
                            compilableGeneralFunction.Function.TimesUsed++;
                        }
                        compilableGeneralFunction.OriginalFunction.TimesUsedTotal++;
                        compilableGeneralFunction.Function.TimesUsedTotal++;

                        AddCompilable(compilableGeneralFunction);
                    }
                }

                if (keywordCall.FunctionName == "clone")
                {
                    if (keywordCall.Parameters.Length != 1)
                    { return; }

                    CompiledType paramType = FindStatementType(keywordCall.Parameters[0]);

                    if (!paramType.IsClass)
                    { return; }

                    if (!GetGeneralFunction(paramType.Class, BuiltinFunctionNames.Cloner, out CompiledGeneralFunction? cloner))
                    { return; }

                    if (!cloner.CanUse(CurrentFile))
                    { return; }

                    cloner.AddReference(keywordCall, CurrentFile);

                    if (CurrentFunction == null || !cloner.IsSame(CurrentFunction))
                    { cloner.TimesUsed++; }
                    cloner.TimesUsedTotal++;

                    return;
                }
            }
            else if (statement is Field field)
            { AnalyzeStatement(field.PrevStatement); }
            else if (statement is Identifier variable)
            {
                if (GetFunction(variable.Token, expectedType, out CompiledFunction? function))
                {
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

                if (!GetClass(constructorCall, out CompiledClass? @class))
                { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

                if (GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), BuiltinFunctionNames.Constructor, out CompiledGeneralFunction? constructor))
                {
                    constructor.AddReference(constructorCall, CurrentFile);

                    if (CurrentFunction == null || !constructor.IsSame(CurrentFunction))
                    { constructor.TimesUsed++; }
                    constructor.TimesUsedTotal++;
                }
                else if (GetConstructorTemplate(@class, constructorCall, out CompliableTemplate<CompiledGeneralFunction> compilableGeneralFunction))
                {
                    compilableGeneralFunction.OriginalFunction.AddReference(constructorCall, CurrentFile);

                    if (CurrentFunction == null || !compilableGeneralFunction.OriginalFunction.IsSame(CurrentFunction))
                    {
                        compilableGeneralFunction.OriginalFunction.TimesUsed++;
                        compilableGeneralFunction.Function.TimesUsed++;
                    }
                    compilableGeneralFunction.OriginalFunction.TimesUsedTotal++;
                    compilableGeneralFunction.Function.TimesUsedTotal++;

                    AddCompilable(compilableGeneralFunction);
                }
            }
            else if (statement is LanguageCore.Parser.Statement.Literal)
            { }
            else if (statement is TypeCast @as)
            { AnalyzeStatement(@as.PrevStatement); }
            else if (statement is AddressGetter memoryAddressGetter)
            { AnalyzeStatement(memoryAddressGetter.PrevStatement); }
            else if (statement is Pointer memoryAddressFinder)
            { AnalyzeStatement(memoryAddressFinder.PrevStatement); }
            else if (statement is LiteralList listValue)
            { AnalyzeStatements(listValue.Values); }
            else if (statement is Block block)
            { AnalyzeStatements(block.Statements); }
            else if (statement is ModifiedStatement modifiedStatement)
            {
                Warnings.Add(new Warning($"Modifiers not supported", modifiedStatement.Modifier, CurrentFile));
                AnalyzeStatement(modifiedStatement.Statement);
            }
            else if (statement is AnyCall anyCall)
            {
                if (anyCall.ToFunctionCall(out FunctionCall? functionCall1))
                {
                    AnalyzeStatement(functionCall1);
                }
                else
                {
                    AnalyzeStatement(anyCall.PrevStatement);
                    for (int j = 0; j < anyCall.Parameters.Length; j++)
                    { AnalyzeStatement(anyCall.Parameters[j]); }
                }
            }
            else
            { throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile); }
        }

        void SearchForReferences(Statement[] topLevelStatements)
        {
            CurrentFunction = null;

            {
                int variablesAdded = AnalyzeNewVariables(topLevelStatements);

                foreach (Statement statement in topLevelStatements)
                {
                    AnalyzeStatement(statement);

                    if (statement is StatementWithBlock blockStatement)
                    { AnalyzeStatements(blockStatement.Block.Statements); }

                    if (statement is StatementWithAnyBlock anyBlockStatement &&
                        anyBlockStatement.Block is Block block)
                    { AnalyzeStatements(block.Statements); }
                }

                CompiledGlobalVariables.AddRange(CompiledVariables.GetRange(CompiledVariables.Count - variablesAdded, variablesAdded));
                for (int i = 0; i < variablesAdded; i++)
                { this.CompiledVariables.RemoveAt(this.CompiledVariables.Count - 1); }
            }

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.IsTemplate)
                { continue; }

                CompiledParameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { CompiledParameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.FilePath;
                CurrentFunction = function;

                AnalyzeStatements(function.Block?.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                CompiledParameters.Clear();
            }

            for (int i = 0; i < this.CompiledOperators.Length; i++)
            {
                CompiledOperator function = this.CompiledOperators[i];

                if (function.IsTemplate)
                { continue; }

                CompiledParameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { CompiledParameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.FilePath;
                CurrentFunction = function;

                AnalyzeStatements(function.Block?.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                CompiledParameters.Clear();
            }

            for (int i = 0; i < this.CompiledGeneralFunctions.Length; i++)
            {
                CompiledGeneralFunction function = this.CompiledGeneralFunctions[i];

                if (function.IsTemplate)
                { continue; }

                CompiledParameters.Clear();
                foreach (ParameterDefinition parameter in function.Parameters)
                { CompiledParameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.FilePath;
                CurrentFunction = function;

                AnalyzeStatements(function.Block?.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                CompiledParameters.Clear();
            }

            for (int i = 0; i < this.CompilableFunctions.Count; i++)
            {
                CompliableTemplate<CompiledFunction> function = this.CompilableFunctions[i];

                SetTypeArguments(function.TypeArguments);

                CompiledParameters.Clear();
                for (int j = 0; j < function.Function.Parameters.Count; j++)
                {
                    CompiledParameters.Add(new CompiledParameter(function.Function.ParameterTypes[j], function.Function.Parameters[j]));
                }
                CurrentFile = function.Function.FilePath;
                CurrentFunction = function.Function;

                AnalyzeStatements(function.Function.Block?.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                CompiledParameters.Clear();
                TypeArguments.Clear();
            }

            for (int i = 0; i < this.CompilableOperators.Count; i++)
            {
                CompliableTemplate<CompiledOperator> function = this.CompilableOperators[i];

                SetTypeArguments(function.TypeArguments);

                CompiledParameters.Clear();
                foreach (ParameterDefinition parameter in function.Function.Parameters)
                { CompiledParameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.Function.FilePath;
                CurrentFunction = function.Function;

                AnalyzeStatements(function.Function.Block?.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                CompiledParameters.Clear();
                TypeArguments.Clear();
            }

            for (int i = 0; i < this.CompilableGeneralFunctions.Count; i++)
            {
                CompliableTemplate<CompiledGeneralFunction> function = this.CompilableGeneralFunctions[i];

                SetTypeArguments(function.TypeArguments);

                CompiledParameters.Clear();
                foreach (ParameterDefinition parameter in function.Function.Parameters)
                { CompiledParameters.Add(new CompiledParameter(new CompiledType(parameter.Type, FindType), parameter)); }
                CurrentFile = function.Function.FilePath;
                CurrentFunction = function.Function;

                AnalyzeStatements(function.Function.Block?.Statements);

                CurrentFunction = null;
                CurrentFile = null;
                CompiledParameters.Clear();
                TypeArguments.Clear();
            }
        }

        void ClearReferences()
        {
            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                this.CompiledFunctions[i].ClearReferences();
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
                this.CompiledOperators[i].ClearReferences();
                this.CompiledOperators[i].TimesUsed = 0;
                this.CompiledOperators[i].TimesUsedTotal = 0;
            }
        }

        void AnalyzeFunctions(Statement[] topLevelStatements, PrintCallback? printCallback = null)
        {
            printCallback?.Invoke($"  Collect references ...", LogType.Debug);

            ClearReferences();

            SearchForReferences(topLevelStatements);
        }

        public static void CollectReferences(
            CompilerResult compilerResult,
            PrintCallback? printCallback = null)
            => new ReferenceCollector()
            {
                CompiledClasses = compilerResult.Classes,
                CompiledStructs = compilerResult.Structs,

                CompiledFunctions = compilerResult.Functions,
                CompiledMacros = compilerResult.Macros,
                CompiledOperators = compilerResult.Operators,
                CompiledGeneralFunctions = compilerResult.GeneralFunctions,

                CompiledEnums = compilerResult.Enums,
            }.AnalyzeFunctions(compilerResult.TopLevelStatements, printCallback);

        public static void ClearReferences(CompilerResult compilerResult)
            => new ReferenceCollector()
            {
                CompiledClasses = compilerResult.Classes,
                CompiledStructs = compilerResult.Structs,

                CompiledFunctions = compilerResult.Functions,
                CompiledMacros = compilerResult.Macros,
                CompiledOperators = compilerResult.Operators,
                CompiledGeneralFunctions = compilerResult.GeneralFunctions,

                CompiledEnums = compilerResult.Enums,
            }.ClearReferences();
    }
}
