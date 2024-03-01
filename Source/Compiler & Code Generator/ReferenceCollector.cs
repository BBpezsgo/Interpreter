namespace LanguageCore.Compiler;

using BBCode.Generator;
using Parser;
using Parser.Statement;

public class ReferenceCollector : CodeGeneratorNonGeneratorBase
{
    ISameCheck? CurrentFunction;

    public ReferenceCollector(CompilerResult compilerResult, AnalysisCollection? analysisCollection) : base(compilerResult, GeneratorSettings.Default, analysisCollection)
    {
        CurrentFunction = null;
    }

    CompiledVariable GetVariableInfo(VariableDeclaration newVariable)
    {
        if (LanguageConstants.Keywords.Contains(newVariable.Identifier.Content))
        { throw new CompilerException($"Identifier \"{newVariable.Identifier.Content}\" reserved as a keyword, do not use it as a variable name", newVariable.Identifier, CurrentFile); }

        GeneralType type;

        if (newVariable.Type == "var")
        {
            if (newVariable.InitialValue == null)
            { throw new CompilerException($"Initial value is required for variable declaration \"var\"", newVariable, newVariable.FilePath); }

            GeneralType initialValueType = FindStatementType(newVariable.InitialValue);

            type = initialValueType;
        }
        else
        {
            type = GeneralType.From(newVariable.Type, FindType, TryCompute);
        }

        return new CompiledVariable(-1, type, newVariable);
    }

    void AnalyzeNewVariable(VariableDeclaration newVariable)
    {
        this.CompiledVariables.Add(GetVariableInfo(newVariable));
    }

    int AnalyzeNewVariable(Statement statement)
    {
        int variablesAdded = 0;
        if (statement is VariableDeclaration newVar)
        {
            AnalyzeNewVariable(newVar);
            variablesAdded++;
        }
        else if (statement is ForLoop forLoop)
        {
            AnalyzeNewVariable(forLoop.VariableDeclaration);
            variablesAdded++;
        }
        else if (statement is AnyCall anyCall)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall) &&
                TryGetMacro(functionCall, out MacroDefinition? macro))
            {
                if (!InlineMacro(macro, out Statement? inlined, functionCall.Parameters))
                { throw new CompilerException($"Failed to inline the macro", functionCall, CurrentFile); }

                variablesAdded += AnalyzeNewVariable(inlined);
            }
        }
        return variablesAdded;
    }

    int AnalyzeNewVariables(Statement[] statements)
    {
        if (statements == null) return 0;

        int variablesAdded = 0;
        foreach (Statement statement in statements)
        { variablesAdded += AnalyzeNewVariable(statement); }
        return variablesAdded;
    }

    void AnalyzeStatements(IEnumerable<Statement>? statements)
        => AnalyzeStatements(statements?.ToArray());
    void AnalyzeStatements(Statement[]? statements)
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

    void AnalyzeStatements(IEnumerable<Statement> statements, IEnumerable<GeneralType> expectedTypes)
        => AnalyzeStatements(statements.ToArray(), expectedTypes.ToArray());
    void AnalyzeStatements(Statement[] statements, GeneralType[] expectedTypes)
    {
        int variablesAdded = AnalyzeNewVariables(statements);

        for (int i = 0; i < statements.Length; i++)
        {
            GeneralType? expectedType = null;
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

    void AnalyzeStatement(Statement? statement, GeneralType? expectedType = null)
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
            AnalyzeStatement(index.Index);

            GeneralType prevType = FindStatementType(index.PrevStatement);

            if (GetIndexGetter(prevType, out CompiledFunction? indexer))
            {
                indexer.References.Add((index, CurrentFile, CurrentFunction));
            }
            else if (GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
            {
                indexerTemplate = AddCompilable(indexerTemplate);

                indexerTemplate.OriginalFunction.References.Add((index, CurrentFile, CurrentFunction));
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
                operatorDefinition.References.Add((@operator, CurrentFile, CurrentFunction));
            }
            else if (GetOperatorTemplate(@operator, out CompliableTemplate<CompiledOperator> compilableOperator))
            {
                compilableOperator.Function.References.Add((@operator, CurrentFile, CurrentFunction));

                AddCompilable(compilableOperator);
            }
        }
        else if (statement is CompoundAssignment compoundAssignment)
        {
            if (compoundAssignment.Left is IndexCall indexSetter)
            {
                AnalyzeStatement(indexSetter.PrevStatement);
                AnalyzeStatement(indexSetter.Index);

                GeneralType prevType = FindStatementType(indexSetter.PrevStatement);
                GeneralType valueType = FindStatementType(compoundAssignment.Right);

                if (GetIndexSetter(prevType, valueType, out CompiledFunction? indexer))
                {
                    indexer.References.Add((indexSetter, CurrentFile, CurrentFunction));
                }
                else if (GetIndexSetterTemplate(prevType, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                {
                    indexerTemplate = AddCompilable(indexerTemplate);

                    indexerTemplate.OriginalFunction.References.Add((indexSetter, CurrentFile, CurrentFunction));
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
                AnalyzeStatement(indexSetter.Index);

                GeneralType prevType = FindStatementType(indexSetter.PrevStatement);
                GeneralType valueType = FindStatementType(setter.Right);

                if (GetIndexSetter(prevType, valueType, out CompiledFunction? indexer))
                {
                    indexer.References.Add((indexSetter, CurrentFile, CurrentFunction));
                }
                else if (GetIndexSetterTemplate(prevType, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                {
                    indexerTemplate = AddCompilable(indexerTemplate);

                    indexerTemplate.OriginalFunction.References.Add((indexSetter, CurrentFile, CurrentFunction));
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

            if (functionCall.Identifier.Content == "sizeof")
            { return; }

            if (GetParameter(functionCall.Identifier.Content, out _))
            { return; }

            if (GetVariable(functionCall.Identifier.Content, out _))
            { return; }

            if (TryGetMacro(functionCall, out MacroDefinition? macro))
            {
                if (!InlineMacro(macro, out Statement? inlinedMacro, functionCall.Parameters))
                { throw new CompilerException($"Failed to inline the macro", functionCall, CurrentFile); }

                AnalyzeStatement(inlinedMacro, expectedType);

                if (inlinedMacro is StatementWithBlock blockStatement)
                { AnalyzeStatements(blockStatement.Block.Statements); }

                if (inlinedMacro is StatementWithAnyBlock anyBlockStatement &&
                    anyBlockStatement.Block is Block block1)
                { AnalyzeStatements(block1.Statements); }

                if (inlinedMacro is Block block2)
                { AnalyzeStatements(block2.Statements); }

                return;
            }

            if (GetFunction(functionCall, out CompiledFunction? function))
            {
                function.References.Add((functionCall, CurrentFile, CurrentFunction));
            }
            else if (GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
            {
                compilableFunction.OriginalFunction.References.Add((functionCall, CurrentFile, CurrentFunction));

                AddCompilable(compilableFunction);
            }
        }
        else if (statement is KeywordCall keywordCall)
        {
            AnalyzeStatements(keywordCall.Parameters);

            if (keywordCall.Identifier.Content == "return")
            { return; }

            if (keywordCall.Identifier.Content == "throw")
            { return; }

            if (keywordCall.Identifier.Content == "break")
            { return; }

            if (keywordCall.Identifier.Content == "sizeof")
            { return; }

            if (keywordCall.Identifier.Content == "delete")
            {
                GeneralType paramType = FindStatementType(keywordCall.Parameters[0]);

                if (GetGeneralFunction(paramType, FindStatementTypes(keywordCall.Parameters).ToArray(), BuiltinFunctionNames.Destructor, out CompiledGeneralFunction? destructor))
                {
                    destructor.References.Add((keywordCall, CurrentFile, CurrentFunction));

                    return;
                }
                else if (GetGeneralFunctionTemplate(paramType, FindStatementTypes(keywordCall.Parameters).ToArray(), BuiltinFunctionNames.Destructor, out CompliableTemplate<CompiledGeneralFunction> compilableGeneralFunction))
                {
                    compilableGeneralFunction.OriginalFunction.References.Add((keywordCall, CurrentFile, CurrentFunction));

                    AddCompilable(compilableGeneralFunction);
                }
            }
        }
        else if (statement is Field field)
        { AnalyzeStatement(field.PrevStatement); }
        else if (statement is Identifier variable)
        {
            if (GetFunction(variable.Token, expectedType, out CompiledFunction? function))
            {
                function.References.Add((variable, CurrentFile, CurrentFunction));
            }
        }
        else if (statement is NewInstance)
        { }
        else if (statement is ConstructorCall constructorCall)
        {
            GeneralType type = GeneralType.From(constructorCall.Type, FindType, TryCompute);
            AnalyzeStatements(constructorCall.Parameters);

            GeneralType[] parameters = FindStatementTypes(constructorCall.Parameters);

            if (GetConstructor(type, parameters, out CompiledConstructor? constructor))
            {
                constructor.References.Add((constructorCall, CurrentFile, CurrentFunction));
            }
            else if (GetConstructorTemplate(type, parameters, out CompliableTemplate<CompiledConstructor> compilableGeneralFunction))
            {
                compilableGeneralFunction.OriginalFunction.References.Add((constructorCall, CurrentFile, CurrentFunction));

                AddCompilable(compilableGeneralFunction);
            }
        }
        else if (statement is Literal)
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
            AnalysisCollection?.Warnings.Add(new Warning($"Modifiers not supported", modifiedStatement.Modifier, CurrentFile));
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
        else if (statement is TypeStatement)
        { }
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

        foreach (CompiledFunction function in CompiledFunctions)
        {
            if (function.IsTemplate)
            { continue; }

            CompiledParameters.Clear();
            foreach (ParameterDefinition parameter in function.Parameters)
            { CompiledParameters.Add(new CompiledParameter(GeneralType.From(parameter.Type, FindType), parameter)); }
            CurrentFile = function.FilePath;
            CurrentFunction = function;

            AnalyzeStatements(function.Block?.Statements);

            CurrentFunction = null;
            CurrentFile = null;
            CompiledParameters.Clear();
        }

        foreach (CompiledOperator function in CompiledOperators)
        {
            if (function.IsTemplate)
            { continue; }

            CompiledParameters.Clear();
            foreach (ParameterDefinition parameter in function.Parameters)
            { CompiledParameters.Add(new CompiledParameter(GeneralType.From(parameter.Type, FindType), parameter)); }
            CurrentFile = function.FilePath;
            CurrentFunction = function;

            AnalyzeStatements(function.Block?.Statements);

            CurrentFunction = null;
            CurrentFile = null;
            CompiledParameters.Clear();
        }

        foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
        {
            if (function.IsTemplate)
            { continue; }

            CompiledParameters.Clear();
            foreach (ParameterDefinition parameter in function.Parameters)
            { CompiledParameters.Add(new CompiledParameter(GeneralType.From(parameter.Type, FindType), parameter)); }
            CurrentFile = function.FilePath;
            CurrentFunction = function;

            AnalyzeStatements(function.Block?.Statements);

            CurrentFunction = null;
            CurrentFile = null;
            CompiledParameters.Clear();
        }

        foreach (CompiledConstructor function in CompiledConstructors)
        {
            if (function.IsTemplate)
            { continue; }

            CompiledParameters.Clear();
            foreach (ParameterDefinition parameter in function.Parameters)
            { CompiledParameters.Add(new CompiledParameter(GeneralType.From(parameter.Type, FindType), parameter)); }
            CurrentFile = function.FilePath;
            CurrentFunction = function;

            AnalyzeStatements(function.Block?.Statements);

            CurrentFunction = null;
            CurrentFile = null;
            CompiledParameters.Clear();
        }

        for (int i = 0; i < CompilableFunctions.Count; i++)
        {
            CompliableTemplate<CompiledFunction> function = CompilableFunctions[i];
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

        for (int i = 0; i < CompilableOperators.Count; i++)
        {
            CompliableTemplate<CompiledOperator> function = CompilableOperators[i];
            SetTypeArguments(function.TypeArguments);

            CompiledParameters.Clear();
            foreach (ParameterDefinition parameter in function.Function.Parameters)
            { CompiledParameters.Add(new CompiledParameter(GeneralType.From(parameter.Type, FindType), parameter)); }
            CurrentFile = function.Function.FilePath;
            CurrentFunction = function.Function;

            AnalyzeStatements(function.Function.Block?.Statements);

            CurrentFunction = null;
            CurrentFile = null;
            CompiledParameters.Clear();
            TypeArguments.Clear();
        }

        for (int i = 0; i < CompilableGeneralFunctions.Count; i++)
        {
            CompliableTemplate<CompiledGeneralFunction> function = CompilableGeneralFunctions[i];
            SetTypeArguments(function.TypeArguments);

            CompiledParameters.Clear();
            foreach (ParameterDefinition parameter in function.Function.Parameters)
            { CompiledParameters.Add(new CompiledParameter(GeneralType.From(parameter.Type, FindType), parameter)); }
            CurrentFile = function.Function.FilePath;
            CurrentFunction = function.Function;

            AnalyzeStatements(function.Function.Block?.Statements);

            CurrentFunction = null;
            CurrentFile = null;
            CompiledParameters.Clear();
            TypeArguments.Clear();
        }

        for (int i = 0; i < CompilableConstructors.Count; i++)
        {
            CompliableTemplate<CompiledConstructor> function = CompilableConstructors[i];
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
    }

    void ClearReferences()
    {
        foreach (CompiledFunction function in CompiledFunctions)
        { function.References.Clear(); }

        foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
        { function.References.Clear(); }

        foreach (CompiledOperator function in CompiledOperators)
        { function.References.Clear(); }

        foreach (CompiledConstructor function in CompiledConstructors)
        { function.References.Clear(); }
    }

    void AnalyzeFunctions(Statement[] topLevelStatements, PrintCallback? printCallback = null)
    {
        printCallback?.Invoke($"  Collect references ...", LogType.Debug);

        ClearReferences();

        SearchForReferences(topLevelStatements);
    }

    public static void CollectReferences(in CompilerResult compilerResult, PrintCallback? printCallback = null, AnalysisCollection? analysisCollection = null)
        => new ReferenceCollector(compilerResult, analysisCollection).AnalyzeFunctions(compilerResult.TopLevelStatements, printCallback);

    public static void ClearReferences(in CompilerResult compilerResult, AnalysisCollection? analysisCollection = null)
        => new ReferenceCollector(compilerResult, analysisCollection).ClearReferences();
}
