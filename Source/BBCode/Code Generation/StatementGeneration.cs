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

    void AddInstruction(
        Opcode opcode)
        => AddInstruction(new PreparationInstruction(opcode));

    void AddInstruction(
        Opcode opcode,
        CompiledValue operand)
        => AddInstruction(new PreparationInstruction(opcode, operand));

    void AddInstruction(
        Opcode opcode,
        int operand)
        => AddInstruction(new PreparationInstruction(opcode, new CompiledValue(operand)));

    void AddInstruction(
        Opcode opcode,
        bool operand)
        => AddInstruction(new PreparationInstruction(opcode, new CompiledValue(operand)));

    void AddInstruction(Opcode opcode,
        InstructionOperand operand1)
        => AddInstruction(new PreparationInstruction(opcode, operand1));

    void AddInstruction(Opcode opcode,
        InstructionOperand operand1,
        InstructionOperand operand2)
        => AddInstruction(new PreparationInstruction(opcode, operand1, operand2));

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

        allocator.References.Add(new Reference<StatementWithValue?>(size, CurrentFile, CurrentContext));

        if (!allocator.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", size, CurrentFile));
            return;
        }

        if (TryEvaluate(allocator, parameters, out CompiledValue? returnValue, out Statement[]? runtimeStatements) &&
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

        parameterCleanup = GenerateCodeForArguments(parameters, allocator);

        AddComment(" .:");

        int jumpInstruction = Call(allocator.InstructionOffset);

        if (allocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, size, allocator, CurrentFile)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateDeallocator(StatementWithValue value)
    {
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create(value);
        ImmutableArray<GeneralType> parameterTypes = FindStatementTypes(parameters);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameterTypes, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out _, AddCompilable))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found", value, CurrentFile); }
        CompiledFunction? deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue?>(value, CurrentFile, CurrentContext));

        if (!deallocator.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", value, CurrentFile));
            return;
        }

        if (TryEvaluate(deallocator, parameters, out CompiledValue? returnValue, out Statement[]? runtimeStatements) &&
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

        if (deallocator.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            GenerateInitialValue(deallocator.Type);
            AddComment($"}}");
        }

        parameterCleanup = GenerateCodeForArguments(parameters, deallocator);

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset);

        if (deallocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, value, deallocator, CurrentFile)); }

        if (deallocator.ReturnSomething)
        {
            AddComment($" Clear return value:");
            for (int i = 0; i < deallocator.Type.SizeBytes; i++)
            { Pop(BitWidth._8); }
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateDeallocator(GeneralType deallocateableType, Position position)
    {
        ImmutableArray<GeneralType> parameterTypes = ImmutableArray.Create(deallocateableType);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameterTypes, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? notFoundError, AddCompilable))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found ({notFoundError.Message})", position, CurrentFile); }
        CompiledFunction? deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue?>(null));

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

        Stack<ParameterCleanupItem> parameterCleanup = new() { new(deallocateableType.SizeBytes, false, deallocateableType, position) };

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset);

        if (deallocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, position, deallocator, CurrentFile)); }

        if (deallocator.ReturnSomething)
        {
            AddComment($" Clear return value:");

            const int returnValueSize = 0;
            for (int i = 0; i < returnValueSize; i++)
            { Pop(BitWidth._8); }
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateDestructor(GeneralType deallocateableType, Position position)
    {
        ImmutableArray<GeneralType> argumentTypes = ImmutableArray.Create<GeneralType>(deallocateableType);
        FunctionQueryResult<CompiledGeneralFunction>? result;

        if (deallocateableType is PointerType deallocateablePointerType)
        {
            if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, CurrentFile, out result, out WillBeCompilerException? error, AddCompilable))
            {
                AddComment($"Pointer value should be already there");
                GenerateDeallocator(deallocateableType, position);
                AddComment("}");

                if (deallocateablePointerType.To is not BuiltinType)
                {
                    AnalysisCollection?.Warnings.Add(new Warning($"Destructor for type \"{deallocateableType}\" not found", position, CurrentFile));
                    AnalysisCollection?.Warnings.Add(error.InstantiateWarning(position, CurrentFile));
                }

                return;
            }
        }
        else
        {
            if (!GetGeneralFunction(deallocateableType, argumentTypes, BuiltinFunctionIdentifiers.Destructor, CurrentFile, out result, out WillBeCompilerException? error, AddCompilable))
            {
                AnalysisCollection?.Warnings.Add(new Warning($"Destructor for type \"{deallocateableType}\" not found", position, CurrentFile));
                AnalysisCollection?.Warnings.Add(error.InstantiateWarning(position, CurrentFile));
                return;
            }
        }

        CompiledGeneralFunction? destructor = result.Function;

        destructor.References.Add(new Reference<Statement?>(null));

        if (!destructor.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Destructor for type \"{deallocateableType}\" function cannot be called due to its protection level", position, CurrentFile));
            AddComment("}");
            return;
        }

        AddComment(" Param0 should be already there");

        AddComment(" .:");

        int jumpInstruction = Call(destructor.InstructionOffset);

        if (destructor.InstructionOffset == InvalidFunctionAddress)
        { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, false, position, destructor, CurrentFile)); }

        AddComment(" Clear Param0:");

        if (deallocateableType is PointerType)
        {
            GenerateDeallocator(deallocateableType, position);
        }
        else
        {
            for (int i = 0; i < deallocateableType.SizeBytes; i++)
            { Pop(BitWidth._8); }
        }

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
            if (keywordCall.Arguments.Length > 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {keywordCall.Arguments.Length}", keywordCall, CurrentFile); }

            if (!CanReturn)
            { throw new CompilerException($"Can't return for some reason", keywordCall.Identifier, CurrentFile); }

            if (keywordCall.Arguments.Length == 1)
            {
                AddComment(" Param 0:");

                StatementWithValue returnValue = keywordCall.Arguments[0];
                GeneralType returnValueType = FindStatementType(returnValue);

                GenerateCodeForStatement(returnValue);

                AssignTypeCheck(CurrentReturnType, returnValueType, returnValue);

                if (InFunction)
                {
                    AddComment(" Set return value:");
                    StackStore(GetReturnValueAddress(returnValueType), returnValueType.SizeBytes);
                }
                else
                {
                    AddComment(" Set exit code:");
                    StackStore(ExitCodeAddress, ExitCodeType.BitWidth);
                }
            }

            AddComment(" .:");

            if (CanReturn)
            {
                AddInstruction(Opcode.Push, new CompiledValue(true));
                StackStore(ReturnFlagAddress, ReturnFlagType.BitWidth);
            }

            ReturnInstructions.Last.Add(GeneratedCode.Count);
            AddInstruction(Opcode.Jump, 0);

            AddComment("}");

            return;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Throw)
        {
            if (keywordCall.Arguments.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Throw}\": required {1} passed {keywordCall.Arguments}", keywordCall, CurrentFile); }

            AddComment(" Param 0:");

            StatementWithValue throwValue = keywordCall.Arguments[0];
            GeneralType throwType = FindStatementType(throwValue);

            GenerateCodeForStatement(throwValue);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(throwType.BitWidth));
                AddInstruction(Opcode.Throw, reg.Get(throwType.BitWidth));
            }

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
            if (keywordCall.Arguments.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Delete}\": required {1} passed {keywordCall.Arguments.Length}", keywordCall, CurrentFile); }

            GenerateCodeForStatement(keywordCall.Arguments[0]);

            GeneralType deletableType = FindStatementType(keywordCall.Arguments[0]);
            GenerateDestructor(deletableType, keywordCall.Arguments[0].Position);

            return;
        }

        throw new CompilerException($"Unknown keyword \"{keywordCall.Identifier}\"", keywordCall.Identifier, CurrentFile);
    }

    Stack<ParameterCleanupItem> GenerateCodeForArguments(IReadOnlyList<StatementWithValue> arguments, ICompiledFunction compiledFunction, int alreadyPassed = 0)
    {
        Stack<ParameterCleanupItem> argumentCleanup = new();

        for (int i = 0; i < arguments.Count; i++)
        {
            StatementWithValue argument = arguments[i];
            GeneralType argumentType = FindStatementType(argument);
            ParameterDefinition parameter = compiledFunction.Parameters[i + alreadyPassed];
            GeneralType parameterType = compiledFunction.ParameterTypes[i + alreadyPassed];

            if (argumentType.SizeBytes != parameterType.SizeBytes)
            { throw new InternalException($"Bad argument type passed: expected {parameterType} passed {argumentType}"); }

            AddComment($" Pass {parameter}:");

            bool typeAllowsTemp = argumentType is PointerType;

            bool calleeAllowsTemp = parameter.Modifiers.Contains(ModifierKeywords.Temp);

            bool callerAllowsTemp;

            if (callerAllowsTemp = StatementCanBeDeallocated(argument, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !calleeAllowsTemp)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", argument, CurrentFile)); }
                if (explicitDeallocate && !typeAllowsTemp)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this type", argument, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", argument, CurrentFile)); }
            }

            GenerateCodeForStatement(argument, parameterType);

            argumentCleanup.Push((argumentType.SizeBytes, calleeAllowsTemp && callerAllowsTemp && typeAllowsTemp, argumentType, argument.Position));
        }

        return argumentCleanup;
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

            parameterCleanup.Push((passedParameterType.SizeBytes, canDeallocate, passedParameterType, passedParameter.Position));
        }

        return parameterCleanup;
    }

    void GenerateCodeForParameterCleanup(Stack<ParameterCleanupItem> parameterCleanup)
    {
        AddComment(" Clear Params:");
        while (parameterCleanup.Count > 0)
        {
            ParameterCleanupItem passedParameter = parameterCleanup.Pop();

            if (passedParameter.CanDeallocate)
            {
                GenerateDestructor(passedParameter.Type, passedParameter.Position);
                continue;
            }

            for (int i = 0; i < passedParameter.Size; i++)
            { Pop(BitWidth._8); }
        }
    }

    void GenerateCodeForFunctionCall_External<TFunction>(IReadOnlyList<StatementWithValue> parameters, bool saveValue, TFunction compiledFunction, ExternalFunctionBase externalFunction)
        where TFunction : ICompiledFunction, ISimpleReadable
    {
        AddComment($"Call {compiledFunction.ToReadable()} {{");

        if (compiledFunction.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            GenerateInitialValue(compiledFunction.Type);
            AddComment($"}}");
        }

        // TODO: what is this -1
        int returnValueOffset = -1;

        Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForArguments(parameters, compiledFunction);
        for (int i = 0; i < parameterCleanup.Count; i++)
        { returnValueOffset -= parameterCleanup[i].Size; }

        AddComment(" .:");
        AddInstruction(Opcode.CallExternal, externalFunction.Id);

        if (compiledFunction.ReturnSomething)
        {
            if (saveValue)
            {
                AddComment($" Store return value:");
                StackStore(new AddressOffset(Register.BasePointer, returnValueOffset), compiledFunction.Type.SizeBytes);
            }
            else
            {
                AddComment($" Clear return value:");
                for (int i = 0; i < compiledFunction.Type.SizeBytes; i++)
                { Pop(BitWidth._8); }
            }
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
    {
        compiledFunction.References.Add((functionCall, CurrentFile, CurrentContext));
        OnGotStatementType(functionCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(functionCall.File ?? CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"The {compiledFunction.ToReadable()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
            return;
        }

        if (functionCall.MethodArguments.Length != compiledFunction.ParameterCount)
        { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ToReadable()}: required {compiledFunction.ParameterCount} passed {functionCall.MethodArguments.Length}", functionCall, CurrentFile); }

        if (compiledFunction.BuiltinFunctionName == BuiltinFunctions.Allocate)
        {
            GenerateAllocator(functionCall.Arguments[0]);
            return;
        }

        if (compiledFunction.BuiltinFunctionName == BuiltinFunctions.Free)
        {
            GenerateDeallocator(functionCall.Arguments[0]);
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

            GenerateCodeForFunctionCall_External(functionCall.MethodArguments, functionCall.SaveValue, compiledFunction, externalFunction);
            return;
        }

        AddComment($"Call {compiledFunction.ToReadable()} {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        if (compiledFunction.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            GenerateInitialValue(compiledFunction.Type);
            AddComment($"}}");
        }

        parameterCleanup = GenerateCodeForArguments(functionCall.MethodArguments, compiledFunction);

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset);

        if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, functionCall, compiledFunction, CurrentFile)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            for (int i = 0; i < compiledFunction.Type.SizeBytes; i++)
            { Pop(BitWidth._8); }
        }

        AddComment("}");
    }

    void GenerateCodeForStatement(AnyCall anyCall)
    {
        if (anyCall.PrevStatement is Identifier _identifier &&
            _identifier.Content == "sizeof")
        {
            _identifier.Token.AnalyzedType = TokenAnalyzedType.Keyword;

            if (anyCall.Arguments.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": required {1} passed {anyCall.Arguments.Length}", anyCall, CurrentFile); }

            StatementWithValue param = anyCall.Arguments[0];
            GeneralType paramType;
            if (param is TypeStatement typeStatement)
            { paramType = GeneralType.From(typeStatement.Type, FindType, TryCompute); }
            else
            { paramType = FindStatementType(param); }

            OnGotStatementType(anyCall, new BuiltinType(BasicType.Integer));
            anyCall.PredictedValue = paramType.SizeBytes;

            AddInstruction(Opcode.Push, paramType.SizeBytes);

            return;
        }

        if (GetFunction(anyCall, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? notFound, AddCompilable) &&
            anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            CompiledFunction compiledFunction = result.Function;

            if (anyCall.PrevStatement is Identifier _identifier2)
            { _identifier2.Token.AnalyzedType = TokenAnalyzedType.FunctionName; }
            anyCall.Reference = compiledFunction;

            if (!Settings.DontOptimize &&
                TryEvaluate(compiledFunction, functionCall.MethodArguments, out CompiledValue? returnValue, out Statement[]? runtimeStatements) &&
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
            anyCall.Reference = functionCall.Reference;
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

        if (anyCall.Arguments.Length != functionType.Parameters.Length)
        { throw new CompilerException($"Wrong number of parameters passed to function {functionType}: required {functionType.Parameters.Length} passed {anyCall.Arguments.Length}", new Position(anyCall.Arguments.As<IPositioned>().Or(anyCall.Brackets)), CurrentFile); }

        AddComment($"Call (runtime) {functionType} {{");

        if (functionType.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            GenerateInitialValue(functionType.ReturnType);
            AddComment($"}}");
        }

        Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForParameterPassing(anyCall.Arguments, functionType);

        AddComment(" .:");

        CallRuntime(anyCall.PrevStatement);

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (functionType.ReturnSomething && !anyCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            for (int i = 0; i < functionType.ReturnType.SizeBytes; i++)
            { Pop(BitWidth._8); }
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(BinaryOperatorCall @operator, GeneralType? expectedType = null)
    {
        if (!Settings.DontOptimize && TryCompute(@operator, out CompiledValue predictedValue))
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

                GenerateCodeForFunctionCall_External(@operator.Arguments, @operator.SaveValue, operatorDefinition, externalFunction);
                return;
            }

            AddComment($"Call {operatorDefinition.Identifier} {{");

            GenerateInitialValue(operatorDefinition.Type);

            Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForArguments(@operator.Arguments, operatorDefinition);

            AddComment(" .:");

            int jumpInstruction = Call(operatorDefinition.InstructionOffset);

            if (operatorDefinition.InstructionOffset == InvalidFunctionAddress)
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition, CurrentFile)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < operatorDefinition.Type.SizeBytes; i++)
                { Pop(BitWidth._8); }
            }

            AddComment("}");
        }
        else if (LanguageOperators.BinaryOperators.Contains(@operator.Operator.Content))
        {
            FindStatementType(@operator);

            GeneralType leftType = FindStatementType(@operator.Left, expectedType);
            GeneralType rightType = FindStatementType(@operator.Right, expectedType);

            BitWidth leftBitWidth = GetBitWidth(leftType);
            BitWidth rightBitWidth = GetBitWidth(rightType);
            BitWidth bitWidth = MaxBitWidth(leftBitWidth, rightBitWidth);

            int jumpInstruction = InvalidFunctionAddress;

            GenerateCodeForStatement(@operator.Left, expectedType);

            if (leftType != BasicType.Float &&
                rightType == BasicType.Float)
            {
                AddInstruction(Opcode.FTo,
                    (InstructionOperand)StackTop,
                    (InstructionOperand)StackTop);
            }

            if (@operator.Operator.Content == BinaryOperatorCall.LogicalAND)
            {
                StackLoad(StackTop, leftBitWidth);

                using (RegisterUsage.Auto regLeft = Registers.GetFree())
                {
                    PopTo(regLeft.Get(leftBitWidth));
                    AddInstruction(Opcode.Compare, regLeft.Get(leftBitWidth), 0);
                    jumpInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JumpIfEqual, 0);
                }
            }
            else if (@operator.Operator.Content == BinaryOperatorCall.LogicalOR)
            {
                StackLoad(StackTop, leftBitWidth);

                using (RegisterUsage.Auto regLeft = Registers.GetFree())
                {
                    PopTo(regLeft.Get(leftBitWidth));
                    AddInstruction(Opcode.Compare, regLeft.Get(leftBitWidth), 0);

                    jumpInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JumpIfNotEqual, 0);
                }
            }

            GenerateCodeForStatement(@operator.Right, expectedType);

            if (leftType == BasicType.Float &&
                rightType != BasicType.Float)
            {
                AddInstruction(Opcode.FTo,
                    (InstructionOperand)StackTop,
                    (InstructionOperand)StackTop);
            }

            using (RegisterUsage.Auto regLeft = Registers.GetFree())
            using (RegisterUsage.Auto regRight = Registers.GetFree())
            {
                bool isFloat = leftType == BasicType.Float || rightType == BasicType.Float;

                PopTo(regRight.Get(bitWidth), rightBitWidth);
                PopTo(regLeft.Get(bitWidth), leftBitWidth);

                switch (@operator.Operator.Content)
                {
                    case BinaryOperatorCall.Addition:
                        AddInstruction(isFloat ? Opcode.FMathAdd : Opcode.MathAdd, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Subtraction:
                        AddInstruction(isFloat ? Opcode.FMathSub : Opcode.MathSub, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Multiplication:
                        AddInstruction(isFloat ? Opcode.FMathMult : Opcode.MathMult, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Division:
                        AddInstruction(isFloat ? Opcode.FMathDiv : Opcode.MathDiv, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Modulo:
                        AddInstruction(isFloat ? Opcode.FMathMod : Opcode.MathMod, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.LogicalAND:
                        AddInstruction(Opcode.LogicAND, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.LogicalOR:
                        AddInstruction(Opcode.LogicOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitwiseAND:
                        AddInstruction(Opcode.BitsAND, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitwiseOR:
                        AddInstruction(Opcode.BitsOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitwiseXOR:
                        AddInstruction(Opcode.BitsXOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitshiftLeft:
                        AddInstruction(Opcode.BitsShiftLeft, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitshiftRight:
                        AddInstruction(Opcode.BitsShiftRight, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;

                    case BinaryOperatorCall.CompEQ:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfEqual, 3);
                        AddInstruction(Opcode.Push, false);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, true);
                        break;

                    case BinaryOperatorCall.CompNEQ:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfNotEqual, 3);
                        AddInstruction(Opcode.Push, false);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, true);
                        break;

                    case BinaryOperatorCall.CompGT:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLessOrEqual, 3);
                        AddInstruction(Opcode.Push, true);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, false);
                        break;

                    case BinaryOperatorCall.CompGEQ:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLess, 3);
                        AddInstruction(Opcode.Push, true);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, false);
                        break;

                    case BinaryOperatorCall.CompLT:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLess, 3);
                        AddInstruction(Opcode.Push, false);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, true);
                        break;

                    case BinaryOperatorCall.CompLEQ:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLessOrEqual, 3);
                        AddInstruction(Opcode.Push, false);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, true);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            if (jumpInstruction != InvalidFunctionAddress)
            { GeneratedCode[jumpInstruction].Operand1 = GeneratedCode.Count - jumpInstruction; }
        }
        else if (@operator.Operator.Content == "=")
        {
            GenerateCodeForValueSetter(@operator.Left, @operator.Right);
        }
        else
        { throw new CompilerException($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, CurrentFile); }
    }
    void GenerateCodeForStatement(UnaryOperatorCall @operator)
    {
        if (!Settings.DontOptimize && TryCompute(@operator, out CompiledValue predictedValue))
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

                GenerateCodeForFunctionCall_External(@operator.Arguments, @operator.SaveValue, operatorDefinition, externalFunction);
                return;
            }

            AddComment($"Call {operatorDefinition.Identifier} {{");

            GenerateInitialValue(operatorDefinition.Type);

            Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForArguments(@operator.Arguments, operatorDefinition);

            AddComment(" .:");

            int jumpInstruction = Call(operatorDefinition.InstructionOffset);

            if (operatorDefinition.InstructionOffset == InvalidFunctionAddress)
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition, CurrentFile)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < operatorDefinition.Type.SizeBytes; i++)
                { Pop(BitWidth._8); }
            }

            AddComment("}");
        }
        else if (LanguageOperators.UnaryOperators.Contains(@operator.Operator.Content))
        {
            GeneralType leftType = FindStatementType(@operator.Left);
            BitWidth bitWidth = GetBitWidth(leftType);

            switch (@operator.Operator.Content)
            {
                case UnaryOperatorCall.LogicalNOT:
                {
                    GenerateCodeForStatement(@operator.Left);

                    using (RegisterUsage.Auto reg = Registers.GetFree())
                    {
                        PopTo(reg.Get(bitWidth));
                        AddInstruction(Opcode.Compare, reg.Get(bitWidth), 0);
                        AddInstruction(Opcode.JumpIfEqual, 3);
                        AddInstruction(Opcode.Push, false);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, true);
                    }

                    return;
                }
                case UnaryOperatorCall.BinaryNOT:
                {
                    GenerateCodeForStatement(@operator.Left);

                    using (RegisterUsage.Auto reg = Registers.GetFree())
                    {
                        PopTo(reg.Get(bitWidth));
                        AddInstruction(Opcode.BitsNOT, reg.Get(bitWidth));
                        AddInstruction(Opcode.Push, reg.Get(bitWidth));
                    }

                    return;
                }
                default: throw new CompilerException($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, CurrentFile);
            }
        }
        else
        { throw new CompilerException($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, CurrentFile); }
    }
    void GenerateCodeForStatement(Assignment setter) => GenerateCodeForValueSetter(setter.Left, setter.Right);
    void GenerateCodeForStatement(LiteralStatement literal, GeneralType? expectedType = null)
    {
        switch (literal.Type)
        {
            case LiteralType.Integer:
            {
                if (expectedType is not null)
                {
                    if (expectedType == BasicType.Byte)
                    {
                        if (literal.GetInt() is >= byte.MinValue and <= byte.MaxValue)
                        {
                            OnGotStatementType(literal, new BuiltinType(BasicType.Byte));
                            AddInstruction(Opcode.Push, new CompiledValue((byte)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType == BasicType.Char)
                    {
                        if (literal.GetInt() is >= ushort.MinValue and <= ushort.MaxValue)
                        {
                            OnGotStatementType(literal, new BuiltinType(BasicType.Char));
                            AddInstruction(Opcode.Push, new CompiledValue((ushort)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType == BasicType.Float)
                    {
                        OnGotStatementType(literal, new BuiltinType(BasicType.Float));
                        AddInstruction(Opcode.Push, new CompiledValue((float)literal.GetInt()));
                        return;
                    }
                }

                OnGotStatementType(literal, new BuiltinType(BasicType.Integer));
                AddInstruction(Opcode.Push, new CompiledValue(literal.GetInt()));
                break;
            }
            case LiteralType.Float:
            {
                OnGotStatementType(literal, new BuiltinType(BasicType.Float));

                AddInstruction(Opcode.Push, new CompiledValue(literal.GetFloat()));
                break;
            }
            case LiteralType.String:
            {
                OnGotStatementType(literal, new PointerType(new BuiltinType(BasicType.Char)));

                GenerateCodeForLiteralString(literal.Value);
                break;
            }
            case LiteralType.Char:
            {
                if (literal.Value.Length != 1) throw new InternalException($"Literal char contains {literal.Value.Length} characters but only 1 allowed", literal, CurrentFile);

                if (expectedType is not null)
                {
                    if (expectedType == BasicType.Byte)
                    {
                        if ((int)literal.Value[0] is >= byte.MinValue and <= byte.MaxValue)
                        {
                            OnGotStatementType(literal, new BuiltinType(BasicType.Byte));
                            AddInstruction(Opcode.Push, new CompiledValue((byte)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType == BasicType.Float)
                    {
                        OnGotStatementType(literal, new BuiltinType(BasicType.Float));
                        AddInstruction(Opcode.Push, new CompiledValue((float)literal.Value[0]));
                        return;
                    }
                }

                OnGotStatementType(literal, new BuiltinType(BasicType.Char));
                AddInstruction(Opcode.Push, new CompiledValue(literal.Value[0]));
                break;
            }
            default: throw new UnreachableException();
        }
    }

    void GenerateCodeForLiteralString(string literal)
    {
        AddComment($"Create String \"{literal}\" {{");

        AddComment("Allocate String object {");

        int charSize = new BuiltinType(BasicType.Char).SizeBytes;

        GenerateAllocator(Literal.CreateAnonymous((1 + literal.Length) * charSize, Position.UnknownPosition));

        AddComment("}");

        AddComment("Set string data {");

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            // Save pointer
            AddInstruction(Opcode.Move, reg.Get(BitWidth._32), (InstructionOperand)StackTop);

            for (int i = 0; i < literal.Length; i++)
            {
                AddInstruction(Opcode.Move, reg.Get(BitWidth._32).ToPtr(i * charSize, BitWidth._16), literal[i]);
            }

            AddInstruction(Opcode.Move, reg.Get(BitWidth._32).ToPtr(literal.Length * charSize, BitWidth._16), '\0');
        }

        AddComment("}");

        AddComment("}");
    }

    void GenerateCodeForStatement(Identifier variable, GeneralType? expectedType = null, bool resolveReference = true)
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

            Address address = GetParameterAddress(param);

            if (param.IsRef && resolveReference)
            { address = new AddressRuntimePointer(address); }

            StackLoad(address, param.Type.SizeBytes);

            return;
        }

        if (GetVariable(variable.Content, out CompiledVariable? val))
        {
            variable.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = val;
            OnGotStatementType(variable, val.Type);

            StackLoad(GetLocalVariableAddress(val), val.Type.SizeBytes);
            return;
        }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariable? globalVariable, out _))
        {
            variable.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = globalVariable;
            OnGotStatementType(variable, globalVariable.Type);

            StackLoad(GetGlobalVariableAddress(globalVariable), globalVariable.Type.SizeBytes);
            return;
        }

        if (GetFunction(variable.Token.Content, expectedType, out FunctionQueryResult<CompiledFunction>? result, out _))
        {
            CompiledFunction? compiledFunction = result.Function;

            compiledFunction.References.Add((variable, CurrentFile, CurrentContext));
            variable.Token.AnalyzedType = TokenAnalyzedType.FunctionName;
            variable.Reference = compiledFunction;
            OnGotStatementType(variable, new FunctionType(compiledFunction));

            if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
            { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(GeneratedCode.Count, true, variable, compiledFunction, CurrentFile)); }

            AddInstruction(Opcode.Push, compiledFunction.InstructionOffset);

            return;
        }

        throw new CompilerException($"Symbol \"{variable.Content}\" not found", variable, CurrentFile);
    }
    void GenerateCodeForStatement(AddressGetter addressGetter)
    {
        StatementWithValue statement = addressGetter.PrevStatement;
        Address address = GetDataAddress(statement);
        GenerateAddressResolver(address);
    }
    void GenerateCodeForStatement(Pointer pointer)
    {
        GenerateCodeForStatement(pointer.PrevStatement);

        GeneralType addressType = FindStatementType(pointer.PrevStatement);
        if (addressType is not PointerType pointerType)
        { throw new CompilerException($"This isn't a pointer", pointer.PrevStatement, CurrentFile); }

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(pointerType.BitWidth));
            StackLoad(new AddressRegisterPointer(reg.Get(pointerType.BitWidth)), pointerType.To.SizeBytes);
        }
    }
    void GenerateCodeForStatement(WhileLoop whileLoop)
    {
        if (!Settings.DontOptimize &&
            TryCompute(whileLoop.Condition, out CompiledValue condition))
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

        GeneralType conditionType = FindStatementType(whileLoop.Condition);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(conditionType.BitWidth));
            AddInstruction(Opcode.Compare, reg.Get(conditionType.BitWidth), 0);
            AddInstruction(Opcode.JumpIfEqual, 0);
        }
        int conditionJumpOffset = GeneratedCode.Count - 1;

        BreakInstructions.Push(new List<int>());

        GenerateCodeForStatement(whileLoop.Block, true);

        AddComment("Jump Back");
        AddInstruction(Opcode.Jump, conditionOffset - GeneratedCode.Count);

        FinishJumpInstructions(BreakInstructions.Last);

        GeneratedCode[conditionJumpOffset].Operand1 = GeneratedCode.Count - conditionJumpOffset;

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
            TryCompute(forLoop.Condition, out CompiledValue condition))
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

        GeneralType conditionType = FindStatementType(forLoop.Condition);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(conditionType.BitWidth));
            AddInstruction(Opcode.Compare, reg.Get(conditionType.BitWidth), 0);
            AddInstruction(Opcode.JumpIfEqual, 0);
        }

        int conditionJumpOffsetFor = GeneratedCode.Count - 1;

        BreakInstructions.Push(new List<int>());

        GenerateCodeForStatement(forLoop.Block);

        AddComment("For-loop expression");
        GenerateCodeForStatement(forLoop.Expression);

        AddComment("Jump back");
        AddInstruction(Opcode.Jump, conditionOffsetFor - GeneratedCode.Count);
        GeneratedCode[conditionJumpOffsetFor].Operand1 = GeneratedCode.Count - conditionJumpOffsetFor;

        FinishJumpInstructions(BreakInstructions.Pop());

        OnScopeExit(forLoop.Position.After());

        AddComment("}");
    }
    void GenerateCodeForStatement(IfContainer @if)
    {
        List<int> jumpOutInstructions = new();

        foreach (BaseBranch ifSegment in @if.Branches)
        {
            if (ifSegment is IfBranch partIf)
            {
                if (!Settings.DontOptimize &&
                    TryCompute(partIf.Condition, out CompiledValue condition))
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

                GeneralType conditionType = FindStatementType(partIf.Condition);

                AddComment("If jump-to-next");
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(conditionType.BitWidth));
                    AddInstruction(Opcode.Compare, reg.Get(conditionType.BitWidth), 0);
                    AddInstruction(Opcode.JumpIfEqual, 0);
                }
                int jumpNextInstruction = GeneratedCode.Count - 1;

                GenerateCodeForStatement(partIf.Block);

                AddComment("If jump-to-end");
                jumpOutInstructions.Add(GeneratedCode.Count);
                AddInstruction(Opcode.Jump, 0);

                AddComment("}");

                GeneratedCode[jumpNextInstruction].Operand1 = GeneratedCode.Count - jumpNextInstruction;
            }
            else if (ifSegment is ElseIfBranch partElseif)
            {
                if (!Settings.DontOptimize &&
                    TryCompute(partElseif.Condition, out CompiledValue condition))
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

                GeneralType conditionType = FindStatementType(partElseif.Condition);

                AddComment("Elseif jump-to-next");
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(conditionType.BitWidth));
                    AddInstruction(Opcode.Compare, reg.Get(conditionType.BitWidth), 0);
                    AddInstruction(Opcode.JumpIfEqual, 0);
                }
                int jumpNextInstruction = GeneratedCode.Count - 1;

                GenerateCodeForStatement(partElseif.Block);

                AddComment("Elseif jump-to-end");
                jumpOutInstructions.Add(GeneratedCode.Count);
                AddInstruction(Opcode.Jump, 0);

                AddComment("}");

                GeneratedCode[jumpNextInstruction].Operand1 = GeneratedCode.Count - jumpNextInstruction;
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
            GeneratedCode[item].Operand1 = GeneratedCode.Count - item;
        }
    }
    void GenerateCodeForStatement(NewInstance newObject)
    {
        AddComment($"new {newObject.Type} {{");

        GeneralType instanceType = GeneralType.From(newObject.Type, FindType, TryCompute);

        newObject.Type.SetAnalyzedType(instanceType);
        OnGotStatementType(newObject, instanceType);

        switch (instanceType)
        {
            case PointerType pointerType:
            {
                GenerateAllocator(Literal.CreateAnonymous(pointerType.To.SizeBytes, newObject.Type));

                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.Move, reg.Get(BitWidth._32), (InstructionOperand)StackTop);

                    for (int offset = 0; offset < pointerType.To.SizeBytes; offset++)
                    { AddInstruction(Opcode.Move, reg.Get(BitWidth._32).ToPtr(offset, BitWidth._8), new InstructionOperand((byte)0)); }
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
        ImmutableArray<GeneralType> parameters = FindStatementTypes(constructorCall.Arguments);

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

        AddComment(" Pass arguments (\"this\" is already passed):");

        parameterCleanup = GenerateCodeForArguments(constructorCall.Arguments, compiledFunction, 1);

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset);

        if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
        { UndefinedConstructorOffsets.Add(new UndefinedOffset<CompiledConstructor>(jumpInstruction, false, constructorCall, compiledFunction, CurrentFile)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }
    void GenerateCodeForStatement(Field field)
    {
        field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        GeneralType prevType = FindStatementType(field.PrevStatement);

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

            if (!structPointerType.GetField(field.Identifier.Content, true, out CompiledField? fieldDefinition, out int fieldOffset))
            { throw new InternalException($"Field \"{field.Identifier}\" not found", field.Identifier, CurrentFile); }

            field.CompiledType = fieldDefinition.Type;
            field.Reference = fieldDefinition;
            fieldDefinition.References.Add(new Reference<Statement>(field, CurrentFile, CurrentContext));

            GenerateCodeForStatement(field.PrevStatement);

            CheckPointerNull();

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(pointerType.BitWidth));
                StackLoad(new AddressOffset(
                    new AddressRegisterPointer(reg.Get(pointerType.BitWidth)),
                    fieldOffset
                    ), fieldDefinition.Type.SizeBytes);
            }

            return;
        }

        if (prevType is not StructType structType) throw new NotImplementedException();

        GeneralType type = FindStatementType(field);

        if (!structType.GetField(field.Identifier.Content, true, out CompiledField? compiledField, out _))
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

        StatementWithValue? dereference = NeedDerefernce(field);

        if (dereference is null)
        {
            Address offset = GetDataAddress(field);
            StackLoad(offset, compiledField.Type.SizeBytes);
        }
        else
        {
            int offset = GetDataOffset(field, dereference);
            HeapLoad(dereference, offset, type.SizeBytes);
        }
    }
    void GenerateCodeForStatement(IndexCall index)
    {
        GeneralType prevType = FindStatementType(index.PrevStatement);

        if (prevType is ArrayType arrayType)
        {
            if (index.PrevStatement is not Identifier identifier)
            { throw new NotSupportedException($"Only variables/parameters supported by now", index.PrevStatement, CurrentFile); }

            if (TryCompute(index.Index, out CompiledValue computedIndexData))
            {
                index.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || computedIndexData >= arrayType.Length)
                { AnalysisCollection?.Warnings.Add(new Warning($"Index out of range", index.Index, CurrentFile)); }

                if (GetParameter(identifier.Content, out CompiledParameter? param))
                {
                    if (param.Type != arrayType)
                    { throw new NotImplementedException(); }

                    Address offset = GetParameterAddress(param, (int)computedIndexData * arrayType.Of.SizeBytes);
                    StackLoad(offset, arrayType.Of.SizeBytes);

                    throw new NotImplementedException();
                }

                if (GetVariable(identifier.Content, out CompiledVariable? val))
                {
                    if (val.Type != arrayType)
                    { throw new NotImplementedException(); }

                    int offset = (int)computedIndexData * arrayType.Of.SizeBytes;
                    Address address = GetLocalVariableAddress(val);

                    StackLoad(new AddressOffset(address, offset), arrayType.Of.SizeBytes);

                    return;
                }
            }

            {
                using (RegisterUsage.Auto regPtr = Registers.GetFree())
                {
                    Address address = GetDataAddress(identifier);
                    GenerateAddressResolver(address);
                    PopTo(regPtr.Get(BitWidth._32));

                    GenerateCodeForStatement(index.Index);

                    GeneralType indexType = FindStatementType(index.Index);

                    if (indexType is not BuiltinType builtinType)
                    { throw new CompilerException($"Index must be a builtin type (i.e. int) and not {indexType}", index.Index, CurrentFile); }

                    using (RegisterUsage.Auto regIndex = Registers.GetFree())
                    {
                        PopTo(regIndex.Get(builtinType.BitWidth));
                        AddInstruction(Opcode.MathMult, regIndex.Get(builtinType.BitWidth), arrayType.Of.SizeBytes);
                        AddInstruction(Opcode.MathAdd, regPtr.Register, regIndex.Get(builtinType.BitWidth));
                    }

                    StackLoad(new AddressRegisterPointer(regPtr.Register), arrayType.Of.SizeBytes);
                }
                return;
            }

            throw new NotImplementedException();
        }

        if (!GetIndexGetter(prevType, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out _, AddCompilable))
        {
            if (prevType is not PointerType pointerType)
            { throw new CompilerException($"Index getter \"{prevType}[]\" not found", index, CurrentFile); }

            GenerateCodeForStatement(index.PrevStatement);

            GeneralType indexType = FindStatementType(index.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", index.Index, CurrentFile); }
            GenerateCodeForStatement(index.Index);

            using (RegisterUsage.Auto reg1 = Registers.GetFree())
            {
                PopTo(reg1.Register);
                AddInstruction(Opcode.MathMult, reg1.Register, pointerType.To.SizeBytes);

                using (RegisterUsage.Auto reg2 = Registers.GetFree())
                {
                    PopTo(reg2.Register);
                    AddInstruction(Opcode.MathAdd, reg1.Register, reg2.Register);
                }

                AddInstruction(Opcode.Push, reg1.Register);

                CheckPointerNull();

                PopTo(reg1.Register);

                for (int i = pointerType.To.SizeBytes - 1; i >= 0; i--)
                { AddInstruction(Opcode.Push, reg1.Register.ToPtr(i, BitWidth._8)); }
            }

            return;
        }

        GenerateCodeForFunctionCall_Function(new FunctionCall(
                index.PrevStatement,
                Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerGet),
                ImmutableArray.Create(index.Index),
                index.Brackets,
                index.File
            ), result.Function);
    }

    void GenerateAddressResolver(Address address)
    {
        switch (address)
        {
            case AddressRuntimePointer runtimePointer:
                GenerateAddressResolver(runtimePointer.PointerAddress);
                break;
            case AddressRegisterPointer registerPointer:
                AddInstruction(Opcode.Push, registerPointer.Register);
                break;
            case AddressOffset addressOffset:
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    GenerateAddressResolver(addressOffset.Base);
                    AddInstruction(Opcode.PopTo32, reg.Get(BitWidth._32));
                    AddInstruction(Opcode.MathAdd, reg.Get(BitWidth._32), addressOffset.Offset);
                    AddInstruction(Opcode.Push, reg.Get(BitWidth._32));
                }
                break;
            default: throw new NotImplementedException();
        }
    }

    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Equals(ModifierKeywords.Ref))
        {
            throw null!;
            Address address = GetDataAddress(statement);

            // if (address is AddressRuntimePointer runtimePointer)
            // {
            //     StackLoad(runtimePointer.PointerAddress, BitWidth._32);
            // }
            // else
            {
                GenerateAddressResolver(address);
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
    [DoesNotReturn]
    void GenerateCodeForStatement(LiteralList listValue) => throw new NotImplementedException();
    void GenerateCodeForStatement(TypeCast typeCast)
    {
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = GeneralType.From(typeCast.Type, FindType, TryCompute);
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (!Settings.DontOptimize &&
            targetType is BuiltinType targetBuiltinType &&
            TryComputeSimple(typeCast.PrevStatement, out CompiledValue prevValue) &&
            CompiledValue.TryCast(ref prevValue, targetBuiltinType.RuntimeType))
        {
            AddInstruction(Opcode.Push, prevValue);
            return;
        }

        if (statementType.SizeBytes != targetType.SizeBytes)
        {
            if (statementType.SizeBytes < targetType.SizeBytes)
            {
                AddComment($"Shrink {statementType} ({statementType.SizeBytes} bytes) to {targetType} ({targetType.SizeBytes}) {{");

                AddComment("Make space");

                for (int i = 0; i < targetType.SizeBytes; i++)
                { AddInstruction(Opcode.Push, new CompiledValue((byte)0)); }

                AddComment("Value");

                GenerateCodeForStatement(typeCast.PrevStatement);

                AddComment("Save");

                for (int i = 0; i < statementType.SizeBytes; i++)
                { PopTo(Register.StackPointer.ToPtr((statementType.SizeBytes - 1) * -BytecodeProcessor.StackDirection, BitWidth._8), BitWidth._8); }

                AddComment("}");

                return;
            }
            else if (statementType.SizeBytes > targetType.SizeBytes)
            {
                AddComment($"Shrink {statementType} ({statementType.SizeBytes} bytes) to {targetType} ({targetType.SizeBytes}) {{");

                AddComment("Make space");

                for (int i = 0; i < targetType.SizeBytes; i++)
                { AddInstruction(Opcode.Push, new CompiledValue((byte)0)); }

                AddComment("Value");

                GenerateCodeForStatement(typeCast.PrevStatement);

                AddComment("Save");

                for (int i = 0; i < targetType.SizeBytes; i++)
                { PopTo(Register.StackPointer.ToPtr((statementType.SizeBytes - 1) * -BytecodeProcessor.StackDirection, BitWidth._8), BitWidth._8); }

                AddComment("Discard excess");

                int excess = statementType.SizeBytes - targetType.SizeBytes;
                for (int i = 0; i < excess; i++)
                { AddInstruction(Opcode.Pop8); }

                AddComment("}");

                return;
            }

            throw new CompilerException($"Can't modify the size of the value. You tried to convert from {statementType} (size of {statementType.SizeBytes}) to {targetType} (size of {targetType.SizeBytes})", new Position(typeCast.Keyword, typeCast.Type), CurrentFile);
        }

        GenerateCodeForStatement(typeCast.PrevStatement, targetType);

        GeneralType type = FindStatementType(typeCast.PrevStatement, targetType);

        if (targetType is not FunctionType && type == targetType)
        {
            AnalysisCollection?.Hints.Add(new Hint($"Redundant type conversion", typeCast.Keyword, CurrentFile));
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

    void GenerateCodeForStatement(Statement statement, GeneralType? expectedType = null, bool resolveReference = true)
    {
        int startInstruction = GeneratedCode.Count;

        switch (statement)
        {
            case LiteralList v: GenerateCodeForStatement(v); break;
            case VariableDeclaration v: GenerateCodeForStatement(v); break;
            case KeywordCall v: GenerateCodeForStatement(v); break;
            case BinaryOperatorCall v: GenerateCodeForStatement(v, expectedType); break;
            case UnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case AnyAssignment v: GenerateCodeForStatement(v.ToAssignment()); break;
            case LiteralStatement v: GenerateCodeForStatement(v, expectedType); break;
            case Identifier v: GenerateCodeForStatement(v, expectedType, resolveReference); break;
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
                if (item.Type is null) throw new InternalException();
                GenerateDestructor(item.Type, position);
            }
            else
            {
                for (int x = 0; x < item.SizeOnStack; x++)
                { Pop(BitWidth._8); }
            }

            CompiledVariables.Pop();
        }
    }

    void CleanupGlobalVariables(ImmutableArray<CleanupItem> cleanupItems, Position position)
    {
        if (cleanupItems.Length == 0) return;
        AddComment("Clear Global Variables");
        for (int i = cleanupItems.Length - 1; i >= 0; i--)
        {
            CleanupItem item = cleanupItems[i];

            if (item.ShouldDeallocate)
            {
                if (item.SizeOnStack != BytecodeProcessor.PointerSize) throw new InternalException();
                if (item.Type is null) throw new InternalException();
                GenerateDestructor(item.Type, position);
            }
            else
            {
                for (int x = 0; x < item.SizeOnStack; x++)
                { Pop(BitWidth._8); }
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

            GenerateCodeForStatement(value, parameter.Type);

            Address address = GetParameterAddress(parameter);

            if (parameter.IsRef)
            { address = new AddressRuntimePointer(address); }

            StackStore(address, parameter.Type.SizeBytes);
        }
        else if (GetVariable(statementToSet.Content, out CompiledVariable? variable))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = variable.Type;
            statementToSet.Reference = variable;

            GeneralType valueType = FindStatementType(value, variable.Type);

            AssignTypeCheck(variable.Type, valueType, value);

            GenerateCodeForStatement(value, variable.Type);

            StackStore(GetLocalVariableAddress(variable), variable.Type.SizeBytes);
        }
        else if (GetGlobalVariable(statementToSet.Content, statementToSet.File, out CompiledVariable? globalVariable, out _))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = globalVariable.Type;
            statementToSet.Reference = globalVariable;

            GeneralType valueType = FindStatementType(value, globalVariable.Type);

            AssignTypeCheck(globalVariable.Type, valueType, value);

            GenerateCodeForStatement(value, globalVariable.Type);

            StackStore(GetGlobalVariableAddress(globalVariable), globalVariable.Type.SizeBytes);
        }
        else
        {
            throw new CompilerException($"Symbol \"{statementToSet.Content}\" not found", statementToSet, CurrentFile);
        }
    }
    void GenerateCodeForValueSetter(Field statementToSet, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType type = FindStatementType(statementToSet);
        GeneralType valueType = FindStatementType(value, type);

        if (prevType is PointerType pointerType)
        {
            if (pointerType.To is not StructType structType)
            { throw new CompilerException($"Failed to get the field offsets of type {pointerType.To}", statementToSet.PrevStatement, CurrentFile); }

            if (!structType.GetField(statementToSet.Identifier.Content, true, out CompiledField? fieldDefinition, out int fieldOffset))
            { throw new CompilerException($"Failed to get the field offset for \"{statementToSet.Identifier}\" in type {pointerType.To}", statementToSet.Identifier, CurrentFile); }

            statementToSet.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;
            statementToSet.Reference = fieldDefinition;
            statementToSet.CompiledType = statementToSet.CompiledType;
            fieldDefinition.References.Add(new Reference<Statement>(statementToSet, CurrentFile, CurrentContext));

            GenerateCodeForStatement(value, type);
            HeapStore(statementToSet.PrevStatement, fieldOffset, valueType.SizeBytes);
            return;
        }

        if (prevType is not StructType)
        { throw new NotImplementedException(); }

        if (type != valueType)
        { throw new CompilerException($"Can not set a \"{valueType}\" type value to the \"{type}\" type field.", value, CurrentFile); }

        GenerateCodeForStatement(value, type);

        StatementWithValue? dereference = NeedDerefernce(statementToSet);

        if (dereference is null)
        {
            Address offset = GetDataAddress(statementToSet);
            StackStore(offset, valueType.SizeBytes);
        }
        else
        {
            int offset = GetDataOffset(statementToSet, dereference);
            HeapStore(dereference, offset, valueType.SizeBytes);
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

            if (TryCompute(statementToSet.Index, out CompiledValue computedIndexData))
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

                    int offset = (int)computedIndexData * arrayType.Of.SizeBytes;
                    StackStore(new AddressOffset(GetLocalVariableAddress(variable), offset), arrayType.Of.SizeBytes);
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

            GeneralType indexType = FindStatementType(statementToSet.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", statementToSet.Index, CurrentFile); }
            GenerateCodeForStatement(statementToSet.Index);

            using (RegisterUsage.Auto regIndex = Registers.GetFree())
            {
                PopTo(regIndex.Get(indexType.BitWidth));
                AddInstruction(Opcode.MathMult, regIndex.Get(indexType.BitWidth), pointerType.To.SizeBytes);

                using (RegisterUsage.Auto regPtr = Registers.GetFree())
                {
                    PopTo(regPtr.Get(BitWidth._32));

                    AddInstruction(Opcode.MathAdd, regPtr.Get(BitWidth._32), regIndex.Get(indexType.BitWidth));

                    AddInstruction(Opcode.Push, regPtr.Get(BitWidth._32));

                    CheckPointerNull(false);

                    for (int i = 0; i < valueType.SizeBytes; i++)
                    { AddInstruction(Opcode.PopTo8, regPtr.Get(BitWidth._32).ToPtr(i, BitWidth._8)); }
                }
            }

            return;
        }

        GenerateCodeForFunctionCall_Function(new FunctionCall(
            statementToSet.PrevStatement,
            Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerSet),
            ImmutableArray.Create<StatementWithValue>(statementToSet.Index, value),
            statementToSet.Brackets,
            statementToSet.File
        ), result.Function);
    }
    void GenerateCodeForValueSetter(Pointer statementToSet, StatementWithValue value)
    {
        GeneralType targetType = FindStatementType(statementToSet);
        GeneralType valueType = FindStatementType(value, targetType);

        AssignTypeCheck(targetType, valueType, value);

        GenerateCodeForStatement(value, targetType);

        GenerateCodeForStatement(statementToSet.PrevStatement);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(BitWidth._32));
            for (int i = 0; i < valueType.SizeBytes; i++)
            { PopTo(reg.Get(BitWidth._32).ToPtr(i, BitWidth._8), BitWidth._8); }
        }
    }

    #endregion

    void OnScopeEnter(Block block) => OnScopeEnter(block.Position, block.Statements.OfType<VariableDeclaration>());

    void OnScopeEnter(Position position, IEnumerable<VariableDeclaration> variables)
    {
        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                SourcePosition = position,
                Uri = CurrentFile,
            },
            Stack = new List<StackElementInformation>(),
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
            StackLoad(ReturnFlagAddress, ReturnFlagType.BitWidth);

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(ReturnFlagType.BitWidth), ReturnFlagType.BitWidth);
                AddInstruction(Opcode.Compare, reg.Get(ReturnFlagType.BitWidth), 0);
                ReturnInstructions.Last.Add(GeneratedCode.Count);
                AddInstruction(Opcode.JumpIfNotEqual, 0);
            }
        }

        CleanupLocalConstants();

        ScopeInformation scope = CurrentScopeDebug.Pop();
        scope.Location.Instructions.End = GeneratedCode.Count - 1;
        DebugInfo?.ScopeInformation.Add(scope);
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

        GeneralType type;
        if (newVariable.Type == StatementKeywords.Var)
        {
            if (newVariable.InitialValue == null)
            { throw new CompilerException($"Initial value for variable declaration with implicit type is required", newVariable, newVariable.File); }

            type = FindStatementType(newVariable.InitialValue);
        }
        else
        {
            type = GeneralType.From(newVariable.Type, FindType, TryCompute);

            newVariable.Type.SetAnalyzedType(type);
            newVariable.CompiledType = type;
        }

        int offset = (VariablesSize + ReturnFlagType.SizeBytes + type.SizeBytes) * BytecodeProcessor.StackDirection;

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        StackElementInformation debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Tag = compiledVariable.Identifier.Content,
            Address = offset,
            BasePointerRelative = true,
            Size = compiledVariable.Type.SizeBytes,
            Type = compiledVariable.Type
        };
        newVariable.CompiledType = compiledVariable.Type;

        CurrentScopeDebug.Last.Stack.Add(debugInfo);

        CompiledVariables.Add(compiledVariable);

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        int size;

        if (!Settings.DontOptimize &&
            TryCompute(newVariable.InitialValue, out CompiledValue computedInitialValue))
        {
            if (computedInitialValue.TryCast(compiledVariable.Type, out CompiledValue castedInitialValue))
            { computedInitialValue = castedInitialValue; }

            newVariable.InitialValue.PredictedValue = computedInitialValue;

            AddComment($"Initial value {{");

            size = new BuiltinType(computedInitialValue.Type).SizeBytes;

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
            size = arrayType.SizeBytes;
            compiledVariable.IsInitialized = true;

            for (int i = 0; i < literalStatement.Value.Length; i++)
            {
                AddInstruction(Opcode.Push, new CompiledValue(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = compiledVariable.Type.SizeBytes;
            GenerateInitialValue(compiledVariable.Type);

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }

        if (size != compiledVariable.Type.SizeBytes)
        { throw new InternalException($"Variable size ({compiledVariable.Type.SizeBytes}) and initial value size ({size}) mismatch"); }

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
    /*

    CleanupItem GenerateCodeForGlobalVariable(VariableDeclaration newVariable)
    {
        if (newVariable.Modifiers.Contains(ModifierKeywords.Const)) return CleanupItem.Null;

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        // TODO: handle tags, originally TagCount.LastOrDefault
        int offset = GlobalVariablesSize + 1; // 1 = Stack pointer offset (???)

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        newVariable.CompiledType = compiledVariable.Type;

        CompiledGlobalVariables.Add(compiledVariable);

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        int size;

        if (!Settings.DontOptimize &&
            TryCompute(newVariable.InitialValue, out CompiledValue computedInitialValue))
        {
            newVariable.InitialValue.PredictedValue = computedInitialValue;

            AddComment($"Initial value {{");

            size = new BuiltinType(computedInitialValue.Type).SizeBytes;

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
            size = arrayType.SizeBytes;
            compiledVariable.IsInitialized = true;

            for (int i = 0; i < literalStatement.Value.Length; i++)
            {
                AddInstruction(Opcode.Push, new CompiledValue(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = compiledVariable.Type.SizeBytes;
            GenerateInitialValue(compiledVariable.Type);

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }

        if (size != compiledVariable.Type.SizeBytes)
        { throw new InternalException($"Variable size ({compiledVariable.Type.SizeBytes}) and initial value size ({size}) mismatch"); }

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

    */
    #endregion

    #region GenerateCodeFor...

    int VariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariable variable in CompiledVariables)
            { sum += variable.Type.SizeBytes; }
            return sum;
        }
    }

    int GlobalVariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariable variable in CompiledGlobalVariables)
            { sum += variable.Type.SizeBytes; }
            return sum;
        }
    }

    void FinishJumpInstructions(IEnumerable<int> jumpInstructions)
        => FinishJumpInstructions(jumpInstructions, GeneratedCode.Count);
    void FinishJumpInstructions(IEnumerable<int> jumpInstructions, int jumpTo)
    {
        foreach (int jumpInstruction in jumpInstructions)
        {
            GeneratedCode[jumpInstruction].Operand1 = jumpTo - jumpInstruction;
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

        CompiledParameters.Clear();
        CompiledVariables.Clear();
        ReturnInstructions.Clear();

        CompileParameters(function.Parameters);

        CurrentFile = function.File;

        int instructionStart = GeneratedCode.Count;

        if (function is IHaveCompiledType functionWithType)
        { CurrentReturnType = functionWithType.Type; }
        else
        { CurrentReturnType = new BuiltinType(BasicType.Void); }

        AddComment("Return flag");
        AddInstruction(Opcode.Push, new CompiledValue(false));

        OnScopeEnter(function.Block ?? throw new CompilerException($"Function \"{function.ToReadable()}\" does not have a body", function, function.File));

        if (function is IHaveCompiledType returnType && returnType.Type != BasicType.Void)
        {
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = GetReturnValueAddress(returnType.Type).Offset,
                BasePointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = returnType.Type.SizeBytes,
                Tag = "Return Value",
                Type = returnType.Type,
            });
        }

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ScaledReturnFlagOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = ReturnFlagType.SizeBytes,
            Tag = "Return Flag",
            Type = ReturnFlagType,
        });

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ScaledSavedBasePointerOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = BasePointerSize,
            Tag = "Saved BasePointer",
            Type = BasePointerType,
        });

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ScaledSavedCodePointerOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = CodePointerSize,
            Tag = "Saved CodePointer",
            Type = CodePointerType,
        });

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ScaledAbsoluteGlobalOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = AbsGlobalAddressSize,
            Tag = "Absolute Global Offset",
            Type = AbsGlobalAddressType,
        });

        for (int i = 0; i < CompiledParameters.Count; i++)
        {
            CompiledParameter p = CompiledParameters[i];

            StackElementInformation debugInfo = new()
            {
                Address = GetParameterAddress(p).Offset,
                Kind = StackElementKind.Parameter,
                BasePointerRelative = true,
                Size = p.IsRef ? BytecodeProcessor.PointerSize : p.Type.SizeBytes,
                Tag = p.Identifier.Content,
                Type = p.IsRef ? new PointerType(p.Type) : p.Type,
            };
            CurrentScopeDebug.Last.Stack.Add(debugInfo);
        }

        AddComment("Statements {");
        for (int i = 0; i < function.Block.Statements.Length; i++)
        { GenerateCodeForStatement(function.Block.Statements[i]); }
        AddComment("}");

        CurrentFile = null;

        CurrentReturnType = null;

        OnScopeExit(function.Block.Brackets.End.Position);

        AddComment("Pop return flag");
        Pop(ReturnFlagType.BitWidth);

        AddComment("Return");
        Return();

        if (function.Block != null && function.Block.Statements.Length > 0) AddComment("}");

        if (function.Identifier is not null)
        {
            DebugInfo?.FunctionInformation.Add(new FunctionInformation()
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
                        if (!function.IsExported) continue;
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
        for (int i = 0; i < parameters.Length; i++)
        {
            GeneralType parameterType = GeneralType.From(parameters[i].Type, FindType);
            parameters[i].Type.SetAnalyzedType(parameterType);

            CompiledParameters.Add(new CompiledParameter(i, parameterType, parameters[i]));
        }
    }

    void GenerateCodeForTopLevelStatements(ImmutableArray<Statement> statements)
    {
        if (statements.IsDefaultOrEmpty) return;

        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                SourcePosition = new Position(statements),
                Uri = CurrentFile,
            },
            Stack = new List<StackElementInformation>(),
        });

        AddComment("TopLevelStatements {");

        AddComment("Create stack frame");
        AddInstruction(Opcode.Push, Register.BasePointer);
        AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ScaledSavedBasePointerOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = BasePointerSize,
            Tag = "Saved BasePointer",
            Type = BasePointerType,
        });

        CurrentContext = null;
        ReturnInstructions.Push(new List<int>());

        CurrentReturnType = ExitCodeType;

        AddComment("Return flag");
        AddInstruction(Opcode.Push, new CompiledValue(false));
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ScaledReturnFlagOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = ReturnFlagType.SizeBytes,
            Tag = "Return Flag",
            Type = ReturnFlagType,
        });

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

        CurrentReturnType = null;
        AddComment("Pop return flag");
        Pop(ReturnFlagType.BitWidth);

        AddComment("Pop stack frame");
        PopTo(Register.BasePointer);

        AddComment("}");

        ScopeInformation scope = CurrentScopeDebug.Pop();
        scope.Location.Instructions.End = GeneratedCode.Count - 1;
        DebugInfo?.ScopeInformation.Add(scope);
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

        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                SourcePosition = Position.UnknownPosition,
                Uri = CurrentFile,
            },
            Stack = new List<StackElementInformation>(),
        });

        {
            // Exit code

            AddComment("Push exit code");
            AddInstruction(Opcode.Push, new CompiledValue(0));

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = (ExitCodeType.SizeBytes - 1) * BytecodeProcessor.StackDirection,
                BasePointerRelative = false,
                Kind = StackElementKind.Internal,
                Size = ExitCodeType.SizeBytes,
                Tag = "Exit Code",
                Type = ExitCodeType,
            });
        }

        {
            // Absolute global offset

            AddComment("Abs global address");

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                AddInstruction(Opcode.Push, Register.StackPointer);
            }

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = ScaledAbsoluteGlobalOffset,
                BasePointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = AbsGlobalAddressSize,
                Tag = "Absolute Global Offset",
                Type = AbsGlobalAddressType,
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

        AddComment("Pop abs global address");
        Pop(AbsGlobalAddressType.BitWidth); // Pop abs global offset

        AddInstruction(Opcode.Exit); // Exit code already there

        foreach (CompiledVariable variable in CompiledGlobalVariables)
        {
            CurrentScopeDebug.LastRef.Stack.Add(new StackElementInformation()
            {
                Address = GetGlobalVariableAddress(variable).Offset,
                BasePointerRelative = false,
                Kind = StackElementKind.Variable,
                Size = variable.Type.SizeBytes,
                Tag = variable.Identifier.Content,
                Type = variable.Type,
            });
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

        {
            ScopeInformation scope = CurrentScopeDebug.Pop();
            scope.Location.Instructions.End = GeneratedCode.Count - 1;
            DebugInfo?.ScopeInformation.Add(scope);
        }

        Print?.Invoke("Code generated", LogType.Debug);

        return new BBLangGeneratorResult()
        {
            Code = GeneratedCode.Select(v => new Instruction(v)).ToImmutableArray(),
            DebugInfo = DebugInfo,
        };
    }
}
