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
            for (int i = 0; i < deallocator.Type.Size; i++)
            { AddInstruction(Opcode.Pop); }
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

        Stack<ParameterCleanupItem> parameterCleanup = new() { new(deallocateableType.Size, false, deallocateableType, position) };

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset);

        if (deallocator.InstructionOffset == InvalidFunctionAddress)
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
            for (int i = 0; i < deallocateableType.Size; i++)
            { AddInstruction(Opcode.Pop); }
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

            if (keywordCall.Arguments.Length == 1)
            {
                AddComment(" Param 0:");

                StatementWithValue returnValue = keywordCall.Arguments[0];
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

                    StackStore(new ValueAddress(AbsoluteGlobalOffset, AddressingMode.PointerBP, true));
                }
            }

            AddComment(" .:");

            if (CanReturn)
            {
                AddInstruction(Opcode.Push, new CompiledValue(true));
                StackStore(ReturnFlagAddress);
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

            GenerateCodeForStatement(throwValue);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                AddInstruction(Opcode.PopTo, reg.Register);
                AddInstruction(Opcode.Throw, reg.Register);
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

            if (argumentType.Size != parameterType.Size)
            { throw new InternalException($"Bad argument type passed: expected {parameterType} passed {argumentType}"); }

            AddComment($" Pass {parameter}:");

            bool canDeallocate = parameter.Modifiers.Contains(ModifierKeywords.Temp);

            canDeallocate = canDeallocate && argumentType is PointerType;

            if (StatementCanBeDeallocated(argument, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", argument, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", argument, CurrentFile)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(argument, parameterType);

            argumentCleanup.Push((argumentType.Size, canDeallocate, argumentType, argument.Position));
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

        if (compiledFunction.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            GenerateInitialValue(compiledFunction.Type);
            AddComment($"}}");
        }

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
                StackStore(new ValueAddress(returnValueOffset, AddressingMode.PointerBP), compiledFunction.Type.Size);
            }
            else
            {
                AddComment($" Clear return value:");
                for (int i = 0; i < compiledFunction.Type.Size; i++)
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
            for (int i = 0; i < compiledFunction.Type.Size; i++)
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

            if (anyCall.Arguments.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": required {1} passed {anyCall.Arguments.Length}", anyCall, CurrentFile); }

            StatementWithValue param = anyCall.Arguments[0];
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
            for (int i = 0; i < functionType.ReturnType.Size; i++)
            { AddInstruction(Opcode.Pop); }
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(BinaryOperatorCall @operator)
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
                for (int i = 0; i < operatorDefinition.Type.Size; i++)
                { AddInstruction(Opcode.Pop); }
            }

            AddComment("}");
        }
        else if (LanguageOperators.BinaryOperators.Contains(@operator.Operator.Content))
        {
            FindStatementType(@operator);

            GeneralType leftType = FindStatementType(@operator.Left);
            GeneralType rightType = FindStatementType(@operator.Right);

            BitWidth leftBitWidth = GetBitWidth(leftType);
            BitWidth rightBitWidth = GetBitWidth(rightType);
            BitWidth bitWidth = MaxBitWidth(leftBitWidth, rightBitWidth);

            int jumpInstruction = InvalidFunctionAddress;

            GenerateCodeForStatement(@operator.Left);

            if (leftType != BasicType.Float &&
                rightType == BasicType.Float)
            {
                AddInstruction(Opcode.FTo,
                    (InstructionOperand)StackTop,
                    (InstructionOperand)StackTop);
            }

            if (@operator.Operator.Content == BinaryOperatorCall.LogicalAND)
            {
                StackLoad(StackTop);

                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.PopTo, reg.Register);
                    AddInstruction(Opcode.Compare, reg.Register, 0);
                    jumpInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JumpIfEqual, 0);
                }
            }
            else if (@operator.Operator.Content == BinaryOperatorCall.LogicalOR)
            {
                StackLoad(StackTop);

                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.PopTo, reg.Get(bitWidth));
                    AddInstruction(Opcode.Compare, reg.Get(bitWidth), 0);

                    jumpInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JumpIfNotEqual, 0);
                }
            }

            GenerateCodeForStatement(@operator.Right);

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

                AddInstruction(Opcode.PopTo, regRight.Get(bitWidth));
                AddInstruction(Opcode.PopTo, regLeft.Get(bitWidth));

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
                        AddInstruction(Opcode.Push, 0);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, 1);
                        break;

                    case BinaryOperatorCall.CompNEQ:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfNotEqual, 3);
                        AddInstruction(Opcode.Push, 0);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, 1);
                        break;

                    case BinaryOperatorCall.CompGT:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLessOrEqual, 3);
                        AddInstruction(Opcode.Push, 1);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, 0);
                        break;

                    case BinaryOperatorCall.CompGEQ:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLess, 3);
                        AddInstruction(Opcode.Push, 1);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, 0);
                        break;

                    case BinaryOperatorCall.CompLT:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLess, 3);
                        AddInstruction(Opcode.Push, 0);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, 1);
                        break;

                    case BinaryOperatorCall.CompLEQ:
                        AddInstruction(Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLessOrEqual, 3);
                        AddInstruction(Opcode.Push, 0);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, 1);
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
                for (int i = 0; i < operatorDefinition.Type.Size; i++)
                { AddInstruction(Opcode.Pop); }
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
                        AddInstruction(Opcode.PopTo, reg.Get(bitWidth));
                        AddInstruction(Opcode.Compare, reg.Get(bitWidth), 0);
                        AddInstruction(Opcode.JumpIfEqual, 3);
                        AddInstruction(Opcode.Push, 0);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, 1);
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
    void GenerateCodeForStatement(LiteralStatement literal)
    {
        switch (literal.Type)
        {
            case LiteralType.Integer:
            {
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
                GenerateCodeForLiteralString(literal.Value);
                break;
            }
            case LiteralType.Char:
            {
                OnGotStatementType(literal, new BuiltinType(BasicType.Char));

                if (literal.Value.Length != 1) throw new InternalException($"Literal char contains {literal.Value.Length} characters but only 1 allowed", literal, CurrentFile);
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

        GenerateAllocator(Literal.CreateAnonymous(1 + literal.Length, Position.UnknownPosition));

        AddComment("}");

        AddComment("Set string data {");

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            // Save pointer
            AddInstruction(Opcode.Move, reg.Register, (InstructionOperand)StackTop);

            for (int i = 0; i < literal.Length; i++)
            {
                AddInstruction(Opcode.Move, reg.Register.ToPtr(i), literal[i]);
            }

            AddInstruction(Opcode.Move, reg.Register.ToPtr(literal.Length), '\0');
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

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariable? globalVariable, out _))
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

            if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
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
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.Push, reg.Register.ToPtr());
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

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.Compare, reg.Register, 0);
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

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.Compare, reg.Register, 0);
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
                AddComment("If jump-to-next");
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.PopTo, reg.Register);
                    AddInstruction(Opcode.Compare, reg.Register, 0);
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
                AddComment("Elseif jump-to-next");

                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.PopTo, reg.Register);
                    AddInstruction(Opcode.Compare, reg.Register, 0);
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
                GenerateAllocator(Literal.CreateAnonymous(pointerType.To.Size, newObject.Type));

                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.Move, reg.Register, (InstructionOperand)StackTop);

                    for (int offset = 0; offset < pointerType.To.Size; offset++)
                    {
                        AddInstruction(Opcode.Move, reg.Register.ToPtr(offset), 0);
                    }
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

        for (int i = 0; i < newInstanceType.Size; i++)
        {
            AddInstruction(Opcode.Push, (InstructionOperand)(StackTop + 1 - newInstanceType.Size));
        }

        parameterCleanup = GenerateCodeForArguments(constructorCall.Arguments, compiledFunction, 1);
        parameterCleanup.Insert(0, (newInstanceType.Size, false, newInstanceType, newInstance.Position));

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

            if (!structPointerType.GetField(field.Identifier.Content, out CompiledField? fieldDefinition, out int fieldOffset))
            { throw new InternalException($"Field \"{field.Identifier}\" not found", field.Identifier, CurrentFile); }

            field.CompiledType = fieldDefinition.Type;
            field.Reference = fieldDefinition;
            fieldDefinition.References.Add(new Reference<Statement>(field, CurrentFile, CurrentContext));

            GenerateCodeForStatement(field.PrevStatement);

            CheckPointerNull();

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                AddInstruction(Opcode.PopTo, reg.Register);
                AddInstruction(Opcode.Push, reg.Register.ToPtr(fieldOffset));
            }

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

        StatementWithValue? dereference = NeedDerefernce(field);

        if (dereference is null)
        {
            ValueAddress offset = GetDataAddress(field);
            StackLoad(offset, compiledField.Type.Size);
        }
        else
        {
            int offset = GetDataOffset(field, dereference);
            ValueAddress pointerOffset = GetBaseAddress(dereference);
            for (int i = 0; i < type.Size; i++)
            {
                AddComment($"{i}:");
                HeapLoad(pointerOffset, offset + i);
            }
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
                using (RegisterUsage.Auto reg1 = Registers.GetFree())
                {
                    ValueAddress address = GetDataAddress(identifier);
                    AddInstruction(Opcode.Move, reg1.Register, address.Address * BytecodeProcessor.StackDirection);

                    switch (address.AddressingMode)
                    {
                        case AddressingMode.Pointer:
                            throw new NotImplementedException();
                        case AddressingMode.PointerBP:
                            AddInstruction(Opcode.MathAdd, reg1.Register, Register.BasePointer);
                            break;
                        default:
                            throw new UnreachableException();
                    }
                    AddInstruction(Opcode.Push, reg1.Register);

                    GenerateCodeForStatement(index.Index);

                    using (RegisterUsage.Auto reg2 = Registers.GetFree())
                    {
                        AddInstruction(Opcode.PopTo, reg2.Register);

                        AddInstruction(Opcode.PopTo, reg1.Register);

                        if (BytecodeProcessor.StackDirection > 0)
                        {
                            AddInstruction(Opcode.MathAdd, reg1.Register, reg2.Register);
                        }
                        else
                        {
                            AddInstruction(Opcode.MathSub, reg1.Register, reg2.Register);
                        }
                    }

                    AddInstruction(Opcode.Push, reg1.Register.ToPtr());
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
                AddInstruction(Opcode.PopTo, reg1.Register);
                AddInstruction(Opcode.MathMult, reg1.Register, pointerType.To.Size);

                using (RegisterUsage.Auto reg2 = Registers.GetFree())
                {
                    AddInstruction(Opcode.PopTo, reg2.Register);
                    AddInstruction(Opcode.MathAdd, reg1.Register, reg2.Register);
                }

                AddInstruction(Opcode.Push, reg1.Register);

                CheckPointerNull();

                AddInstruction(Opcode.PopTo, reg1.Register);
                AddInstruction(Opcode.Push, reg1.Register.ToPtr());
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
    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Equals(ModifierKeywords.Ref))
        {
            ValueAddress address = GetDataAddress(statement);

            if (address.IsReference)
            {
                StackLoad(address.ToUnreferenced());
            }
            else
            {
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.Move, reg.Register, address.Address * BytecodeProcessor.StackDirection);

                    switch (address.AddressingMode)
                    {
                        case AddressingMode.Pointer:
                            break;
                        case AddressingMode.PointerBP:
                            AddInstruction(Opcode.MathAdd, reg.Register, Register.BasePointer);
                            break;
                        default:
                            throw new UnreachableException();
                    }

                    AddInstruction(Opcode.Push, reg.Register);
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
    [DoesNotReturn]
    void GenerateCodeForStatement(LiteralList listValue) => throw new NotImplementedException();
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
            TryComputeSimple(typeCast.PrevStatement, out CompiledValue prevValue) &&
            CompiledValue.TryCast(ref prevValue, targetBuiltinType.RuntimeType))
        {
            AddInstruction(Opcode.Push, prevValue);
            return;
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

    void CleanupGlobalVariables(ImmutableArray<CleanupItem> cleanupItems, Position position)
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
        else if (GetGlobalVariable(statementToSet.Content, statementToSet.File, out CompiledVariable? globalVariable, out _))
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
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType type = FindStatementType(statementToSet);
        GeneralType valueType = FindStatementType(value);

        if (prevType is PointerType pointerType)
        {
            if (pointerType.To is not StructType structType)
            { throw new CompilerException($"Failed to get the field offsets of type {pointerType.To}", statementToSet.PrevStatement, CurrentFile); }

            if (!structType.GetField(statementToSet.Identifier.Content, out CompiledField? fieldDefinition, out int fieldOffset))
            { throw new CompilerException($"Failed to get the field offset for \"{statementToSet.Identifier}\" in type {pointerType.To}", statementToSet.Identifier, CurrentFile); }

            statementToSet.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;
            statementToSet.Reference = fieldDefinition;
            statementToSet.CompiledType = statementToSet.CompiledType;
            fieldDefinition.References.Add(new Reference<Statement>(statementToSet, CurrentFile, CurrentContext));

            GenerateCodeForStatement(statementToSet.PrevStatement);

            GenerateCodeForStatement(value);
            ValueAddress pointerAddress = StackTop - valueType.Size;
            HeapStore(pointerAddress, fieldOffset);

            AddInstruction(Opcode.Pop);
            return;
        }

        if (prevType is not StructType)
        { throw new NotImplementedException(); }

        if (type != valueType)
        { throw new CompilerException($"Can not set a \"{valueType}\" type value to the \"{type}\" type field.", value, CurrentFile); }

        GenerateCodeForStatement(value);

        StatementWithValue? dereference = NeedDerefernce(statementToSet);

        if (dereference is null)
        {
            ValueAddress offset = GetDataAddress(statementToSet);
            StackStore(offset, valueType.Size);
        }
        else
        {
            int offset = GetDataOffset(statementToSet, dereference);
            HeapStore(dereference, offset);
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

            GeneralType indexType = FindStatementType(statementToSet.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", statementToSet.Index, CurrentFile); }
            GenerateCodeForStatement(statementToSet.Index);

            using (RegisterUsage.Auto reg1 = Registers.GetFree())
            {
                AddInstruction(Opcode.PopTo, reg1.Register);
                AddInstruction(Opcode.MathMult, reg1.Register, pointerType.To.Size);

                using (RegisterUsage.Auto reg2 = Registers.GetFree())
                {
                    AddInstruction(Opcode.PopTo, reg2.Register);

                    AddInstruction(Opcode.MathAdd, reg1.Register, reg2.Register);

                    AddInstruction(Opcode.Push, reg1.Register);

                    CheckPointerNull();

                    AddInstruction(Opcode.PopTo, reg1.Register);

                    AddInstruction(Opcode.PopTo, reg2.Register);

                    AddInstruction(Opcode.Move, reg1.Register.ToPtr(), reg2.Register);
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

        if (targetType.Size != 1) throw new NotImplementedException();

        GenerateCodeForStatement(value);

        GenerateCodeForStatement(statementToSet.PrevStatement);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);

            AddInstruction(Opcode.PopTo, reg.Register.ToPtr());
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
            StackLoad(ReturnFlagAddress);

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                AddInstruction(Opcode.PopTo, reg.Register);
                AddInstruction(Opcode.Compare, reg.Register, 0);
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

        int offset = TagCount.Last + VariablesSize;

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        StackElementInformation debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Tag = compiledVariable.Identifier.Content,
            Address = offset * BytecodeProcessor.StackDirection,
            BasePointerRelative = true,
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
            TryCompute(newVariable.InitialValue, out CompiledValue computedInitialValue))
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
                AddInstruction(Opcode.Push, new CompiledValue(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = compiledVariable.Type.Size;
            GenerateInitialValue(compiledVariable.Type);

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

        StackElementInformation debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Tag = compiledVariable.Identifier.Content,
            Address = offset * BytecodeProcessor.StackDirection,
            BasePointerRelative = false,
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
            TryCompute(newVariable.InitialValue, out CompiledValue computedInitialValue))
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
                AddInstruction(Opcode.Push, new CompiledValue(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = compiledVariable.Type.Size;
            GenerateInitialValue(compiledVariable.Type);

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

        TagCount.Push(0);

        CompiledParameters.Clear();
        CompiledVariables.Clear();
        ReturnInstructions.Clear();

        CompileParameters(function.Parameters);

        CurrentFile = function.File;

        int instructionStart = GeneratedCode.Count;

        CanReturn = true;
        AddInstruction(Opcode.Push, new CompiledValue(false));
        TagCount[^1]++;

        OnScopeEnter(function.Block ?? throw new CompilerException($"Function \"{function.ToReadable()}\" does not have a body", function, function.File));

        if (function is IHaveCompiledType returnType)
        {
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = GetReturnValueAddress(returnType.Type).Address * BytecodeProcessor.StackDirection,
                BasePointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = returnType.Type.Size,
                Tag = "Return Value",
                Type = StackElementType.Value,
            });
        }

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ReturnFlagOffset * BytecodeProcessor.StackDirection,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Return Flag",
            Type = StackElementType.Value,
        });
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = SavedBasePointerOffset * BytecodeProcessor.StackDirection,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Saved BasePointer",
            Type = StackElementType.Value,
        });
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = SavedCodePointerOffset * BytecodeProcessor.StackDirection,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Saved CodePointer",
            Type = StackElementType.Value,
        });
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = AbsoluteGlobalOffset * BytecodeProcessor.StackDirection,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Absolute Global Offset",
            Type = StackElementType.Value,
        });

        for (int i = 0; i < CompiledParameters.Count; i++)
        {
            CompiledParameter p = CompiledParameters[i];

            StackElementInformation debugInfo = new()
            {
                Address = GetBaseAddress(p).Address * BytecodeProcessor.StackDirection,
                Kind = StackElementKind.Parameter,
                BasePointerRelative = true,
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

        AddInstruction(Opcode.Push, Register.BasePointer);
        AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = SavedBasePointerOffset * BytecodeProcessor.StackDirection,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Saved BasePointer",
            Type = StackElementType.Value,
        });

        CurrentContext = null;
        TagCount.Push(0);
        ReturnInstructions.Push(new List<int>());

        CanReturn = true;
        AddInstruction(Opcode.Push, new CompiledValue(false));
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ReturnFlagOffset * BytecodeProcessor.StackDirection,
            BasePointerRelative = true,
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

        AddInstruction(Opcode.PopTo, Register.BasePointer);

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

            AddComment("Push exit code:");
            AddInstruction(Opcode.Push, new CompiledValue(0));
        }

        {
            // Absolute global offset

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                if (false)
                {
                    AddInstruction(Opcode.Push, Register.StackPointer);
                }
                else
                {
                    AddInstruction(Opcode.Move, reg.Register, Register.StackPointer);
                    AddInstruction(Opcode.MathAdd, reg.Register, -1 * BytecodeProcessor.StackDirection);
                    AddInstruction(Opcode.Push, reg.Register);
                }
            }

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = AbsoluteGlobalOffset * BytecodeProcessor.StackDirection,
                BasePointerRelative = true,
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
            ScopeInformation scope = CurrentScopeDebug.Pop();
            scope.Location.Instructions.End = GeneratedCode.Count - 1;
            DebugInfo?.ScopeInformation.Add(scope);
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
