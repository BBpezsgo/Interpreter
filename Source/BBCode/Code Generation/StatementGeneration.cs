namespace LanguageCore.BBLang.Generator;

using Compiler;
using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;
using LiteralStatement = Parser.Statement.Literal;
using ParameterCleanupItem = (int Size, bool CanDeallocate, Compiler.GeneralType Type, Position Position);

public partial class CodeGeneratorForMain : CodeGenerator
{
    #region AddInstruction()

    void AddInstruction(PreparationInstruction instruction) => GeneratedCode.Add(instruction);
    void AddInstruction(Opcode opcode) => AddInstruction(new PreparationInstruction(opcode));
    void AddInstruction(Opcode opcode, DataItem param0) => AddInstruction(new PreparationInstruction(opcode, param0));
    void AddInstruction(Opcode opcode, int param0) => AddInstruction(new PreparationInstruction(opcode, new DataItem(param0)));
    void AddInstruction(Opcode opcode, AddressingMode addressingMode) => AddInstruction(new PreparationInstruction(opcode, addressingMode));
    void AddInstruction(Opcode opcode, AddressingMode addressingMode, int param0) => AddInstruction(new PreparationInstruction(opcode, addressingMode, new DataItem(param0)));

    void AddComment(string comment)
    {
        if (DebugInfo is null) return;
        if (DebugInfo.CodeComments.TryGetValue(GeneratedCode.Count, out List<string>? comments))
        { comments.Add(comment); }
        else
        { DebugInfo.CodeComments.Add(GeneratedCode.Count, new List<string>() { comment }); }
    }

    #endregion

    void GenerateAllocator(StatementWithValue size)
    {
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create(size);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, FindStatementTypes(parameters), CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? error, AddCompilable))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found: {error}", size, CurrentFile); }
        CompiledFunction? allocator = result.Function;

        if (!allocator.ReturnSomething)
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", size, CurrentFile); }

        allocator.References.Add(new Reference<StatementWithValue>(size, CurrentFile, CurrentContext));

        if (!allocator.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", size, CurrentFile));
            return;
        }

        if (TryEvaluate(allocator, parameters, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            AddInstruction(Opcode.Push, returnValue.Value);
            return;
        }

        if (allocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
            {
                AnalysisCollection?.Errors.Add(exception.InstantiateError(size, CurrentFile));
                AddComment("}");
                return;
            }

            GenerateCodeForFunctionCall_External(parameters, true, allocator, externalFunction);
            return;
        }

        AddComment($"Call {allocator.ToReadable()} {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        if (allocator.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            GenerateInitialValue(allocator.Type);
            AddComment($"}}");
        }

        parameterCleanup = GenerateCodeForParameterPassing(parameters, allocator);

        AddComment(" .:");

        int jumpInstruction = Call(allocator.InstructionOffset);

        if (allocator.InstructionOffset == -1)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, size, allocator, CurrentFile)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");

        // GenerateCodeForStatement(size, new BuiltinType(BasicType.Integer));
        // AddInstruction(Opcode.Allocate);
    }

    void GenerateDeallocator(StatementWithValue value)
    {
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create(value);
        ImmutableArray<GeneralType> parameterTypes = FindStatementTypes(parameters);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameterTypes, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out _, AddCompilable))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found", value, CurrentFile); }
        CompiledFunction? deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue>(value, CurrentFile, CurrentContext));

        if (!deallocator.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", value, CurrentFile));
            return;
        }

        if (TryEvaluate(deallocator, parameters, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            AddInstruction(Opcode.Push, returnValue.Value);
            return;
        }

        if (deallocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(deallocator.ExternalFunctionName, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
            {
                AnalysisCollection?.Errors.Add(exception.InstantiateError(value, CurrentFile));
                AddComment("}");
                return;
            }

            GenerateCodeForFunctionCall_External(parameters, true, deallocator, externalFunction);
            return;
        }

        AddComment($"Call {deallocator.ToReadable()} {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        int returnValueSize = 0;
        if (deallocator.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            returnValueSize = GenerateInitialValue(deallocator.Type);
            AddComment($"}}");
        }

        parameterCleanup = GenerateCodeForParameterPassing(parameters, deallocator);

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset);

        if (deallocator.InstructionOffset == -1)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, value, deallocator, CurrentFile)); }

        if (deallocator.ReturnSomething)
        {
            AddComment($" Clear return value:");
            for (int i = 0; i < returnValueSize; i++)
            { AddInstruction(Opcode.Pop); }
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");

        // GenerateCodeForStatement(value);
        // GenerateDeallocator();
    }

    void GenerateDeallocator(GeneralType deallocateableType, Position position)
    {
        ImmutableArray<GeneralType> parameterTypes = ImmutableArray.Create(deallocateableType);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameterTypes, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? notFoundError, AddCompilable))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found ({notFoundError.Message})", position, CurrentFile); }
        CompiledFunction? deallocator = result.Function;

        if (!deallocator.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", position, CurrentFile));
            return;
        }

        if (deallocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(deallocator.ExternalFunctionName, out _, out WillBeCompilerException? exception))
            {
                AnalysisCollection?.Errors.Add(exception.InstantiateError(position, CurrentFile));
                AddComment("}");
                return;
            }

            throw new NotImplementedException();
        }

        AddComment($"Call {deallocator.ToReadable()} {{");

        if (deallocator.ReturnSomething)
        { throw new NotImplementedException(); }

        Stack<ParameterCleanupItem> parameterCleanup = new() { new(deallocateableType.Size, false, deallocateableType, position) };

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset);

        if (deallocator.InstructionOffset == -1)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, position, deallocator, CurrentFile)); }

        if (deallocator.ReturnSomething)
        {
            AddComment($" Clear return value:");

            const int returnValueSize = 0;
            for (int i = 0; i < returnValueSize; i++)
            { AddInstruction(Opcode.Pop); }
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateDestructor(GeneralType deallocateableType, Position position)
    {
        if (deallocateableType is not PointerType deallocateablePointerType)
        {
            AnalysisCollection?.Warnings.Add(new Warning($"Deallocation only working on pointers or pointer so I skip this", position, CurrentFile));
            return;
        }
        ImmutableArray<GeneralType> argumentTypes = ImmutableArray.Create<GeneralType>(deallocateablePointerType);

        if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, CurrentFile, out FunctionQueryResult<CompiledGeneralFunction>? result, out WillBeCompilerException? error, AddCompilable))
        {
            AddComment($"Pointer value should be already there");
            GenerateDeallocator(deallocateableType, position);
            AddComment("}");

            if (deallocateablePointerType.To is not BuiltinType)
            {
                AnalysisCollection?.Warnings.Add(new Warning($"Destructor for type \"{deallocateablePointerType}\" not found", position, CurrentFile));
                AnalysisCollection?.Warnings.Add(error.InstantiateWarning(position, CurrentFile));
            }

            return;
        }

        CompiledGeneralFunction? destructor = result.Function;

        if (!destructor.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Destructor for type \"{deallocateablePointerType}\" function cannot be called due to its protection level", position, CurrentFile));
            AddComment("}");
            return;
        }

        AddComment(" Param0 should be already there");

        AddComment(" .:");

        int jumpInstruction = Call(destructor.InstructionOffset);

        if (destructor.InstructionOffset == -1)
        { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, false, position, destructor, CurrentFile)); }

        AddComment(" Clear Param0:");

        GenerateDeallocator(deallocateableType, position);

        AddComment("}");
    }

    #region GenerateCodeForStatement

    void GenerateCodeForStatement(VariableDeclaration newVariable)
    {
        if (newVariable.Modifiers.Contains(ModifierKeywords.Const)) return;

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        if (GetConstant(newVariable.Identifier.Content, out _))
        { throw new CompilerException($"Symbol name \"{newVariable.Identifier}\" conflicts with an another symbol name", newVariable.Identifier, newVariable.File); }

        if (!GetVariable(newVariable.Identifier.Content, out CompiledVariable? compiledVariable))
        { throw new InternalException($"Variable \"{newVariable.Identifier.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", newVariable.Identifier, newVariable.File); }

        if (compiledVariable.IsInitialized) return;

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        if (newVariable.InitialValue == null) return;

        AddComment($"New Variable \"{newVariable.Identifier.Content}\" {{");

        if (GetConstant(newVariable.Identifier.Content, out _))
        { throw new CompilerException($"Can not set constant value: it is readonly", newVariable, newVariable.File); }

        if (newVariable.InitialValue is LiteralList)
        { throw new NotImplementedException(); }

        GenerateCodeForValueSetter(new Identifier(newVariable.Identifier, newVariable.File), newVariable.InitialValue);
        AddComment("}");
    }
    void GenerateCodeForStatement(KeywordCall keywordCall)
    {
        AddComment($"Call Keyword {keywordCall.Identifier} {{");

        if (keywordCall.Identifier.Content == StatementKeywords.Return)
        {
            if (keywordCall.Parameters.Length > 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

            if (keywordCall.Parameters.Length == 1)
            {
                AddComment(" Param 0:");

                StatementWithValue returnValue = keywordCall.Parameters[0];
                GeneralType returnValueType = FindStatementType(returnValue);

                GenerateCodeForStatement(returnValue);

                if (InFunction)
                {
                    StackStore(GetReturnValueAddress(returnValueType), returnValueType.Size);
                }
                else
                {
                    if (returnValueType is not BuiltinType)
                    { throw new CompilerException($"Exit code must be a built-in type (not {returnValueType})", returnValue, CurrentFile); }

                    StackStore(new ValueAddress(AbsoluteGlobalOffset, AddressingMode.BasePointerRelative, true));
                }
            }

            AddComment(" .:");

            if (CanReturn)
            {
                AddInstruction(Opcode.Push, new DataItem(true));
                StackStore(ReturnFlagAddress);
            }

            ReturnInstructions.Last.Add(GeneratedCode.Count);
            AddInstruction(Opcode.Jump, 0);

            AddComment("}");

            return;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Throw)
        {
            if (keywordCall.Parameters.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Throw}\": required {1} passed {keywordCall.Parameters}", keywordCall, CurrentFile); }

            AddComment(" Param 0:");

            StatementWithValue throwValue = keywordCall.Parameters[0];

            GenerateCodeForStatement(throwValue);
            AddInstruction(Opcode.Throw);

            return;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Break)
        {
            if (BreakInstructions.Count == 0)
            { throw new CompilerException($"The keyword \"{StatementKeywords.Break}\" does not available in the current context", keywordCall.Identifier, CurrentFile); }

            BreakInstructions.Last.Add(GeneratedCode.Count);
            AddInstruction(Opcode.Jump, 0);

            AddComment("}");

            return;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Delete)
        {
            if (keywordCall.Parameters.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Delete}\": required {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

            GenerateCodeForStatement(keywordCall.Parameters[0]);

            GeneralType deletableType = FindStatementType(keywordCall.Parameters[0]);
            GenerateDestructor(deletableType, keywordCall.Parameters[0].Position);

            // if (deletableType is not PointerType deletablePointerType)
            // {
            //     AnalysisCollection?.Warnings.Add(new Warning($"The \"{StatementKeywords.Delete}\" keyword-function is only working on pointers or pointer so I skip this", keywordCall.Parameters[0], CurrentFile));
            //     return;
            // }
            // 
            // if (!GetGeneralFunction(deletablePointerType, FindStatementTypes(keywordCall.Parameters).ToArray(), BuiltinFunctionNames.Destructor, out CompiledGeneralFunction? destructor))
            // {
            //     if (!GetGeneralFunctionTemplate(deletablePointerType, FindStatementTypes(keywordCall.Parameters).ToArray(), BuiltinFunctionNames.Destructor, out CompliableTemplate<CompiledGeneralFunction> destructorTemplate))
            //     {
            //         GenerateDeallocator(keywordCall.Parameters[0]);
            //         AddComment("}");
            // 
            //         if (deletablePointerType.To is not BuiltinType)
            //         { AnalysisCollection?.Warnings.Add(new Warning($"Destructor for type \"{deletablePointerType}\" not found", keywordCall.Identifier, CurrentFile)); }
            // 
            //         return;
            //     }
            //     destructorTemplate = AddCompilable(destructorTemplate);
            //     destructor = destructorTemplate.Function;
            // }
            // 
            // if (!destructor.CanUse(CurrentFile))
            // {
            //     AnalysisCollection?.Errors.Add(new Error($"Destructor for type \"{deletablePointerType}\" function cannot be called due to its protection level", keywordCall.Identifier, CurrentFile));
            //     AddComment("}");
            //     return;
            // }
            // 
            // AddComment(" Param0:");
            // GenerateCodeForStatement(keywordCall.Parameters[0], deletablePointerType);
            // 
            // AddComment(" .:");
            // 
            // int jumpInstruction = Call(destructor.InstructionOffset);
            // 
            // if (destructor.InstructionOffset == -1)
            // { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, false, keywordCall, destructor, CurrentFile)); }
            // 
            // AddComment(" Clear Param0:");
            // 
            // GenerateDeallocator();
            // 
            // AddComment("}");

            return;
        }

        throw new CompilerException($"Unknown keyword \"{keywordCall.Identifier}\"", keywordCall.Identifier, CurrentFile);
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(IReadOnlyList<StatementWithValue> parameters, ICompiledFunction compiledFunction)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        for (int i = 0; i < parameters.Count; i++)
        {
            StatementWithValue passedParameter = parameters[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);
            ParameterDefinition definedParameter = compiledFunction.Parameters[i];
            GeneralType definedParameterType = compiledFunction.ParameterTypes[i];

            if (!passedParameterType.Equals(definedParameterType))
            {
                passedParameter = new TypeCast(
                    passedParameter,
                    Token.CreateAnonymous("as"),
                    definedParameterType.ToTypeInstance());
                passedParameterType = FindStatementType(passedParameter);
            }

            AddComment($" Pass {definedParameter}:");

            bool canDeallocate = definedParameter.Modifiers.Contains(ModifierKeywords.Temp);

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", passedParameter, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(passedParameter, definedParameterType);

            parameterCleanup.Push((passedParameterType.Size, canDeallocate, passedParameterType, passedParameter.Position));
        }

        return parameterCleanup;
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(IReadOnlyList<StatementWithValue> parameters, FunctionType function)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        for (int i = 0; i < parameters.Count; i++)
        {
            StatementWithValue passedParameter = parameters[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);
            GeneralType definedParameterType = function.Parameters[i];

            AddComment($" Param {i}:");

            bool canDeallocate = true; // temp type maybe?

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", passedParameter, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(passedParameter, definedParameterType);

            parameterCleanup.Push((passedParameterType.Size, canDeallocate, passedParameterType, passedParameter.Position));
        }

        return parameterCleanup;
    }

    void GenerateCodeForParameterCleanup(Stack<ParameterCleanupItem> parameterCleanup)
    {
        AddComment(" Clear Params:");
        while (parameterCleanup.Count > 0)
        {
            ParameterCleanupItem passedParameter = parameterCleanup.Pop();

            if (passedParameter.CanDeallocate && passedParameter.Size == 1)
            {
                GenerateDestructor(passedParameter.Type, passedParameter.Position);
                continue;
            }

            for (int i = 0; i < passedParameter.Size; i++)
            { AddInstruction(Opcode.Pop); }
        }
    }

    void GenerateCodeForFunctionCall_External<TFunction>(IReadOnlyList<StatementWithValue> parameters, bool saveValue, TFunction compiledFunction, ExternalFunctionBase externalFunction)
        where TFunction : ICompiledFunction, ISimpleReadable
    {
        AddComment($"Call {compiledFunction.ToReadable()} {{");

        int returnValueSize = 0;
        if (compiledFunction.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            returnValueSize = GenerateInitialValue(compiledFunction.Type);
            AddComment($"}}");
        }

        int returnValueOffset = -1;

        Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForParameterPassing(parameters, compiledFunction);
        for (int i = 0; i < parameterCleanup.Count; i++)
        { returnValueOffset -= parameterCleanup[i].Size; }

        AddComment(" .:");
        AddInstruction(Opcode.Push, externalFunction.Id);
        AddInstruction(Opcode.CallExternal, externalFunction.Parameters.Length);

        if (compiledFunction.ReturnSomething)
        {
            if (saveValue)
            {
                AddComment($" Store return value:");
                StackStore(new ValueAddress(returnValueOffset, AddressingMode.BasePointerRelative), returnValueSize);
            }
            else
            {
                AddComment($" Clear return value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.Pop); }
            }
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
    {
        compiledFunction.References.Add((functionCall, CurrentFile, CurrentContext));
        OnGotStatementType(functionCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(functionCall.OriginalFile ?? CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"The {compiledFunction.ToReadable()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
            return;
        }

        if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
        { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ToReadable()}: required {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

        if (compiledFunction.BuiltinFunctionName == BuiltinFunctions.Allocate)
        {
            GenerateAllocator(functionCall.Parameters[0]);
            return;
        }

        if (compiledFunction.BuiltinFunctionName == BuiltinFunctions.Free)
        {
            GenerateDeallocator(functionCall.Parameters[0]);
            return;
        }

        if (compiledFunction.IsExternal)
        {
            if (!ExternalFunctions.TryGet(compiledFunction.ExternalFunctionName, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
            {
                AnalysisCollection?.Errors.Add(exception.InstantiateError(functionCall.Identifier, CurrentFile));
                AddComment("}");
                return;
            }

            GenerateCodeForFunctionCall_External(functionCall.MethodParameters, functionCall.SaveValue, compiledFunction, externalFunction);
            return;
        }

        AddComment($"Call {compiledFunction.ToReadable()} {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        int returnValueSize = 0;
        if (compiledFunction.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            returnValueSize = GenerateInitialValue(compiledFunction.Type);
            AddComment($"}}");
        }

        parameterCleanup = GenerateCodeForParameterPassing(functionCall.MethodParameters, compiledFunction);

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset);

        if (compiledFunction.InstructionOffset == -1)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, functionCall, compiledFunction, CurrentFile)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            for (int i = 0; i < returnValueSize; i++)
            { AddInstruction(Opcode.Pop); }
        }

        AddComment("}");
    }
    void GenerateCodeForFunctionCall_Runtime(FunctionCall functionCall, StatementWithValue compiledVariable)
    {
        GeneralType statementType = FindStatementType(compiledVariable);
        if (statementType is not FunctionType functionType)
        { throw new CompilerException($"Statement {compiledVariable} is not a function", compiledVariable, CurrentFile); }
        OnGotStatementType(functionCall, functionType.ReturnType);

        if (functionCall.MethodParameters.Length != functionType.Parameters.Length)
        { throw new CompilerException($"Wrong number of parameters passed to function {functionType}: required {functionType.Parameters.Length} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

        AddComment($"Call {functionType} {{");

        functionCall.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        int returnValueSize = 0;
        if (functionType.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            returnValueSize = GenerateInitialValue(functionType.ReturnType);
            AddComment($"}}");
        }

        Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForParameterPassing(functionCall.MethodParameters, functionType);

        AddComment(" .:");

        CallRuntime(compiledVariable);

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (functionType.ReturnSomething && !functionCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            for (int i = 0; i < returnValueSize; i++)
            { AddInstruction(Opcode.Pop); }
        }

        AddComment("}");
    }

    void GenerateCodeForStatement(AnyCall anyCall)
    {
        if (anyCall.PrevStatement is Identifier _identifier &&
            _identifier.Content == "sizeof")
        {
            _identifier.Token.AnalyzedType = TokenAnalyzedType.Keyword;

            if (anyCall.Parameters.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": required {1} passed {anyCall.Parameters.Length}", anyCall, CurrentFile); }

            StatementWithValue param = anyCall.Parameters[0];
            GeneralType paramType;
            if (param is TypeStatement typeStatement)
            { paramType = GeneralType.From(typeStatement.Type, FindType, TryCompute); }
            else
            { paramType = FindStatementType(param); }

            OnGotStatementType(anyCall, new BuiltinType(BasicType.Integer));
            anyCall.PredictedValue = paramType.Size;

            AddInstruction(Opcode.Push, paramType.Size);

            return;
        }

        if (GetFunction(anyCall, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? notFound, AddCompilable) &&
            anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            CompiledFunction compiledFunction = result.Function;

            if (anyCall.PrevStatement is Identifier _identifier2)
            { _identifier2.Token.AnalyzedType = TokenAnalyzedType.FunctionName; }
            anyCall.Reference = compiledFunction;

            ImmutableArray<StatementWithValue>.Builder convertedParameters = ImmutableArray.CreateBuilder<StatementWithValue>(functionCall.MethodParameters.Length);
            for (int i = 0; i < functionCall.MethodParameters.Length; i++)
            {
                convertedParameters.Add(functionCall.MethodParameters[i]);
                GeneralType passed = FindStatementType(functionCall.MethodParameters[i]);
                GeneralType defined = compiledFunction.ParameterTypes[i];
                if (passed.Equals(defined)) continue;
                convertedParameters[i] = new TypeCast(
                    convertedParameters[i],
                    Token.CreateAnonymous("as"),
                    defined.ToTypeInstance());
            }

            if (!Settings.DontOptimize &&
                TryEvaluate(compiledFunction, convertedParameters.ToImmutable(), out DataItem? returnValue, out Statement[]? runtimeStatements) &&
                returnValue.HasValue &&
                runtimeStatements.Length == 0)
            {
                anyCall.PredictedValue = returnValue.Value;
                AddInstruction(Opcode.Push, returnValue.Value);
                return;
            }

            GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);

            if (functionCall.CompiledType is not null)
            { OnGotStatementType(anyCall, functionCall.CompiledType); }

            if (anyCall.PrevStatement is IReferenceableTo _ref1)
            { _ref1.Reference = functionCall.Reference; }
            else
            { anyCall.Reference = functionCall.Reference; }
            return;
        }

        GeneralType prevType = FindStatementType(anyCall.PrevStatement);
        if (prevType is not FunctionType functionType)
        {
            if (notFound is not null)
            { throw notFound.Instantiate(anyCall.PrevStatement, CurrentFile); }

            throw new CompilerException($"This isn't a function", anyCall.PrevStatement, CurrentFile);
        }

        /*
        if (TryInlineMacro(anyCall.PrevStatement, out Statement? inlined))
        {
            if (inlined is Identifier identifier)
            {
                functionCall = new FunctionCall(null, identifier.Token, anyCall.Parameters, anyCall.Brackets)
                {
                    SaveValue = anyCall.SaveValue,
                    Semicolon = anyCall.Semicolon,
                    SurroundingBracelet = anyCall.SurroundingBracelet,
                };
                GenerateCodeForStatement(functionCall);
                return;
            }
        }
        */

        OnGotStatementType(anyCall, functionType.ReturnType);

        if (anyCall.Parameters.Length != functionType.Parameters.Length)
        { throw new CompilerException($"Wrong number of parameters passed to function {functionType}: required {functionType.Parameters.Length} passed {anyCall.Parameters.Length}", new Position(anyCall.Parameters), CurrentFile); }

        AddComment($"Call (runtime) {functionType} {{");

        int returnValueSize = 0;
        if (functionType.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            returnValueSize = GenerateInitialValue(functionType.ReturnType);
            AddComment($"}}");
        }

        Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForParameterPassing(anyCall.Parameters, functionType);

        AddComment(" .:");

        CallRuntime(anyCall.PrevStatement);

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (functionType.ReturnSomething && !anyCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            for (int i = 0; i < returnValueSize; i++)
            { AddInstruction(Opcode.Pop); }
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(BinaryOperatorCall @operator)
    {
        if (!Settings.DontOptimize && TryCompute(@operator, out DataItem predictedValue))
        {
            OnGotStatementType(@operator, new BuiltinType(predictedValue.Type));
            @operator.PredictedValue = predictedValue;

            AddInstruction(Opcode.Push, predictedValue);
            return;
        }

        if (GetOperator(@operator, CurrentFile, out FunctionQueryResult<CompiledOperator>? result, out _))
        {
            CompiledOperator? operatorDefinition = result.Function;

            operatorDefinition.References.Add((@operator, CurrentFile, CurrentContext));
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            if (!operatorDefinition.CanUse(CurrentFile))
            {
                AnalysisCollection?.Errors.Add(new LanguageError($"The {operatorDefinition.ToReadable()} operator cannot be called due to its protection level", @operator.Operator, CurrentFile));
                return;
            }

            if (BinaryOperatorCall.ParameterCount != operatorDefinition.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator {operatorDefinition.ToReadable()}: required {operatorDefinition.ParameterCount} passed {BinaryOperatorCall.ParameterCount}", @operator, CurrentFile); }

            if (operatorDefinition.IsExternal)
            {
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
                {
                    AnalysisCollection?.Errors.Add(exception.InstantiateError(@operator.Operator, CurrentFile));
                    AddComment("}");
                    return;
                }

                GenerateCodeForFunctionCall_External(@operator.Parameters, @operator.SaveValue, operatorDefinition, externalFunction);
                return;
            }

            AddComment($"Call {operatorDefinition.Identifier} {{");

            int returnValueSize = GenerateInitialValue(operatorDefinition.Type);

            Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForParameterPassing(@operator.Parameters, operatorDefinition);

            AddComment(" .:");

            int jumpInstruction = Call(operatorDefinition.InstructionOffset);

            if (operatorDefinition.InstructionOffset == -1)
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition, CurrentFile)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.Pop); }
            }

            AddComment("}");
        }
        else if (LanguageOperators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
        {
            if (LanguageOperators.ParameterCounts[@operator.Operator.Content] != BinaryOperatorCall.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {LanguageOperators.ParameterCounts[@operator.Operator.Content]} passed {BinaryOperatorCall.ParameterCount}", @operator.Operator, CurrentFile); }

            FindStatementType(@operator);

            int jumpInstruction = -1;

            GenerateCodeForStatement(@operator.Left);

            if (opcode == Opcode.LogicAND)
            {
                StackLoad(new ValueAddress(-1, AddressingMode.StackPointerRelative));
                jumpInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.JumpIfZero);
            }
            else if (opcode == Opcode.LogicOR)
            {
                StackLoad(new ValueAddress(-1, AddressingMode.StackPointerRelative));
                AddInstruction(Opcode.LogicNOT);
                jumpInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.JumpIfZero);
            }

            if (@operator.Right != null) GenerateCodeForStatement(@operator.Right);
            AddInstruction(opcode);

            if (jumpInstruction != -1)
            { GeneratedCode[jumpInstruction].Parameter = GeneratedCode.Count - jumpInstruction; }
        }
        else if (@operator.Operator.Content == "=")
        {
            if (BinaryOperatorCall.ParameterCount != 2)
            { throw new CompilerException($"Wrong number of parameters passed to assignment operator \"{@operator.Operator.Content}\": required {2} passed {BinaryOperatorCall.ParameterCount}", @operator.Operator, CurrentFile); }

            GenerateCodeForValueSetter(@operator.Left, @operator.Right);
        }
        else
        { throw new CompilerException($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, CurrentFile); }
    }
    void GenerateCodeForStatement(UnaryOperatorCall @operator)
    {
        if (!Settings.DontOptimize && TryCompute(@operator, out DataItem predictedValue))
        {
            OnGotStatementType(@operator, new BuiltinType(predictedValue.Type));
            @operator.PredictedValue = predictedValue;

            AddInstruction(Opcode.Push, predictedValue);
            return;
        }

        if (GetOperator(@operator, CurrentFile, out FunctionQueryResult<CompiledOperator>? result, out _))
        {
            CompiledOperator? operatorDefinition = result.Function;

            operatorDefinition.References.Add((@operator, CurrentFile, CurrentContext));
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            if (!operatorDefinition.CanUse(CurrentFile))
            {
                AnalysisCollection?.Errors.Add(new LanguageError($"The {operatorDefinition.ToReadable()} operator cannot be called due to its protection level", @operator.Operator, CurrentFile));
                return;
            }

            if (UnaryOperatorCall.ParameterCount != operatorDefinition.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator {operatorDefinition.ToReadable()}: required {operatorDefinition.ParameterCount} passed {UnaryOperatorCall.ParameterCount}", @operator, CurrentFile); }

            if (operatorDefinition.IsExternal)
            {
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
                {
                    AnalysisCollection?.Errors.Add(exception.InstantiateError(@operator.Operator, CurrentFile));
                    AddComment("}");
                    return;
                }

                GenerateCodeForFunctionCall_External(@operator.Parameters, @operator.SaveValue, operatorDefinition, externalFunction);
                return;
            }

            AddComment($"Call {operatorDefinition.Identifier} {{");

            int returnValueSize = GenerateInitialValue(operatorDefinition.Type);

            Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForParameterPassing(@operator.Parameters, operatorDefinition);

            AddComment(" .:");

            int jumpInstruction = Call(operatorDefinition.InstructionOffset);

            if (operatorDefinition.InstructionOffset == -1)
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition, CurrentFile)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.Pop); }
            }

            AddComment("}");
        }
        else if (LanguageOperators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
        {
            if (LanguageOperators.ParameterCounts[@operator.Operator.Content] != UnaryOperatorCall.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator \"{@operator.Operator.Content}\": required {LanguageOperators.ParameterCounts[@operator.Operator.Content]} passed {UnaryOperatorCall.ParameterCount}", @operator.Operator, CurrentFile); }

            GenerateCodeForStatement(@operator.Left);

            AddInstruction(opcode);
        }
        else
        { throw new CompilerException($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, CurrentFile); }
    }
    void GenerateCodeForStatement(Assignment setter)
    {
        GenerateCodeForValueSetter(setter.Left, setter.Right);
    }
    void GenerateCodeForStatement(LiteralStatement literal)
    {
        switch (literal.Type)
        {
            case LiteralType.Integer:
            {
                OnGotStatementType(literal, new BuiltinType(BasicType.Integer));

                AddInstruction(Opcode.Push, new DataItem(literal.GetInt()));
                break;
            }
            case LiteralType.Float:
            {
                OnGotStatementType(literal, new BuiltinType(BasicType.Float));

                AddInstruction(Opcode.Push, new DataItem(literal.GetFloat()));
                break;
            }
            case LiteralType.String:
            {
                GenerateCodeForLiteralString(literal.Value);
                break;
            }
            case LiteralType.Char:
            {
                OnGotStatementType(literal, new BuiltinType(BasicType.Char));

                if (literal.Value.Length != 1) throw new InternalException($"Literal char contains {literal.Value.Length} characters but only 1 allowed", literal, CurrentFile);
                AddInstruction(Opcode.Push, new DataItem(literal.Value[0]));
                break;
            }
            default: throw new UnreachableException();
        }
    }

    void GenerateCodeForLiteralString(string literal)
    {
        AddComment($"Create String \"{literal}\" {{");

        AddComment("Allocate String object {");

        GenerateAllocator(Literal.CreateAnonymous(1 + literal.Length, Position.UnknownPosition));

        AddComment("}");

        AddComment("Set string data {");

        for (int i = 0; i < literal.Length; i++)
        {
            // Prepare value
            AddInstruction(Opcode.Push, new DataItem(literal[i]));

            // Calculate pointer
            StackLoad(new ValueAddress(-2, AddressingMode.StackPointerRelative));
            AddInstruction(Opcode.Push, i);
            AddInstruction(Opcode.MathAdd);

            // Set value
            AddInstruction(Opcode.HeapSet, AddressingMode.Runtime);
        }

        {
            // Prepare value
            AddInstruction(Opcode.Push, new DataItem('\0'));

            // Calculate pointer
            StackLoad(new ValueAddress(-2, AddressingMode.StackPointerRelative));
            AddInstruction(Opcode.Push, literal.Length);
            AddInstruction(Opcode.MathAdd);

            // Set value
            AddInstruction(Opcode.HeapSet, AddressingMode.Runtime);
        }

        AddComment("}");

        AddComment("}");
    }

    void GenerateCodeForStatement(Identifier variable, GeneralType? expectedType = null)
    {
        if (GetConstant(variable.Content, out IConstant? constant))
        {
            OnGotStatementType(variable, constant.Type);
            variable.PredictedValue = constant.Value;
            variable.Reference = constant;
            variable.Token.AnalyzedType = TokenAnalyzedType.ConstantName;

            AddInstruction(Opcode.Push, constant.Value);
            return;
        }

        if (GetParameter(variable.Content, out CompiledParameter? param))
        {
            if (variable.Content != StatementKeywords.This)
            { variable.Token.AnalyzedType = TokenAnalyzedType.ParameterName; }
            variable.Reference = param;
            OnGotStatementType(variable, param.Type);

            ValueAddress address = GetBaseAddress(param);

            StackLoad(address, param.Type.Size);

            return;
        }

        if (GetVariable(variable.Content, out CompiledVariable? val))
        {
            variable.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = val;
            OnGotStatementType(variable, val.Type);

            StackLoad(new ValueAddress(val), val.Type.Size);
            return;
        }

        if (GetGlobalVariable(variable.Content, variable.OriginalFile, out CompiledVariable? globalVariable, out _))
        {
            variable.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = globalVariable;
            OnGotStatementType(variable, globalVariable.Type);

            StackLoad(GetGlobalVariableAddress(globalVariable), globalVariable.Type.Size);
            return;
        }

        if (GetFunction(variable.Token.Content, expectedType, out FunctionQueryResult<CompiledFunction>? result, out _))
        {
            CompiledFunction? compiledFunction = result.Function;

            compiledFunction.References.Add((variable, CurrentFile, CurrentContext));
            variable.Token.AnalyzedType = TokenAnalyzedType.FunctionName;
            variable.Reference = compiledFunction;
            OnGotStatementType(variable, new FunctionType(compiledFunction));

            if (compiledFunction.InstructionOffset == -1)
            { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(GeneratedCode.Count, true, variable, compiledFunction, CurrentFile)); }

            AddInstruction(Opcode.Push, compiledFunction.InstructionOffset);

            return;
        }

        throw new CompilerException($"Symbol \"{variable.Content}\" not found", variable, CurrentFile);
    }
    void GenerateCodeForStatement(AddressGetter addressGetter)
    {
        if (addressGetter.PrevStatement is Identifier identifier)
        {
            GeneralType type = FindStatementType(identifier);

            throw new CompilerException($"Variable \"{identifier}\" (type of {type}) is stored on the stack", addressGetter, CurrentFile);

            // GenerateCodeForStatement(identifier);
            // return;
        }

        if (addressGetter.PrevStatement is Field field)
        {
            // int offset = GetDataOffset(field);
            // ValueAddress pointerOffset = GetBaseAddress(field);

            throw new CompilerException($"Field \"{field}\" is on the stack", addressGetter, CurrentFile);

            // StackLoad(pointerOffset);
            // AddInstruction(Opcode.MATH_ADD, offset);
            // return;
        }

        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(Pointer memoryAddressFinder)
    {
        GenerateCodeForStatement(memoryAddressFinder.PrevStatement);
        AddInstruction(Opcode.HeapGet, AddressingMode.Runtime);
    }
    void GenerateCodeForStatement(WhileLoop whileLoop)
    {
        if (!Settings.DontOptimize &&
            TryCompute(whileLoop.Condition, out DataItem condition))
        {
            if (condition)
            {
                AddComment("while (1) {");

                OnScopeEnter(whileLoop.Block);

                int beginOffset = GeneratedCode.Count;

                BreakInstructions.Push(new List<int>());

                GenerateCodeForStatement(whileLoop.Block, true);

                AddComment("Jump Back");
                AddInstruction(Opcode.Jump, beginOffset - GeneratedCode.Count);

                FinishJumpInstructions(BreakInstructions.Last);

                OnScopeExit(whileLoop.Block.Brackets.End.Position);

                BreakInstructions.Pop();

                AddComment("}");
            }
            else
            {
                AddComment("while (0) { }");
            }
            return;
        }

        AddComment("while (...) {");

        OnScopeEnter(whileLoop.Block);

        AddComment("Condition");
        int conditionOffset = GeneratedCode.Count;
        GenerateCodeForStatement(whileLoop.Condition);

        int conditionJumpOffset = GeneratedCode.Count;
        AddInstruction(Opcode.JumpIfZero, 0);

        BreakInstructions.Push(new List<int>());

        GenerateCodeForStatement(whileLoop.Block, true);

        AddComment("Jump Back");
        AddInstruction(Opcode.Jump, conditionOffset - GeneratedCode.Count);

        FinishJumpInstructions(BreakInstructions.Last);

        GeneratedCode[conditionJumpOffset].Parameter = GeneratedCode.Count - conditionJumpOffset;

        OnScopeExit(whileLoop.Block.Brackets.End.Position);

        AddComment("}");

        BreakInstructions.Pop();
    }
    void GenerateCodeForStatement(ForLoop forLoop)
    {
        AddComment("for (...) {");

        OnScopeEnter(forLoop.Position, Enumerable.Repeat(forLoop.VariableDeclaration, 1));

        AddComment("For-loop variable");
        GenerateCodeForStatement(forLoop.VariableDeclaration);

        if (!Settings.DontOptimize &&
            TryCompute(forLoop.Condition, out DataItem condition))
        {
            if (condition)
            {
                AddComment("For-loop condition");
                int beginOffset = GeneratedCode.Count;

                BreakInstructions.Push(new List<int>());

                GenerateCodeForStatement(forLoop.Block);

                AddComment("For-loop expression");
                GenerateCodeForStatement(forLoop.Expression);

                AddComment("Jump back");
                AddInstruction(Opcode.Jump, beginOffset - GeneratedCode.Count);

                FinishJumpInstructions(BreakInstructions.Pop());

                OnScopeExit(forLoop.Position.After());

                AddComment("}");
            }
            else
            {
                OnScopeExit(forLoop.Position.After());

                AddComment("}");
            }
            return;
        }

        AddComment("For-loop condition");
        int conditionOffsetFor = GeneratedCode.Count;
        GenerateCodeForStatement(forLoop.Condition);
        AddInstruction(Opcode.JumpIfZero, 0);
        int conditionJumpOffsetFor = GeneratedCode.Count - 1;

        BreakInstructions.Push(new List<int>());

        GenerateCodeForStatement(forLoop.Block);

        AddComment("For-loop expression");
        GenerateCodeForStatement(forLoop.Expression);

        AddComment("Jump back");
        AddInstruction(Opcode.Jump, conditionOffsetFor - GeneratedCode.Count);
        GeneratedCode[conditionJumpOffsetFor].Parameter = GeneratedCode.Count - conditionJumpOffsetFor;

        FinishJumpInstructions(BreakInstructions.Pop());

        OnScopeExit(forLoop.Position.After());

        AddComment("}");
    }
    void GenerateCodeForStatement(IfContainer @if)
    {
        List<int> jumpOutInstructions = new();

        foreach (BaseBranch ifSegment in @if.Parts)
        {
            if (ifSegment is IfBranch partIf)
            {
                if (!Settings.DontOptimize &&
                    TryCompute(partIf.Condition, out DataItem condition))
                {
                    if (!condition)
                    {
                        AddComment("if (0) { }");
                        continue;
                    }
                    else
                    {
                        AddComment("if (1) {");
                        GenerateCodeForStatement(partIf.Block);
                        AddComment("}");
                        break;
                    }
                }

                AddComment("if (...) {");

                AddComment("If condition");
                GenerateCodeForStatement(partIf.Condition);
                AddComment("If jump-to-next");
                int jumpNextInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.JumpIfZero, 0);

                GenerateCodeForStatement(partIf.Block);

                AddComment("If jump-to-end");
                jumpOutInstructions.Add(GeneratedCode.Count);
                AddInstruction(Opcode.Jump, 0);

                AddComment("}");

                GeneratedCode[jumpNextInstruction].Parameter = GeneratedCode.Count - jumpNextInstruction;
            }
            else if (ifSegment is ElseIfBranch partElseif)
            {
                if (!Settings.DontOptimize &&
                    TryCompute(partElseif.Condition, out DataItem condition))
                {
                    if (!condition)
                    {
                        AddComment("elseif (0) { }");
                        continue;
                    }
                    else
                    {
                        AddComment("elseif (1) {");
                        GenerateCodeForStatement(partElseif.Block);
                        AddComment("}");
                        break;
                    }
                }

                AddComment("elseif (...) {");

                AddComment("Elseif condition");
                GenerateCodeForStatement(partElseif.Condition);
                AddComment("Elseif jump-to-next");
                int jumpNextInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.JumpIfZero, 0);

                GenerateCodeForStatement(partElseif.Block);

                AddComment("Elseif jump-to-end");
                jumpOutInstructions.Add(GeneratedCode.Count);
                AddInstruction(Opcode.Jump, 0);

                AddComment("}");

                GeneratedCode[jumpNextInstruction].Parameter = GeneratedCode.Count - jumpNextInstruction;
            }
            else if (ifSegment is ElseBranch partElse)
            {
                AddComment("else {");

                GenerateCodeForStatement(partElse.Block);

                AddComment("}");
            }
        }

        foreach (int item in jumpOutInstructions)
        {
            GeneratedCode[item].Parameter = GeneratedCode.Count - item;
        }
    }
    void GenerateCodeForStatement(NewInstance newObject)
    {
        AddComment($"new {newObject.Type} {{");

        GeneralType instanceType = FindType(newObject.Type);

        newObject.Type.SetAnalyzedType(instanceType);
        OnGotStatementType(newObject, instanceType);

        switch (instanceType)
        {
            case PointerType pointerType:
            {
                GenerateAllocator(Literal.CreateAnonymous(pointerType.To.Size, newObject.Type));

                for (int offset = 0; offset < pointerType.To.Size; offset++)
                {
                    AddInstruction(Opcode.Push, 0);
                    StackLoad(new ValueAddress(-2, AddressingMode.StackPointerRelative));
                    AddInstruction(Opcode.Push, offset);
                    AddInstruction(Opcode.MathAdd);
                    AddInstruction(Opcode.HeapSet, AddressingMode.Runtime);
                }

                break;
            }

            case StructType structType:
            {
                structType.Struct.References.Add((newObject.Type, CurrentFile, CurrentContext));

                GenerateInitialValue(structType);
                break;
            }

            default:
                throw new CompilerException($"Unknown type definition {instanceType}", newObject.Type, CurrentFile);
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(ConstructorCall constructorCall)
    {
        GeneralType instanceType = GeneralType.From(constructorCall.Type, FindType, TryCompute);
        ImmutableArray<GeneralType> parameters = FindStatementTypes(constructorCall.Parameters);

        if (!GetConstructor(instanceType, parameters, CurrentFile, out FunctionQueryResult<CompiledConstructor>? result, out WillBeCompilerException? notFound, AddCompilable))
        { throw notFound.Instantiate(constructorCall.Type, CurrentFile); }

        CompiledConstructor? compiledFunction = result.Function;

        compiledFunction.References.Add((constructorCall, CurrentFile, CurrentContext));
        OnGotStatementType(constructorCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Constructor {compiledFunction.ToReadable()} could not be called due to its protection level", constructorCall.Type, CurrentFile));
            return;
        }

        AddComment($"Call {compiledFunction.ToReadable()} {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        NewInstance newInstance = constructorCall.ToInstantiation();
        GeneralType newInstanceType = FindStatementType(newInstance);
        GenerateCodeForStatement(newInstance);

        for (int i = 0; i < newInstanceType.Size; i++)
        { AddInstruction(Opcode.StackLoad, AddressingMode.StackPointerRelative, -newInstanceType.Size * BytecodeProcessor.StackDirection); }

        parameterCleanup = GenerateCodeForParameterPassing(constructorCall.Parameters, compiledFunction);
        parameterCleanup.Insert(0, (newInstanceType.Size, false, newInstanceType, newInstance.Position));

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset);

        if (compiledFunction.InstructionOffset == -1)
        { UndefinedConstructorOffsets.Add(new UndefinedOffset<CompiledConstructor>(jumpInstruction, false, constructorCall, compiledFunction, CurrentFile)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }
    void GenerateCodeForStatement(Field field)
    {
        field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType is EnumType enumType)
        {
            if (!enumType.Enum.GetValue(field.Identifier.Content, out DataItem enumValue))
            { throw new CompilerException($"I didn't find anything like \"{field.Identifier.Content}\" in the enum {enumType.Enum.Identifier}", field.Identifier, CurrentFile); }

            OnGotStatementType(field, new BuiltinType(enumValue.Type));

            AddInstruction(Opcode.Push, enumValue);
            return;
        }

        if (prevType is ArrayType arrayType && field.Identifier.Equals("Length"))
        {
            OnGotStatementType(field, new BuiltinType(BasicType.Integer));
            field.PredictedValue = arrayType.Length;

            AddInstruction(Opcode.Push, arrayType.Length);
            return;
        }

        if (prevType is PointerType pointerType)
        {
            if (pointerType.To is not StructType structPointerType)
            { throw new CompilerException($"Could not get the field offsets of type {pointerType}", field.PrevStatement, CurrentFile); }

            if (!structPointerType.GetField(field.Identifier.Content, out CompiledField? fieldDefinition, out int fieldOffset))
            { throw new InternalException($"Field \"{field.Identifier}\" not found", field.Identifier, CurrentFile); }

            field.CompiledType = fieldDefinition.Type;
            field.Reference = fieldDefinition;
            fieldDefinition.References.Add(new Reference<Statement>(field, CurrentFile, CurrentContext));

            GenerateCodeForStatement(field.PrevStatement);

            CheckPointerNull();

            AddInstruction(Opcode.Push, fieldOffset);
            AddInstruction(Opcode.MathAdd);
            AddInstruction(Opcode.HeapGet, AddressingMode.Runtime);

            return;
        }

        if (prevType is not StructType structType) throw new NotImplementedException();

        GeneralType type = FindStatementType(field);

        if (!structType.GetField(field.Identifier.Content, out CompiledField? compiledField, out _))
        { throw new CompilerException($"Field definition \"{field.Identifier}\" not found in type \"{structType}\"", field, CurrentFile); }

        field.CompiledType = compiledField.Type;
        field.Reference = compiledField;
        compiledField.References.Add(new Reference<Statement>(field, CurrentFile, CurrentContext));

        // if (CurrentContext?.Context != null)
        // {
        //     switch (compiledField.Protection)
        //     {
        //         case Protection.Private:
        //             if (CurrentContext.Context.Identifier.Content != compiledField.Class.Identifier.Content)
        //             { throw new CompilerException($"Can not access field \"{compiledField.Identifier.Content}\" of class \"{compiledField.Class.Identifier}\" due to it's protection level", field, CurrentFile); }
        //             break;
        //         case Protection.Public:
        //             break;
        //         default: throw new UnreachableException();
        //     }
        // }

        if (IsItInHeap(field))
        {
            int offset = GetDataOffset(field);
            ValueAddress pointerOffset = GetBaseAddress(field);
            for (int i = 0; i < type.Size; i++)
            {
                AddComment($"{i}:");
                HeapLoad(pointerOffset, offset + i);
            }
        }
        else
        {
            ValueAddress offset = GetDataAddress(field);
            StackLoad(offset, compiledField.Type.Size);
        }
    }
    void GenerateCodeForStatement(IndexCall index)
    {
        GeneralType prevType = FindStatementType(index.PrevStatement);

        if (prevType is ArrayType arrayType)
        {
            if (index.PrevStatement is not Identifier identifier)
            { throw new NotSupportedException($"Only variables/parameters supported by now", index.PrevStatement, CurrentFile); }

            if (TryCompute(index.Index, out DataItem computedIndexData))
            {
                index.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || computedIndexData >= arrayType.Length)
                { AnalysisCollection?.Warnings.Add(new Warning($"Index out of range", index.Index, CurrentFile)); }

                if (GetParameter(identifier.Content, out CompiledParameter? param))
                {
                    if (param.Type != arrayType)
                    { throw new NotImplementedException(); }

                    ValueAddress offset = GetBaseAddress(param, (int)computedIndexData * arrayType.Of.Size);
                    StackLoad(offset);

                    throw new NotImplementedException();
                }

                if (GetVariable(identifier.Content, out CompiledVariable? val))
                {
                    if (val.Type != arrayType)
                    { throw new NotImplementedException(); }

                    int offset = (int)computedIndexData * arrayType.Of.Size;
                    ValueAddress address = new(val);

                    StackLoad(address + offset, arrayType.Of.Size);

                    return;
                }
            }

            {
                ValueAddress address = GetDataAddress(identifier);
                AddInstruction(Opcode.Push, address.Address * BytecodeProcessor.StackDirection);

                switch (address.AddressingMode)
                {
                    case AddressingMode.Absolute:
                    case AddressingMode.Runtime:
                        throw new NotImplementedException();
                    case AddressingMode.BasePointerRelative:
                        AddInstruction(Opcode.GetBasePointer);
                        AddInstruction(Opcode.MathAdd);
                        break;
                    default:
                        throw new UnreachableException();
                }

                GenerateCodeForStatement(index.Index);
                if (BytecodeProcessor.StackDirection > 0) AddInstruction(Opcode.MathAdd);
                else AddInstruction(Opcode.MathSub);

                AddInstruction(Opcode.StackLoad, AddressingMode.Runtime);
                return;
            }

            throw new NotImplementedException();
        }

        if (!GetIndexGetter(prevType, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out _, AddCompilable))
        {
            if (prevType is not PointerType pointerType)
            { throw new CompilerException($"Index getter \"{prevType}[]\" not found", index, CurrentFile); }

            GenerateCodeForStatement(index.PrevStatement);

            AddInstruction(Opcode.Push, pointerType.To.Size);
            GeneralType indexType = FindStatementType(index.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", index.Index, CurrentFile); }
            GenerateCodeForStatement(index.Index);
            AddInstruction(Opcode.MathMult);

            AddInstruction(Opcode.MathAdd);

            CheckPointerNull();

            AddInstruction(Opcode.HeapGet, AddressingMode.Runtime);

            return;
        }

        GenerateCodeForFunctionCall_Function(new FunctionCall(
                index.PrevStatement,
                Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerGet),
                new StatementWithValue[]
                {
                    index.Index,
                },
                index.Brackets,
                index.OriginalFile
            ), result.Function);
    }
    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Equals(ModifierKeywords.Ref))
        {
            ValueAddress address = GetDataAddress(statement);

            if (address.InHeap)
            { throw new CompilerException($"This value is stored in the heap and not in the stack", statement, CurrentFile); }

            if (address.IsReference)
            {
                StackLoad(address.ToUnreferenced());
            }
            else
            {
                AddInstruction(Opcode.Push, address.Address * BytecodeProcessor.StackDirection);

                switch (address.AddressingMode)
                {
                    case AddressingMode.Absolute:
                        break;
                    case AddressingMode.Runtime:
                        throw new NotImplementedException();
                    case AddressingMode.BasePointerRelative:
                        AddInstruction(Opcode.GetBasePointer);
                        AddInstruction(Opcode.MathAdd);
                        break;
                    case AddressingMode.StackPointerRelative:
                    default:
                        throw new UnreachableException();
                }
            }
            return;
        }

        if (modifier.Equals(ModifierKeywords.Temp))
        {
            GenerateCodeForStatement(statement);
            return;
        }

        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(LiteralList listValue)
    { throw new NotImplementedException(); }
    void GenerateCodeForStatement(TypeCast typeCast)
    {
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = GeneralType.From(typeCast.Type, FindType, TryCompute);
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (statementType.Size != targetType.Size)
        { throw new CompilerException($"Can't modify the size of the value. You tried to convert from {statementType} (size of {statementType.Size}) to {targetType} (size of {targetType.Size})", new Position(typeCast.Keyword, typeCast.Type), CurrentFile); }

        if (!Settings.DontOptimize &&
            targetType is BuiltinType targetBuiltinType &&
            TryComputeSimple(typeCast.PrevStatement, out DataItem prevValue) &&
            DataItem.TryCast(ref prevValue, targetBuiltinType.RuntimeType))
        {
            AddInstruction(Opcode.Push, prevValue);
            return;
        }

        GenerateCodeForStatement(typeCast.PrevStatement, targetType);

        GeneralType type = FindStatementType(typeCast.PrevStatement, targetType);

        if (targetType is not FunctionType && type == targetType)
        {
            AnalysisCollection?.Hints.Add(new Hint($"Redundant type conversion", typeCast.Keyword, CurrentFile));
            return;
        }

        if (type is BuiltinType && targetType is BuiltinType targetBuiltinType2)
        {
            AddInstruction(Opcode.Push, new DataItem((byte)targetBuiltinType2.Type.Convert()));
            AddInstruction(Opcode.TypeSet);
        }
    }

    /// <param name="silent">
    /// If set to <see langword="true"/> then it will <b>ONLY</b> generate the statements and does not
    /// generate variables or something like that.
    /// </param>
    void GenerateCodeForStatement(Block block, bool silent = false)
    {
        if (!Settings.DontOptimize &&
            block.Statements.Length == 0)
        {
            AddComment("Statements { }");
            return;
        }

        if (silent)
        {
            AddComment("Statements {");
            for (int i = 0; i < block.Statements.Length; i++)
            { GenerateCodeForStatement(block.Statements[i]); }
            AddComment("}");

            return;
        }

        OnScopeEnter(block);

        AddComment("Statements {");
        for (int i = 0; i < block.Statements.Length; i++)
        { GenerateCodeForStatement(block.Statements[i]); }
        AddComment("}");

        OnScopeExit(block.Brackets.End.Position);
    }

    void GenerateCodeForStatement(Statement statement, GeneralType? expectedType = null)
    {
        int startInstruction = GeneratedCode.Count;

        switch (statement)
        {
            case LiteralList v: GenerateCodeForStatement(v); break;
            case VariableDeclaration v: GenerateCodeForStatement(v); break;
            case KeywordCall v: GenerateCodeForStatement(v); break;
            case BinaryOperatorCall v: GenerateCodeForStatement(v); break;
            case UnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case AnyAssignment v: GenerateCodeForStatement(v.ToAssignment()); break;
            case LiteralStatement v: GenerateCodeForStatement(v); break;
            case Identifier v: GenerateCodeForStatement(v, expectedType); break;
            case AddressGetter v: GenerateCodeForStatement(v); break;
            case Pointer v: GenerateCodeForStatement(v); break;
            case WhileLoop v: GenerateCodeForStatement(v); break;
            case ForLoop v: GenerateCodeForStatement(v); break;
            case IfContainer v: GenerateCodeForStatement(v); break;
            case NewInstance v: GenerateCodeForStatement(v); break;
            case ConstructorCall v: GenerateCodeForStatement(v); break;
            case IndexCall v: GenerateCodeForStatement(v); break;
            case Field v: GenerateCodeForStatement(v); break;
            case TypeCast v: GenerateCodeForStatement(v); break;
            case ModifiedStatement v: GenerateCodeForStatement(v); break;
            case AnyCall v: GenerateCodeForStatement(v); break;
            case Block v: GenerateCodeForStatement(v); break;
            default: throw new InternalException($"Unimplemented statement {statement.GetType().Name}");
        }

        DebugInfo?.SourceCodeLocations.Add(new SourceCodeLocation()
        {
            Instructions = (startInstruction, GeneratedCode.Count - 1),
            SourcePosition = statement.Position,
            Uri = CurrentFile,
        });
    }

    ImmutableArray<CleanupItem> CompileVariables(IEnumerable<VariableDeclaration> statements, bool addComments = true)
    {
        if (addComments) AddComment("Variables {");

        ImmutableArray<CleanupItem>.Builder result = ImmutableArray.CreateBuilder<CleanupItem>();

        foreach (VariableDeclaration statement in statements)
        {
            CleanupItem item = GenerateCodeForLocalVariable(statement);
            if (item.SizeOnStack == 0) continue;

            result.Add(item);
        }

        if (addComments) AddComment("}");

        return result.ToImmutable();
    }

    void CleanupVariables(ImmutableArray<CleanupItem> cleanupItems, Position position)
    {
        if (cleanupItems.Length == 0) return;
        AddComment("Clear Variables");
        for (int i = cleanupItems.Length - 1; i >= 0; i--)
        {
            CleanupItem item = cleanupItems[i];

            if (item.ShouldDeallocate)
            {
                if (item.SizeOnStack != 1) throw new InternalException();
                if (item.Type is null) throw new InternalException();
                GenerateDestructor(item.Type, position);
            }
            else
            {
                for (int x = 0; x < item.SizeOnStack; x++)
                { AddInstruction(Opcode.Pop); }
            }

            CompiledVariables.Pop();
        }
    }

    void CleanupGlobalVariables(CleanupItem[] cleanupItems, Position position)
    {
        if (cleanupItems.Length == 0) return;
        AddComment("Clear Global Variables");
        for (int i = cleanupItems.Length - 1; i >= 0; i--)
        {
            CleanupItem item = cleanupItems[i];

            if (item.ShouldDeallocate)
            {
                if (item.SizeOnStack != 1) throw new InternalException();
                if (item.Type is null) throw new InternalException();
                GenerateDestructor(item.Type, position);
            }
            else
            {
                for (int x = 0; x < item.SizeOnStack; x++)
                { AddInstruction(Opcode.Pop); }
            }

            CompiledGlobalVariables.Pop();
        }
    }

    void GenerateCodeForValueSetter(Statement statementToSet, StatementWithValue value)
    {
        switch (statementToSet)
        {
            case Identifier v: GenerateCodeForValueSetter(v, value); break;
            case Field v: GenerateCodeForValueSetter(v, value); break;
            case IndexCall v: GenerateCodeForValueSetter(v, value); break;
            case Pointer v: GenerateCodeForValueSetter(v, value); break;
            default: throw new CompilerException($"The left side of the assignment operator should be a variable, field or memory address. Passed {statementToSet.GetType().Name}", statementToSet, CurrentFile);
        }
    }
    void GenerateCodeForValueSetter(Identifier statementToSet, StatementWithValue value)
    {
        if (GetConstant(statementToSet.Content, out _))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.ConstantName;
            throw new CompilerException($"Can not set constant value: it is readonly", statementToSet, CurrentFile);
        }

        if (GetParameter(statementToSet.Content, out CompiledParameter? parameter))
        {
            if (statementToSet.Content != StatementKeywords.This)
            { statementToSet.Token.AnalyzedType = TokenAnalyzedType.ParameterName; }
            statementToSet.CompiledType = parameter.Type;
            statementToSet.Reference = parameter;

            GeneralType valueType = FindStatementType(value, parameter.Type);

            AssignTypeCheck(parameter.Type, valueType, value);

            GenerateCodeForStatement(value);

            ValueAddress address = GetBaseAddress(parameter);

            StackStore(address);
        }
        else if (GetVariable(statementToSet.Content, out CompiledVariable? variable))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = variable.Type;
            statementToSet.Reference = variable;

            GeneralType valueType = FindStatementType(value, variable.Type);

            AssignTypeCheck(variable.Type, valueType, value);

            GenerateCodeForStatement(value);

            StackStore(new ValueAddress(variable), variable.Type.Size);
        }
        else if (GetGlobalVariable(statementToSet.Content, statementToSet.OriginalFile, out CompiledVariable? globalVariable, out _))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = globalVariable.Type;
            statementToSet.Reference = globalVariable;

            GeneralType valueType = FindStatementType(value, globalVariable.Type);

            AssignTypeCheck(globalVariable.Type, valueType, value);

            GenerateCodeForStatement(value);

            StackStore(GetGlobalVariableAddress(globalVariable));
        }
        else
        {
            throw new CompilerException($"Symbol \"{statementToSet.Content}\" not found", statementToSet, CurrentFile);
        }
    }
    void GenerateCodeForValueSetter(Field statementToSet, StatementWithValue value)
    {
        statementToSet.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        GeneralType valueType = FindStatementType(value);

        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);

        if (prevType is PointerType pointerType)
        {
            if (pointerType.To is not StructType structType)
            { throw new CompilerException($"Failed to get the field offsets of type {pointerType.To}", statementToSet.PrevStatement, CurrentFile); }

            if (!structType.GetField(statementToSet.Identifier.Content, out CompiledField? fieldDefinition, out int fieldOffset))
            { throw new CompilerException($"Failed to get the field offset for \"{statementToSet.Identifier}\" in type {pointerType.To}", statementToSet.Identifier, CurrentFile); }

            statementToSet.Reference = fieldDefinition;
            statementToSet.CompiledType = statementToSet.CompiledType;
            fieldDefinition.References.Add(new Reference<Statement>(statementToSet, CurrentFile, CurrentContext));

            GenerateCodeForStatement(statementToSet.PrevStatement);

            GenerateCodeForStatement(value);
            ValueAddress pointerAddress = new(-(valueType.Size + 1), AddressingMode.StackPointerRelative);
            HeapStore(pointerAddress, fieldOffset);

            AddInstruction(Opcode.Pop);
            return;
        }

        if (prevType is not StructType)
        { throw new NotImplementedException(); }

        GeneralType type = FindStatementType(statementToSet);

        if (type != valueType)
        { throw new CompilerException($"Can not set a \"{valueType}\" type value to the \"{type}\" type field.", value, CurrentFile); }

        GenerateCodeForStatement(value);

        if (IsItInHeap(statementToSet))
        {
            int offset = GetDataOffset(statementToSet);
            ValueAddress pointerOffset = GetBaseAddress(statementToSet);
            HeapStore(pointerOffset, offset);
        }
        else
        {
            ValueAddress offset = GetDataAddress(statementToSet);
            StackStore(offset, valueType.Size);
        }
    }
    void GenerateCodeForValueSetter(IndexCall statementToSet, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType valueType = FindStatementType(value);

        if (prevType is ArrayType arrayType)
        {
            if (statementToSet.PrevStatement is not Identifier identifier)
            { throw new NotSupportedException($"Only variables/parameters supported by now", statementToSet.PrevStatement, CurrentFile); }

            if (TryCompute(statementToSet.Index, out DataItem computedIndexData))
            {
                statementToSet.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || computedIndexData >= arrayType.Length)
                { AnalysisCollection?.Warnings.Add(new Warning($"Index out of range", statementToSet.Index, CurrentFile)); }

                GenerateCodeForStatement(value);

                if (GetParameter(identifier.Content, out _))
                { throw new NotImplementedException(); }

                if (GetVariable(identifier.Content, out CompiledVariable? variable))
                {
                    if (variable.Type != arrayType)
                    { throw new NotImplementedException(); }

                    int offset = (int)computedIndexData * arrayType.Of.Size;
                    StackStore(new ValueAddress(variable) + offset, arrayType.Of.Size);
                    return;
                }
            }

            throw new NotImplementedException();
        }

        if (!GetIndexSetter(prevType, valueType, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out _, AddCompilable))
        {
            if (prevType is not PointerType pointerType)
            { throw new CompilerException($"Index setter \"{prevType}[...] = {valueType}\" not found", statementToSet, CurrentFile); }

            AssignTypeCheck(pointerType.To, valueType, value);

            GenerateCodeForStatement(value);

            GenerateCodeForStatement(statementToSet.PrevStatement);

            AddInstruction(Opcode.Push, pointerType.To.Size);
            GeneralType indexType = FindStatementType(statementToSet.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", statementToSet.Index, CurrentFile); }
            GenerateCodeForStatement(statementToSet.Index);
            AddInstruction(Opcode.MathMult);

            AddInstruction(Opcode.MathAdd);

            CheckPointerNull();

            AddInstruction(Opcode.HeapSet, AddressingMode.Runtime);

            return;
        }

        GenerateCodeForFunctionCall_Function(new FunctionCall(
            statementToSet.PrevStatement,
            Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerSet),
            new StatementWithValue[]
            {
                statementToSet.Index,
                value,
            },
            statementToSet.Brackets,
            statementToSet.OriginalFile
        ), result.Function);
    }
    void GenerateCodeForValueSetter(Pointer statementToSet, StatementWithValue value)
    {
        GeneralType targetType = FindStatementType(statementToSet);

        if (targetType.Size != 1) throw new NotImplementedException();

        GenerateCodeForStatement(value);
        GenerateCodeForStatement(statementToSet.PrevStatement);

        AddInstruction(Opcode.HeapSet, AddressingMode.Runtime);
    }

    #endregion

    void OnScopeEnter(Block block) => OnScopeEnter(block.Position, block.Statements.OfType<VariableDeclaration>());

    void OnScopeEnter(Position position, IEnumerable<VariableDeclaration> variables)
    {
        CurrentScopeDebug.Push(new ScopeInformations()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                SourcePosition = position,
                Uri = CurrentFile,
            },
            Stack = new List<StackElementInformations>(),
        });

        AddComment("Scope enter");

        CompileLocalConstants(variables);

        CleanupStack.Push(CompileVariables(variables, CurrentContext is null));
        ReturnInstructions.Push(new List<int>());
    }

    void OnScopeExit(Position position)
    {
        AddComment("Scope exit");

        FinishJumpInstructions(ReturnInstructions.Last);
        ReturnInstructions.Pop();

        CleanupVariables(CleanupStack.Pop(), position);

        if (CanReturn)
        {
            StackLoad(ReturnFlagAddress);
            AddInstruction(Opcode.LogicNOT);
            ReturnInstructions.Last.Add(GeneratedCode.Count);
            AddInstruction(Opcode.JumpIfZero, 0);
        }

        CleanupLocalConstants();

        ScopeInformations scope = CurrentScopeDebug.Pop();
        scope.Location.Instructions.End = GeneratedCode.Count - 1;
        DebugInfo?.ScopeInformations.Add(scope);
    }

    #region GenerateCodeForLocalVariable

    CleanupItem GenerateCodeForLocalVariable(VariableDeclaration newVariable)
    {
        if (newVariable.Modifiers.Contains(ModifierKeywords.Const)) return CleanupItem.Null;

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        for (int i = 0; i < CompiledVariables.Count; i++)
        {
            if (CompiledVariables[i].Identifier.Content == newVariable.Identifier.Content)
            {
                AnalysisCollection?.Warnings.Add(new Warning($"Variable \"{CompiledVariables[i].Identifier}\" already defined", CompiledVariables[i].Identifier, CurrentFile));
                return CleanupItem.Null;
            }
        }

        int offset = TagCount.Last + VariablesSize;

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        StackElementInformations debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Tag = compiledVariable.Identifier.Content,
            Address = offset * BytecodeProcessor.StackDirection,
            BasepointerRelative = true,
            Size = compiledVariable.Type.Size,
        };

        if (compiledVariable.Type is PointerType)
        { debugInfo.Type = StackElementType.HeapPointer; }
        else
        { debugInfo.Type = StackElementType.Value; }
        newVariable.CompiledType = compiledVariable.Type;

        CurrentScopeDebug.Last.Stack.Add(debugInfo);

        CompiledVariables.Add(compiledVariable);

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        int size;

        if (!Settings.DontOptimize &&
            TryCompute(newVariable.InitialValue, out DataItem computedInitialValue))
        {
            newVariable.InitialValue.PredictedValue = computedInitialValue;

            AddComment($"Initial value {{");

            size = 1;

            AddInstruction(Opcode.Push, computedInitialValue);
            compiledVariable.IsInitialized = true;

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }
        else if (compiledVariable.Type is ArrayType arrayType &&
            arrayType.Of == BasicType.Char &&
            newVariable.InitialValue is LiteralStatement literalStatement &&
            literalStatement.Type == LiteralType.String &&
            literalStatement.Value.Length == arrayType.Length)
        {
            size = literalStatement.Value.Length;
            compiledVariable.IsInitialized = true;

            for (int i = 0; i < literalStatement.Value.Length; i++)
            {
                AddInstruction(Opcode.Push, new DataItem(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = GenerateInitialValue(compiledVariable.Type);

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }

        if (size != compiledVariable.Type.Size)
        { throw new InternalException($"Variable size ({compiledVariable.Type.Size}) and initial value size ({size}) mismatch"); }

        return new CleanupItem(size, newVariable.Modifiers.Contains(ModifierKeywords.Temp), compiledVariable.Type);
    }
    CleanupItem GenerateCodeForLocalVariable(Statement st)
    {
        if (st is VariableDeclaration newVariable)
        { return GenerateCodeForLocalVariable(newVariable); }
        return CleanupItem.Null;
    }
    ImmutableArray<CleanupItem> GenerateCodeForLocalVariable(IEnumerable<Statement> statements)
    {
        ImmutableArray<CleanupItem>.Builder result = ImmutableArray.CreateBuilder<CleanupItem>();

        foreach (Statement statement in statements)
        {
            CleanupItem item = GenerateCodeForLocalVariable(statement);
            if (item.SizeOnStack == 0) continue;

            result.Add(item);
        }

        return result.ToImmutable();
    }

    #endregion

    #region GenerateCodeForGlobalVariable

    CleanupItem GenerateCodeForGlobalVariable(VariableDeclaration newVariable)
    {
        if (newVariable.Modifiers.Contains(ModifierKeywords.Const)) return CleanupItem.Null;

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        int offset = TagCount.LastOrDefault + GlobalVariablesSize;

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        StackElementInformations debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Tag = compiledVariable.Identifier.Content,
            Address = offset * BytecodeProcessor.StackDirection,
            BasepointerRelative = false,
            Size = compiledVariable.Type.Size,
        };

        if (compiledVariable.Type is PointerType)
        { debugInfo.Type = StackElementType.HeapPointer; }
        else
        { debugInfo.Type = StackElementType.Value; }
        newVariable.CompiledType = compiledVariable.Type;

        CompiledGlobalVariables.Add(compiledVariable);

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        int size;

        if (!Settings.DontOptimize &&
            TryCompute(newVariable.InitialValue, out DataItem computedInitialValue))
        {
            newVariable.InitialValue.PredictedValue = computedInitialValue;

            AddComment($"Initial value {{");

            size = 1;

            AddInstruction(Opcode.Push, computedInitialValue);
            compiledVariable.IsInitialized = true;

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }
        else if (compiledVariable.Type is ArrayType arrayType &&
            arrayType.Of == BasicType.Char &&
            newVariable.InitialValue is LiteralStatement literalStatement &&
            literalStatement.Type == LiteralType.String &&
            literalStatement.Value.Length == arrayType.Length)
        {
            size = literalStatement.Value.Length;
            compiledVariable.IsInitialized = true;

            for (int i = 0; i < literalStatement.Value.Length; i++)
            {
                AddInstruction(Opcode.Push, new DataItem(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = GenerateInitialValue(compiledVariable.Type);

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }

        if (size != compiledVariable.Type.Size)
        { throw new InternalException($"Variable size ({compiledVariable.Type.Size}) and initial value size ({size}) mismatch"); }

        return new CleanupItem(size, newVariable.Modifiers.Contains(ModifierKeywords.Temp), compiledVariable.Type);
    }
    CleanupItem GenerateCodeForGlobalVariable(Statement st)
    {
        if (st is VariableDeclaration newVariable)
        { return GenerateCodeForGlobalVariable(newVariable); }
        return CleanupItem.Null;
    }
    void GenerateCodeForGlobalVariable(IEnumerable<Statement> statements, Stack<CleanupItem> cleanupStack)
    {
        foreach (Statement statement in statements)
        {
            CleanupItem item = GenerateCodeForGlobalVariable(statement);
            if (item.SizeOnStack == 0) continue;

            cleanupStack.Push(item);
        }
    }

    #endregion

    #region GenerateCodeFor...

    int VariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariable variable in CompiledVariables)
            { sum += variable.Type.Size; }
            return sum;
        }
    }

    int GlobalVariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariable variable in CompiledGlobalVariables)
            { sum += variable.Type.Size; }
            return sum;
        }
    }

    void FinishJumpInstructions(IEnumerable<int> jumpInstructions)
        => FinishJumpInstructions(jumpInstructions, GeneratedCode.Count);
    void FinishJumpInstructions(IEnumerable<int> jumpInstructions, int jumpTo)
    {
        foreach (int jumpInstruction in jumpInstructions)
        {
            GeneratedCode[jumpInstruction].Parameter = jumpTo - jumpInstruction;
        }
    }

    bool GenerateCodeForFunction(FunctionThingDefinition function)
    {
        if (function.Identifier is not null &&
            LanguageConstants.KeywordList.Contains(function.Identifier.ToString()))
        { throw new CompilerException($"The identifier \"{function.Identifier}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.File); }

        if (function.Identifier is not null)
        { function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName; }

        if (function is FunctionDefinition functionDefinition)
        {
            for (int i = 0; i < functionDefinition.Attributes.Length; i++)
            {
                if (functionDefinition.Attributes[i].Identifier.Equals(AttributeConstants.ExternalIdentifier))
                { return false; }
            }
        }

        Print?.Invoke($"Generate {function.ToReadable()} ...", LogType.Debug);

        if (function.Identifier is not null)
        { AddComment(function.Identifier.Content + ((function.Parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }")); }
        else
        { AddComment("null" + ((function.Parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }")); }

        CurrentContext = function as ISameCheck;
        InFunction = true;

        TagCount.Push(0);

        CompiledParameters.Clear();
        CompiledVariables.Clear();
        ReturnInstructions.Clear();

        CompileParameters(function.Parameters);

        CurrentFile = function.File;

        int instructionStart = GeneratedCode.Count;

        CanReturn = true;
        AddInstruction(Opcode.Push, new DataItem(false));
        TagCount[^1]++;

        OnScopeEnter(function.Block ?? throw new CompilerException($"Function \"{function.ToReadable()}\" does not have a body", function, function.File));

        if (function is IHaveCompiledType returnType)
        {
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
            {
                Address = GetReturnValueAddress(returnType.Type).Address * BytecodeProcessor.StackDirection,
                BasepointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = returnType.Type.Size,
                Tag = "Return Value",
                Type = StackElementType.Value,
            });
        }

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
        {
            Address = ReturnFlagOffset * BytecodeProcessor.StackDirection,
            BasepointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Return Flag",
            Type = StackElementType.Value,
        });
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
        {
            Address = SavedBasePointerOffset * BytecodeProcessor.StackDirection,
            BasepointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Saved BasePointer",
            Type = StackElementType.Value,
        });
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
        {
            Address = SavedCodePointerOffset * BytecodeProcessor.StackDirection,
            BasepointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Saved CodePointer",
            Type = StackElementType.Value,
        });
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
        {
            Address = AbsoluteGlobalOffset * BytecodeProcessor.StackDirection,
            BasepointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Absolute Global Offset",
            Type = StackElementType.Value,
        });

        for (int i = 0; i < CompiledParameters.Count; i++)
        {
            CompiledParameter p = CompiledParameters[i];

            StackElementInformations debugInfo = new()
            {
                Address = GetBaseAddress(p).Address * BytecodeProcessor.StackDirection,
                Kind = StackElementKind.Parameter,
                BasepointerRelative = true,
                Size = p.Type.Size,
                Tag = p.Identifier.Content,
            };
            if (p.IsRef)
            { debugInfo.Type = StackElementType.StackPointer; }
            else if (p.Type is PointerType)
            { debugInfo.Type = StackElementType.HeapPointer; }
            else
            { debugInfo.Type = StackElementType.Value; }
            CurrentScopeDebug.Last.Stack.Add(debugInfo);
        }

        AddComment("Statements {");
        for (int i = 0; i < function.Block.Statements.Length; i++)
        { GenerateCodeForStatement(function.Block.Statements[i]); }
        AddComment("}");

        CurrentFile = null;

        CanReturn = false;

        OnScopeExit(function.Block.Brackets.End.Position);

        AddInstruction(Opcode.Pop);
        TagCount[^1]--;

        AddComment("Return");
        Return();

        if (function.Block != null && function.Block.Statements.Length > 0) AddComment("}");

        if (function.Identifier is not null)
        {
            DebugInfo?.FunctionInformations.Add(new FunctionInformations()
            {
                IsValid = true,
                SourcePosition = function.Identifier.Position,
                Identifier = function.Identifier.Content,
                File = function.File,
                ReadableIdentifier = function.ToReadable(),
                Instructions = (instructionStart, GeneratedCode.Count),
            });
        }

        CompiledParameters.Clear();
        CompiledVariables.Clear();
        ReturnInstructions.Clear();

        TagCount.Pop();

        CurrentContext = null;
        InFunction = false;
        return true;
    }

    bool GenerateCodeForFunctions<TFunction>(IEnumerable<TFunction> functions)
        where TFunction : FunctionThingDefinition, IHaveInstructionOffset, IReferenceable
    {
        bool generatedAnything = false;
        foreach (TFunction function in functions)
        {
            if (function.IsTemplate) continue;
            if (function.InstructionOffset >= 0) continue;
            if (!function.References.Any())
            {
                switch (CompileLevel)
                {
                    case CompileLevel.Minimal: continue;
                    case CompileLevel.Exported:
                        if (!function.IsExport) continue;
                        break;
                }
            }

            function.InstructionOffset = GeneratedCode.Count;
            if (GenerateCodeForFunction(function))
            { generatedAnything = true; }
        }
        return generatedAnything;
    }

    bool GenerateCodeForCompilableFunction<T>(CompliableTemplate<T> function)
        where T : FunctionThingDefinition, ITemplateable<T>
    {
        SetTypeArguments(function.TypeArguments);
        bool generatedAnything = GenerateCodeForFunction(function.Function);
        TypeArguments.Clear();
        return generatedAnything;
    }

    bool GenerateCodeForCompilableFunctions<T>(IReadOnlyList<CompliableTemplate<T>> functions)
        where T : FunctionThingDefinition, ITemplateable<T>, IHaveInstructionOffset
    {
        bool generatedAnything = false;
        int i = 0;
        while (i < functions.Count)
        {
            CompliableTemplate<T> function = functions[i];
            i++;

            if (function.Function.InstructionOffset >= 0) continue;

            function.Function.InstructionOffset = GeneratedCode.Count;
            if (GenerateCodeForCompilableFunction(function))
            { generatedAnything = true; }
        }
        return generatedAnything;
    }

    void CompileParameters(ImmutableArray<ParameterDefinition> parameters)
    {
        int paramsSize = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            GeneralType parameterType = GeneralType.From(parameters[i].Type, FindType);
            parameters[i].Type.SetAnalyzedType(parameterType);

            CompiledParameters.Add(new CompiledParameter(i, -(paramsSize + 1 + CodeGeneratorForMain.TagsBeforeBasePointer), parameterType, parameters[i]));

            paramsSize += parameterType.Size;
        }
    }

    void GenerateCodeForTopLevelStatements(ImmutableArray<Statement> statements)
    {
        if (statements.IsDefaultOrEmpty) return;

        CurrentScopeDebug.Push(new ScopeInformations()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                SourcePosition = new Position(statements),
                Uri = CurrentFile,
            },
            Stack = new List<StackElementInformations>(),
        });

        AddComment("TopLevelStatements {");

        AddInstruction(Opcode.GetBasePointer);
        AddInstruction(Opcode.SetBasePointer, AddressingMode.StackPointerRelative, 0);

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
        {
            Address = SavedBasePointerOffset * BytecodeProcessor.StackDirection,
            BasepointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Saved BasePointer",
            Type = StackElementType.Value,
        });

        CurrentContext = null;
        TagCount.Push(0);
        ReturnInstructions.Push(new List<int>());

        CanReturn = true;
        AddInstruction(Opcode.Push, new DataItem(false));
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
        {
            Address = ReturnFlagOffset * BytecodeProcessor.StackDirection,
            BasepointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Return Flag",
            Type = StackElementType.Value,
        });
        TagCount.Last++;

        AddComment("Variables");
        CleanupStack.Push(GenerateCodeForLocalVariable(statements));

        AddComment("Statements {");
        foreach (Statement statement in statements)
        { GenerateCodeForStatement(statement); }
        AddComment("}");

        FinishJumpInstructions(ReturnInstructions.Last);
        ReturnInstructions.Pop();

        CompiledGlobalVariables.AddRange(CompiledVariables);
        CleanupVariables(CleanupStack.Pop(), statements[^1].Position.NextLine());

        CanReturn = false;
        AddInstruction(Opcode.Pop);
        TagCount.Last--;

        TagCount.Pop();

        AddInstruction(Opcode.SetBasePointer, AddressingMode.Runtime, 0);

        AddComment("}");

        ScopeInformations scope = CurrentScopeDebug.Pop();
        scope.Location.Instructions.End = GeneratedCode.Count - 1;
        DebugInfo?.ScopeInformations.Add(scope);
    }

    #endregion

    BBLangGeneratorResult GenerateCode(CompilerResult compilerResult, MainGeneratorSettings settings)
    {
        if (settings.ExternalFunctionsCache)
        { throw new NotImplementedException(); }

        Print?.Invoke("Generating code ...", LogType.Debug);

        List<string> usedExternalFunctions = new();

        foreach (CompiledFunction function in CompiledFunctions)
        {
            if (function.IsExternal)
            { usedExternalFunctions.Add(function.ExternalFunctionName); }
        }

        foreach (CompiledOperator @operator in CompiledOperators)
        {
            if (@operator.IsExternal)
            { usedExternalFunctions.Add(@operator.ExternalFunctionName); }
        }

        foreach ((ImmutableArray<Statement> statements, _) in compilerResult.TopLevelStatements)
        {
            CompileGlobalConstants(statements);
        }

        CurrentScopeDebug.Push(new ScopeInformations()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                SourcePosition = Position.UnknownPosition,
                Uri = CurrentFile,
            },
            Stack = new List<StackElementInformations>(),
        });

        {
            // Exit code

            AddComment("Push exit code:");
            AddInstruction(Opcode.Push, new DataItem(0));
        }

        {
            // Absolute global offset

            AddInstruction(Opcode.GetRegister, 2);
            AddInstruction(Opcode.Push, -1 * BytecodeProcessor.StackDirection);
            AddInstruction(Opcode.MathAdd);

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
            {
                Address = AbsoluteGlobalOffset * BytecodeProcessor.StackDirection,
                BasepointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = 1,
                Tag = "Absolute Global Offset",
                Type = StackElementType.Value,
            });
        }

        for (int i = 0; i < compilerResult.TopLevelStatements.Length - 1; i++)
        {
            (ImmutableArray<Statement> statements, Uri file) = compilerResult.TopLevelStatements[i];
            CurrentFile = file;
            Print?.Invoke($"Generating top level statements for file {file?.ToString() ?? "null"} ...", LogType.Debug);
            GenerateCodeForTopLevelStatements(statements);
            CurrentFile = null;
        }

        if (compilerResult.TopLevelStatements.Length > 0)
        {
            CurrentFile = compilerResult.File;
            Print?.Invoke($"Generating top level statements for file {compilerResult.TopLevelStatements[^1].File?.ToString() ?? "null"} ...", LogType.Debug);
            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements[^1].Statements);
            CurrentFile = null;
        }

        AddInstruction(Opcode.Pop); // Pop abs global offset

        AddInstruction(Opcode.Exit); // Exit code already there

        {
            ScopeInformations scope = CurrentScopeDebug.Pop();
            scope.Location.Instructions.End = GeneratedCode.Count - 1;
            DebugInfo?.ScopeInformations.Add(scope);
        }

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

        return new BBLangGeneratorResult()
        {
            Code = GeneratedCode.Select(v => new Instruction(v)).ToImmutableArray(),
            DebugInfo = DebugInfo,
        };
    }
}