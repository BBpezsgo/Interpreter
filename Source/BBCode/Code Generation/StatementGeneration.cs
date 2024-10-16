using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;
using LiteralStatement = LanguageCore.Parser.Statement.Literal;

#if NET_STANDARD
#pragma warning disable CS8604 // Possible null reference argument.
#endif

namespace LanguageCore.BBLang.Generator;

public record struct ParameterCleanupItem(int Size, bool CanDeallocate, GeneralType Type, Position Position) { }

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
        => AddInstruction(new PreparationInstruction(opcode, new InstructionOperand(operand)));

    void AddInstruction(
        Opcode opcode,
        int operand)
        => AddInstruction(new PreparationInstruction(opcode, new InstructionOperand(new CompiledValue(operand))));

    void AddInstruction(
        Opcode opcode,
        bool operand)
        => AddInstruction(new PreparationInstruction(opcode, new InstructionOperand(new CompiledValue(operand))));

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

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, parameters, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? error, AddCompilable))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found: {error}", size, CurrentFile); }

        CompiledFunction? allocator = result.Function;

        if (!allocator.ReturnSomething)
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", size, CurrentFile); }

        allocator.References.Add(size, CurrentFile);

        if (!allocator.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", size, CurrentFile));
            return;
        }

        if (TryEvaluate(allocator, parameters, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            AddInstruction(Opcode.Push, returnValue.Value);
            return;
        }

        if (allocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out IExternalFunction? externalFunction, out WillBeCompilerException? exception))
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
            StackAlloc(allocator.Type.GetSize(this), false);
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

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameters, CurrentFile, out FunctionQueryResult<CompiledFunction>? result, out _, AddCompilable))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found", value, CurrentFile); }
        CompiledFunction? deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue?>(value, CurrentFile));

        if (!deallocator.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", value, CurrentFile));
            return;
        }

        if (TryEvaluate(deallocator, parameters, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            AddInstruction(Opcode.Push, returnValue.Value);
            return;
        }

        if (deallocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(deallocator.ExternalFunctionName, out IExternalFunction? externalFunction, out WillBeCompilerException? exception))
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
            StackAlloc(deallocator.Type.GetSize(this), false);
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
            Pop(deallocator.Type.GetSize(this));
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

        Stack<ParameterCleanupItem> parameterCleanup = new() { new(deallocateableType.GetSize(this), false, deallocateableType, position) };

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset);

        if (deallocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, position, deallocator, CurrentFile)); }

        if (deallocator.ReturnSomething)
        {
            AddComment($" Clear return value:");

            // TODO: wtf?
            const int returnValueSize = 0;
            Pop(returnValueSize);
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateDestructor(GeneralType deallocateableType, Position position)
    {
        ImmutableArray<GeneralType> argumentTypes = ImmutableArray.Create<GeneralType>(deallocateableType);
        FunctionQueryResult<CompiledGeneralFunction>? result;

        if (deallocateableType.Is(out PointerType? deallocateablePointerType))
        {
            if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, CurrentFile, out result, out WillBeCompilerException? error, AddCompilable))
            {
                AddComment($"Pointer value should be already there");
                GenerateDeallocator(deallocateableType, position);
                AddComment("}");

                if (!deallocateablePointerType.To.Is<BuiltinType>())
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

        if (deallocateableType.Is<PointerType>())
        {
            GenerateDeallocator(deallocateableType, position);
        }
        else
        {
            Pop(deallocateableType.GetSize(this));
        }

        AddComment("}");
    }

    #region Find Size

    protected override bool FindSize(PointerType type, out int size)
    {
        size = PointerSize;
        return true;
    }

    protected override bool FindSize(FunctionType type, out int size)
    {
        size = PointerSize;
        return true;
    }

    #endregion

    #region Generate Size

    void GenerateSize(GeneralType type, Register result)
    {
        switch (type)
        {
            case PointerType v: GenerateSize(v, result); break;
            case ArrayType v: GenerateSize(v, result); break;
            case FunctionType v: GenerateSize(v, result); break;
            case StructType v: GenerateSize(v, result); break;
            case GenericType v: GenerateSize(v, result); break;
            case BuiltinType v: GenerateSize(v, result); break;
            case AliasType v: GenerateSize(v, result); break;
            default: throw new NotImplementedException();
        };
    }

    void GenerateSize(PointerType type, Register result)
    {
        AddInstruction(Opcode.MathAdd, result, PointerSize);
    }
    void GenerateSize(ArrayType type, Register result)
    {
        if (FindSize(type, out int size))
        {
            AddInstruction(Opcode.MathAdd, result, size);
            return;
        }
        if (type.Length is null)
        { throw new CompilerException($"Array type doesn't have a size", null, CurrentFile); }

        GeneralType lengthType = FindStatementType(type.Length);
        if (!lengthType.Is<BuiltinType>())
        { throw new CompilerException($"Array length must be a builtin type and not {lengthType}", type.Length, CurrentFile); }
        if (lengthType.GetBitWidth(this) != BitWidth._32)
        { throw new CompilerException($"Array length must be a 32 bit integer and not {lengthType}", type.Length, CurrentFile); }

        if (!FindSize(type.Of, out int elementSize))
        { throw new NotImplementedException(); }

        GenerateCodeForStatement(type.Length);
        using (RegisterUsage.Auto lengthRegister = Registers.GetFree())
        {
            PopTo(lengthRegister.Get(lengthType.GetBitWidth(this)));
            AddInstruction(Opcode.MathMult, lengthRegister.Get(lengthType.GetBitWidth(this)), elementSize);
            AddInstruction(Opcode.MathAdd, result, lengthRegister.Get(lengthType.GetBitWidth(this)));
        }
    }
    void GenerateSize(FunctionType type, Register result)
    {
        AddInstruction(Opcode.MathAdd, result, PointerSize);
    }
    void GenerateSize(StructType type, Register result)
    {
        if (!FindSize(type, out int size))
        { throw new NotImplementedException(); }
        AddInstruction(Opcode.MathAdd, result, size);
    }
    void GenerateSize(GenericType type, Register result) => throw new InvalidOperationException($"Generic type doesn't have a size");
    void GenerateSize(BuiltinType type, Register result)
    {
        if (!FindSize(type, out int size))
        { throw new NotImplementedException(); }
        AddInstruction(Opcode.MathAdd, result, size);
    }
    void GenerateSize(AliasType type, Register result) => GenerateSize(type.Value, result);

    #endregion

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

        Identifier variableIdentifier = new(newVariable.Identifier, newVariable.File);
        if (newVariable.InitialValue is LiteralList literalList)
        {
            for (int i = 0; i < literalList.Values.Length; i++)
            {
                StatementWithValue value = literalList.Values[i];
                GenerateCodeForValueSetter(new IndexCall(variableIdentifier, LiteralStatement.CreateAnonymous(i, variableIdentifier.Position.After()), TokenPair.CreateAnonymous(variableIdentifier.Position.After(), "[", "]"), variableIdentifier.File), value);
            }
            AddComment("}");
            return;
        }

        GenerateCodeForValueSetter(variableIdentifier, newVariable.InitialValue);
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
                    PopTo(GetReturnValueAddress(returnValueType), returnValueType.GetSize(this));
                }
                else
                {
                    AddComment(" Set exit code:");
                    PopTo(ExitCodeAddress, ExitCodeType.GetSize(this));
                }
            }

            AddComment(" .:");

            if (CanReturn)
            {
                AddInstruction(Opcode.Push, ReturnFlagTrue);
                PopTo(ReturnFlagAddress, ReturnFlagType.GetSize(this));
            }

            ReturnInstructions.Last.Add(GeneratedCode.Count);
            AddInstruction(Opcode.Jump, 0);

            AddComment("}");

            return;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Crash)
        {
            if (keywordCall.Arguments.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Crash}\": required {1} passed {keywordCall.Arguments}", keywordCall, CurrentFile); }

            AddComment(" Param 0:");

            StatementWithValue throwValue = keywordCall.Arguments[0];
            GeneralType throwType = FindStatementType(throwValue);

            GenerateCodeForStatement(throwValue);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(throwType.GetBitWidth(this)));
                AddInstruction(Opcode.Crash, reg.Get(throwType.GetBitWidth(this)));
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

            if (argumentType.GetSize(this) != parameterType.GetSize(this))
            { throw new InternalException($"Bad argument type passed: expected {parameterType} passed {argumentType}"); }

            AddComment($" Pass {parameter}:");

            bool typeAllowsTemp = argumentType.Is<PointerType>();

            bool calleeAllowsTemp = parameter.Modifiers.Contains(ModifierKeywords.Temp);

            bool callerAllowsTemp = StatementCanBeDeallocated(argument, out bool explicitDeallocate);

            if (callerAllowsTemp)
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

            if (argument is AddressGetter addressGetter &&
                addressGetter.PrevStatement is Identifier identifier &&
                GetVariable(identifier.Content, out CompiledVariable? variable))
            {
                variable.IsInitialized = true;
            }

            GenerateCodeForStatement(argument, parameterType);

            argumentCleanup.Push(new ParameterCleanupItem(argumentType.GetSize(this), calleeAllowsTemp && callerAllowsTemp && typeAllowsTemp, argumentType, argument.Position));
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

            canDeallocate = canDeallocate && passedParameterType.Is<PointerType>();

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

            parameterCleanup.Push(new ParameterCleanupItem(passedParameterType.GetSize(this), canDeallocate, passedParameterType, passedParameter.Position));
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

            Pop(passedParameter.Size);
        }
    }

    void GenerateCodeForFunctionCall_External<TFunction>(IReadOnlyList<StatementWithValue> parameters, bool saveValue, TFunction compiledFunction, IExternalFunction externalFunction)
        where TFunction : FunctionThingDefinition, ICompiledFunction, ISimpleReadable
    {
        Compiler.Compiler.CheckExternalFunctionDeclaration(this, compiledFunction, externalFunction);

        AddComment($"Call {compiledFunction.ToReadable()} {{");

        if (compiledFunction.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(compiledFunction.Type.GetSize(this), false);
            AddComment($"}}");
        }

        int returnValueOffset = -externalFunction.ReturnValueSize;

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
                PopTo(new AddressOffset(Register.StackPointer, -returnValueOffset), compiledFunction.Type.GetSize(this));
            }
            else
            {
                AddComment($" Clear return value:");
                Pop(compiledFunction.Type.GetSize(this));
            }
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
    {
        compiledFunction.References.Add(new Reference<StatementWithValue?>(functionCall, CurrentFile));
        OnGotStatementType(functionCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(functionCall.File ?? CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"The {compiledFunction.ToReadable()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
            return;
        }

        if (functionCall.MethodArguments.Length != compiledFunction.ParameterCount)
        { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ToReadable()}: required {compiledFunction.ParameterCount} passed {functionCall.MethodArguments.Length}", functionCall, CurrentFile); }

        if (compiledFunction.IsExternal)
        {
            if (!ExternalFunctions.TryGet(compiledFunction.ExternalFunctionName, out IExternalFunction? externalFunction, out WillBeCompilerException? exception))
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
            StackAlloc(compiledFunction.Type.GetSize(this), false);
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
            Pop(compiledFunction.Type.GetSize(this));
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
            else if (param is CompiledTypeStatement compiledTypeStatement)
            { paramType = compiledTypeStatement.Type; }
            else
            { paramType = FindStatementType(param); }

            OnGotStatementType(anyCall, BuiltinType.I32);

            if (FindSize(paramType, out int size))
            {
                AddInstruction(Opcode.Push, size);
                anyCall.PredictedValue = size;
            }
            else
            {
                using RegisterUsage.Auto reg = Registers.GetFree();
                AddInstruction(Opcode.Move, reg.Get(BuiltinType.I32.GetBitWidth(this)), 0);
                GenerateSize(paramType, reg.Get(BuiltinType.I32.GetBitWidth(this)));
                AddInstruction(Opcode.Push, reg.Get(BuiltinType.I32.GetBitWidth(this)));
            }

            return;
        }

        if (GetFunction(anyCall, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? notFound, AddCompilable) &&
            anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            CompiledFunction compiledFunction = result.Function;

            if (anyCall.PrevStatement is Identifier _identifier2)
            { _identifier2.Token.AnalyzedType = TokenAnalyzedType.FunctionName; }
            anyCall.Reference = compiledFunction;
            if (anyCall.PrevStatement is IReferenceableTo<CompiledFunction> _ref1)
            { _ref1.Reference = compiledFunction; }

            if (!Settings.DontOptimize &&
                TryEvaluate(compiledFunction, functionCall.MethodArguments, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
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

            return;
        }

        GeneralType prevType = FindStatementType(anyCall.PrevStatement);
        if (!prevType.Is(out FunctionType? functionType))
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
            StackAlloc(functionType.ReturnType.GetSize(this), false);
            AddComment($"}}");
        }

        Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForParameterPassing(anyCall.Arguments, functionType);

        AddComment(" .:");

        CallRuntime(anyCall.PrevStatement);

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (functionType.ReturnSomething && !anyCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            Pop(functionType.ReturnType.GetSize(this));
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

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperator>? result, out WillBeCompilerException? notFoundError))
        {
            CompiledOperator? operatorDefinition = result.Function;

            operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, CurrentFile));
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
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out IExternalFunction? externalFunction, out WillBeCompilerException? exception))
                {
                    AnalysisCollection?.Errors.Add(exception.InstantiateError(@operator.Operator, CurrentFile));
                    AddComment("}");
                    return;
                }

                GenerateCodeForFunctionCall_External(@operator.Arguments, @operator.SaveValue, operatorDefinition, externalFunction);
                return;
            }

            AddComment($"Call {operatorDefinition.Identifier} {{");

            StackAlloc(operatorDefinition.Type.GetSize(this), false);

            Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForArguments(@operator.Arguments, operatorDefinition);

            AddComment(" .:");

            int jumpInstruction = Call(operatorDefinition.InstructionOffset);

            if (operatorDefinition.InstructionOffset == InvalidFunctionAddress)
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition, CurrentFile)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                Pop(operatorDefinition.Type.GetSize(this));
            }

            AddComment("}");
        }
        else if (LanguageOperators.BinaryOperators.Contains(@operator.Operator.Content))
        {
            FindStatementType(@operator);

            StatementWithValue left = @operator.Left;
            StatementWithValue right = @operator.Right;

            GeneralType leftType = FindStatementType(left, expectedType);
            GeneralType rightType = FindStatementType(right, expectedType);

            if (!leftType.TryGetNumericType(out NumericType leftNType) ||
                !rightType.TryGetNumericType(out NumericType rightNType))
            {
                // AnalysisCollection?.Warnings.Add(notFoundError.InstantiateWarning(@operator, @operator.File));
                throw notFoundError.Instantiate(@operator, @operator.File);
            }

            BitWidth leftBitWidth = leftType.GetBitWidth(this);
            BitWidth rightBitWidth = rightType.GetBitWidth(this);
            BitWidth bitWidth = MaxBitWidth(leftBitWidth, rightBitWidth);

            leftType = BuiltinType.CreateNumeric(leftNType, leftBitWidth);
            rightType = BuiltinType.CreateNumeric(rightNType, rightBitWidth);

            int jumpInstruction = InvalidFunctionAddress;

            GenerateCodeForStatement(left, leftType);

            if (leftNType != NumericType.Float &&
                rightNType == NumericType.Float)
            {
                AddInstruction(Opcode.FTo,
                    (InstructionOperand)StackTop,
                    (InstructionOperand)StackTop);
                leftType = BuiltinType.F32;
            }

            if (@operator.Operator.Content == BinaryOperatorCall.LogicalAND)
            {
                PushFrom(StackTop, leftType.GetSize(this));

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
                PushFrom(StackTop, leftType.GetSize(this));

                using (RegisterUsage.Auto regLeft = Registers.GetFree())
                {
                    PopTo(regLeft.Get(leftBitWidth));
                    AddInstruction(Opcode.Compare, regLeft.Get(leftBitWidth), 0);

                    jumpInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JumpIfNotEqual, 0);
                }
            }

            GenerateCodeForStatement(right, rightType);

            if (leftType.SameAs(BasicType.F32) &&
                !rightType.SameAs(BasicType.F32))
            {
                AddInstruction(Opcode.FTo,
                    (InstructionOperand)StackTop,
                    (InstructionOperand)StackTop);
            }

            if ((leftNType == NumericType.Float) != (rightNType == NumericType.Float))
            {
                throw notFoundError.Instantiate(@operator, @operator.File);
            }

            bool isFloat = leftType.SameAs(BasicType.F32) || rightType.SameAs(BasicType.F32);
            bool isUnsigned = false;
            if (!isFloat)
            {
                isUnsigned = leftNType == NumericType.UnsignedInteger && rightNType == NumericType.UnsignedInteger;
            }

            using (RegisterUsage.Auto regLeft = Registers.GetFree())
            using (RegisterUsage.Auto regRight = Registers.GetFree())
            {
                PopTo(regRight.Get(bitWidth), rightBitWidth);
                PopTo(regLeft.Get(bitWidth), leftBitWidth);

                switch (@operator.Operator.Content)
                {
                    case BinaryOperatorCall.Addition:
                        AddInstruction(isFloat ? Opcode.FMathAdd : isUnsigned ? Opcode.UMathAdd : Opcode.MathAdd, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Subtraction:
                        AddInstruction(isFloat ? Opcode.FMathSub : isUnsigned ? Opcode.UMathSub : Opcode.MathSub, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Multiplication:
                        AddInstruction(isFloat ? Opcode.FMathMult : isUnsigned ? Opcode.UMathMult : Opcode.MathMult, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Division:
                        AddInstruction(isFloat ? Opcode.FMathDiv : isUnsigned ? Opcode.UMathDiv : Opcode.MathDiv, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.Push, regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Modulo:
                        AddInstruction(isFloat ? Opcode.FMathMod : isUnsigned ? Opcode.UMathMod : Opcode.MathMod, regLeft.Get(bitWidth), regRight.Get(bitWidth));
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
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfEqual, 3);
                        AddInstruction(Opcode.Push, false);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, true);
                        break;

                    case BinaryOperatorCall.CompNEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfNotEqual, 3);
                        AddInstruction(Opcode.Push, false);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, true);
                        break;

                    case BinaryOperatorCall.CompGT:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLessOrEqual, 3);
                        AddInstruction(Opcode.Push, true);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, false);
                        break;

                    case BinaryOperatorCall.CompGEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLess, 3);
                        AddInstruction(Opcode.Push, true);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, false);
                        break;

                    case BinaryOperatorCall.CompLT:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLess, 3);
                        AddInstruction(Opcode.Push, false);
                        AddInstruction(Opcode.Jump, 2);
                        AddInstruction(Opcode.Push, true);
                        break;

                    case BinaryOperatorCall.CompLEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
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

            operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, CurrentFile));
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
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out IExternalFunction? externalFunction, out WillBeCompilerException? exception))
                {
                    AnalysisCollection?.Errors.Add(exception.InstantiateError(@operator.Operator, CurrentFile));
                    AddComment("}");
                    return;
                }

                GenerateCodeForFunctionCall_External(@operator.Arguments, @operator.SaveValue, operatorDefinition, externalFunction);
                return;
            }

            AddComment($"Call {operatorDefinition.Identifier} {{");

            StackAlloc(operatorDefinition.Type.GetSize(this), false);

            Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForArguments(@operator.Arguments, operatorDefinition);

            AddComment(" .:");

            int jumpInstruction = Call(operatorDefinition.InstructionOffset);

            if (operatorDefinition.InstructionOffset == InvalidFunctionAddress)
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition, CurrentFile)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                Pop(operatorDefinition.Type.GetSize(this));
            }

            AddComment("}");
        }
        else if (LanguageOperators.UnaryOperators.Contains(@operator.Operator.Content))
        {
            GeneralType leftType = FindStatementType(@operator.Left);
            BitWidth bitWidth = leftType.GetBitWidth(this);

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
                    if (expectedType.SameAs(BasicType.U8))
                    {
                        if (literal.GetInt() is >= byte.MinValue and <= byte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U8);
                            AddInstruction(Opcode.Push, new CompiledValue((byte)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if (literal.GetInt() is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I8);
                            AddInstruction(Opcode.Push, new CompiledValue((sbyte)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.Char))
                    {
                        if (literal.GetInt() is >= ushort.MinValue and <= ushort.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.Char);
                            AddInstruction(Opcode.Push, new CompiledValue((ushort)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if (literal.GetInt() is >= short.MinValue and <= short.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I16);
                            AddInstruction(Opcode.Push, new CompiledValue((short)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.GetInt() >= (int)uint.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U32);
                            AddInstruction(Opcode.Push, new CompiledValue((uint)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I32))
                    {
                        OnGotStatementType(literal, BuiltinType.I32);
                        AddInstruction(Opcode.Push, new CompiledValue(literal.GetInt()));
                        return;
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        OnGotStatementType(literal, BuiltinType.F32);
                        AddInstruction(Opcode.Push, new CompiledValue((float)literal.GetInt()));
                        return;
                    }
                }

                OnGotStatementType(literal, BuiltinType.I32);
                AddInstruction(Opcode.Push, new CompiledValue(literal.GetInt()));
                break;
            }
            case LiteralType.Float:
            {
                OnGotStatementType(literal, BuiltinType.F32);

                AddInstruction(Opcode.Push, new CompiledValue(literal.GetFloat()));
                break;
            }
            case LiteralType.String:
            {
                if (expectedType is not null &&
                    expectedType.Is(out PointerType? pointerType) &&
                    pointerType.To.Is(out ArrayType? arrayType) &&
                    arrayType.Of.SameAs(BasicType.U8))
                {
                    OnGotStatementType(literal, new PointerType(new ArrayType(BuiltinType.U8, LiteralStatement.CreateAnonymous(literal.Value.Length + 1, literal), literal.Value.Length + 1)));
                    GenerateCodeForLiteralString(literal.Value, true);
                }
                else
                {
                    OnGotStatementType(literal, new PointerType(new ArrayType(BuiltinType.Char, LiteralStatement.CreateAnonymous(literal.Value.Length + 1, literal), literal.Value.Length + 1)));
                    GenerateCodeForLiteralString(literal.Value, false);
                }
                break;
            }
            case LiteralType.Char:
            {
                if (literal.Value.Length != 1) throw new InternalException($"Literal char contains {literal.Value.Length} characters but only 1 allowed", literal, CurrentFile);

                if (expectedType is not null)
                {
                    if (expectedType.SameAs(BasicType.U8))
                    {
                        if ((int)literal.Value[0] is >= byte.MinValue and <= byte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U8);
                            AddInstruction(Opcode.Push, new CompiledValue((byte)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if ((int)literal.Value[0] is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I8);
                            AddInstruction(Opcode.Push, new CompiledValue((sbyte)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.Char))
                    {
                        OnGotStatementType(literal, BuiltinType.Char);
                        AddInstruction(Opcode.Push, new CompiledValue(literal.Value[0]));
                        return;
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if ((int)literal.Value[0] is >= short.MinValue and <= short.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I16);
                            AddInstruction(Opcode.Push, new CompiledValue((short)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.Value[0] >= uint.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U32);
                            AddInstruction(Opcode.Push, new CompiledValue((short)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I32))
                    {
                        if (literal.Value[0] >= int.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I32);
                            AddInstruction(Opcode.Push, new CompiledValue((short)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        OnGotStatementType(literal, BuiltinType.F32);
                        AddInstruction(Opcode.Push, new CompiledValue((float)literal.Value[0]));
                        return;
                    }
                }

                OnGotStatementType(literal, BuiltinType.Char);
                AddInstruction(Opcode.Push, new CompiledValue(literal.Value[0]));
                break;
            }
            default: throw new UnreachableException();
        }
    }

    void GenerateCodeForLiteralString(string literal, bool withBytes)
    {
        BuiltinType type = withBytes ? BuiltinType.U8 : BuiltinType.Char;

        AddComment($"Create String \"{literal}\" {{");

        AddComment("Allocate String object {");

        GenerateAllocator(LiteralStatement.CreateAnonymous((1 + literal.Length) * type.GetSize(this), Position.UnknownPosition));

        AddComment("}");

        AddComment("Set string data {");

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            // Save pointer
            AddInstruction(Opcode.Move, reg.Get(PointerBitWidth), (InstructionOperand)StackTop);

            if (withBytes)
            {
                for (int i = 0; i < literal.Length; i++)
                {
                    AddInstruction(Opcode.Move, reg.Get(PointerBitWidth).ToPtr(i * type.GetSize(this), type.GetBitWidth(this)), (byte)literal[i]);
                }

                AddInstruction(Opcode.Move, reg.Get(PointerBitWidth).ToPtr(literal.Length * type.GetSize(this), type.GetBitWidth(this)), (byte)'\0');
            }
            else
            {
                for (int i = 0; i < literal.Length; i++)
                {
                    AddInstruction(Opcode.Move, reg.Get(PointerBitWidth).ToPtr(i * type.GetSize(this), type.GetBitWidth(this)), literal[i]);
                }

                AddInstruction(Opcode.Move, reg.Get(PointerBitWidth).ToPtr(literal.Length * type.GetSize(this), type.GetBitWidth(this)), '\0');
            }
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

            PushFrom(address, param.Type.GetSize(this));

            return;
        }

        if (GetVariable(variable.Content, out CompiledVariable? val))
        {
            variable.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = val;
            OnGotStatementType(variable, val.Type);

            if (!val.IsInitialized)
            { AnalysisCollection?.Warnings.Add(new Warning($"U are using the variable {val.Identifier} but its aint initialized.", variable, variable.File)); }

            PushFrom(GetLocalVariableAddress(val), val.Type.GetSize(this));
            return;
        }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariable? globalVariable, out _))
        {
            variable.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = globalVariable;
            OnGotStatementType(variable, globalVariable.Type);

            PushFrom(GetGlobalVariableAddress(globalVariable), globalVariable.Type.GetSize(this));
            return;
        }

        if (GetFunction(variable.Token.Content, expectedType, out FunctionQueryResult<CompiledFunction>? result, out _))
        {
            CompiledFunction? compiledFunction = result.Function;

            compiledFunction.References.Add(variable, CurrentFile);
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
        if (!addressType.Is(out PointerType? pointerType))
        { throw new CompilerException($"This isn't a pointer", pointer.PrevStatement, CurrentFile); }

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(pointerType.GetBitWidth(this)));
            PushFrom(new AddressRegisterPointer(reg.Get(pointerType.GetBitWidth(this))), pointerType.To.GetSize(this));
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
            PopTo(reg.Get(conditionType.GetBitWidth(this)));
            AddInstruction(Opcode.Compare, reg.Get(conditionType.GetBitWidth(this)), 0);
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
            PopTo(reg.Get(conditionType.GetBitWidth(this)));
            AddInstruction(Opcode.Compare, reg.Get(conditionType.GetBitWidth(this)), 0);
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
                    PopTo(reg.Get(conditionType.GetBitWidth(this)));
                    AddInstruction(Opcode.Compare, reg.Get(conditionType.GetBitWidth(this)), 0);
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
                    PopTo(reg.Get(conditionType.GetBitWidth(this)));
                    AddInstruction(Opcode.Compare, reg.Get(conditionType.GetBitWidth(this)), 0);
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
                GenerateAllocator(new AnyCall(
                    new Identifier(Token.CreateAnonymous("sizeof"), newObject.File),
                    new CompiledTypeStatement[] { new(Token.CreateAnonymous(StatementKeywords.Type), pointerType.To) },
                    Array.Empty<Token>(),
                    TokenPair.CreateAnonymous(Position.UnknownPosition, "(", ")"),
                    newObject.File));

                // using (RegisterUsage.Auto reg = Registers.GetFree())
                // {
                //     AddInstruction(Opcode.Move, reg.Get(BytecodeProcessor.PointerBitWidth), (InstructionOperand)StackTop);
                // 
                //      for (int offset = 0; offset < pointerType.To.SizeBytes; offset++)
                //   { AddInstruction(Opcode.Move, reg.Get(BytecodeProcessor.PointerBitWidth).ToPtr(offset, BitWidth._8), new InstructionOperand((byte)0)); }
                // }

                break;
            }

            case StructType structType:
            {
                structType.Struct.References.Add(newObject.Type, CurrentFile);

                StackAlloc(structType.GetSize(this), true);
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

        compiledFunction.References.Add(constructorCall, CurrentFile);
        OnGotStatementType(constructorCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"Constructor {compiledFunction.ToReadable()} could not be called due to its protection level", constructorCall.Type, CurrentFile));
            return;
        }

        AddComment($"Call {compiledFunction.ToReadable()} {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        NewInstance newInstance = constructorCall.ToInstantiation();
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

        if (prevType.Is(out ArrayType? arrayType) && field.Identifier.Equals("Length"))
        {
            if (arrayType.Length is null)
            { throw new CompilerException("Array type's length isn't defined", field, field.File); }

            if (!arrayType.ComputedLength.HasValue)
            { throw new CompilerException("I will eventually implement this", field, field.File); }

            OnGotStatementType(field, ArrayLengthType);
            field.PredictedValue = arrayType.ComputedLength.Value;

            AddInstruction(Opcode.Push, arrayType.ComputedLength.Value);
            return;
        }

        if (prevType.Is(out PointerType? pointerType))
        {
            if (!pointerType.To.Is(out StructType? structPointerType))
            { throw new CompilerException($"Could not get the field offsets of type {pointerType}", field.PrevStatement, CurrentFile); }

            if (!structPointerType.GetField(field.Identifier.Content, this, out CompiledField? fieldDefinition, out int fieldOffset))
            { throw new InternalException($"Field \"{field.Identifier}\" not found", field.Identifier, CurrentFile); }

            field.CompiledType = fieldDefinition.Type;
            field.Reference = fieldDefinition;
            fieldDefinition.References.Add(field, CurrentFile);

            GenerateCodeForStatement(field.PrevStatement);

            CheckPointerNull();

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(pointerType.GetBitWidth(this)));
                PushFrom(new AddressOffset(
                    new AddressRegisterPointer(reg.Get(pointerType.GetBitWidth(this))),
                    fieldOffset
                    ), fieldDefinition.Type.GetSize(this));
            }

            return;
        }

        if (!prevType.Is(out StructType? structType)) throw new NotImplementedException();

        GeneralType type = FindStatementType(field);

        if (!structType.GetField(field.Identifier.Content, this, out CompiledField? compiledField, out _))
        { throw new CompilerException($"Field definition \"{field.Identifier}\" not found in type \"{structType}\"", field, CurrentFile); }

        field.CompiledType = compiledField.Type;
        field.Reference = compiledField;
        compiledField.References.Add(field, CurrentFile);

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

        StatementWithValue? dereference = NeedDereference(field);

        if (dereference is null)
        {
            Address offset = GetDataAddress(field);
            PushFrom(offset, compiledField.Type.GetSize(this));
        }
        else
        {
            int offset = GetDataOffset(field, dereference);
            HeapLoad(dereference, offset, type.GetSize(this));
        }
    }
    void GenerateCodeForStatement(IndexCall index)
    {
        GeneralType prevType = FindStatementType(index.PrevStatement);
        GeneralType indexType = FindStatementType(index.Index);

        if (GetIndexGetter(prevType, indexType, index.File, out FunctionQueryResult<CompiledFunction>? indexer, out WillBeCompilerException? notFoundError, AddCompilable))
        {
            GenerateCodeForFunctionCall_Function(new FunctionCall(
                    index.PrevStatement,
                    Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerGet),
                    ImmutableArray.Create(index.Index),
                    index.Brackets,
                    index.File
                ), indexer.Function);
            return;
        }

        if (prevType.Is(out ArrayType? arrayType))
        {
            if (index.PrevStatement is not Identifier identifier)
            { throw new NotSupportedException($"Only variables/parameters supported by now", index.PrevStatement, CurrentFile); }

            if (TryCompute(index.Index, out CompiledValue computedIndexData))
            {
                index.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || (arrayType.ComputedLength.HasValue && computedIndexData >= arrayType.ComputedLength.Value))
                { AnalysisCollection?.Warnings.Add(new Warning($"Index out of range", index.Index, CurrentFile)); }

                if (GetParameter(identifier.Content, out CompiledParameter? param))
                {
                    if (!param.Type.SameAs(arrayType))
                    { throw new NotImplementedException(); }

                    Address offset = GetParameterAddress(param, (int)computedIndexData * arrayType.Of.GetSize(this));
                    PushFrom(offset, arrayType.Of.GetSize(this));

                    throw new NotImplementedException();
                }

                if (GetVariable(identifier.Content, out CompiledVariable? val))
                {
                    if (!val.Type.SameAs(arrayType))
                    { throw new NotImplementedException(); }

                    if (!val.IsInitialized)
                    { AnalysisCollection?.Warnings.Add(new Warning($"U are using the variable {val.Identifier} but its aint initialized.", identifier, identifier.File)); }

                    int offset = (int)computedIndexData * arrayType.Of.GetSize(this);
                    Address address = GetLocalVariableAddress(val);

                    PushFrom(new AddressOffset(address, offset), arrayType.Of.GetSize(this));

                    return;
                }
            }

            {
                using (RegisterUsage.Auto regPtr = Registers.GetFree())
                {
                    Address address = GetDataAddress(identifier);
                    GenerateAddressResolver(address);
                    PopTo(regPtr.Get(PointerBitWidth));

                    GenerateCodeForStatement(index.Index);

                    if (!indexType.Is<BuiltinType>())
                    { throw new CompilerException($"Index must be a builtin type (i.e. int) and not {indexType}", index.Index, CurrentFile); }

                    using (RegisterUsage.Auto regIndex = Registers.GetFree())
                    {
                        PopTo(regIndex.Get(indexType.GetBitWidth(this)));
                        AddInstruction(Opcode.MathMult, regIndex.Get(indexType.GetBitWidth(this)), arrayType.Of.GetSize(this));
                        AddInstruction(Opcode.MathAdd, regPtr.Get(PointerBitWidth), regIndex.Get(indexType.GetBitWidth(this)));
                    }

                    PushFrom(new AddressRegisterPointer(regPtr.Get(PointerBitWidth)), arrayType.Of.GetSize(this));
                }
                return;
            }

            throw new NotImplementedException();
        }

        if (prevType.Is(out PointerType? pointerType) && pointerType.To.Is(out arrayType))
        {
            GenerateCodeForStatement(index.PrevStatement);

            if (!indexType.Is<BuiltinType>())
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", index.Index, CurrentFile); }

            GenerateCodeForStatement(index.Index);

            using (RegisterUsage.Auto regIndex = Registers.GetFree())
            {
                PopTo(regIndex.Get(indexType.GetBitWidth(this)));
                AddInstruction(Opcode.MathMult, regIndex.Get(indexType.GetBitWidth(this)), arrayType.Of.GetSize(this));

                using (RegisterUsage.Auto regPtr = Registers.GetFree())
                {
                    PopTo(regPtr.Get(PointerBitWidth));
                    AddInstruction(Opcode.MathAdd, regIndex.Get(indexType.GetBitWidth(this)), regPtr.Get(PointerBitWidth));
                }

                AddInstruction(Opcode.Push, regIndex.Get(indexType.GetBitWidth(this)));

                CheckPointerNull();

                PopTo(regIndex.Get(indexType.GetBitWidth(this)));

                PushFrom(new AddressRegisterPointer(regIndex.Get(indexType.GetBitWidth(this))), arrayType.Of.GetSize(this));
            }

            return;
        }

        throw new CompilerException($"Index getter for type {prevType} not found", index, CurrentFile);
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
                    PopTo(reg.Get(PointerBitWidth));
                    AddInstruction(Opcode.MathAdd, reg.Get(PointerBitWidth), addressOffset.Offset);
                    AddInstruction(Opcode.Push, reg.Get(PointerBitWidth));
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
    void GenerateCodeForStatement(BasicTypeCast typeCast)
    {
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = GeneralType.From(typeCast.Type, FindType, TryCompute);

        if (statementType.SameAs(targetType))
        { AnalysisCollection?.Hints.Add(new Hint($"Redundant type conversion", typeCast.Keyword, CurrentFile)); }

        if (statementType.GetSize(this) != targetType.GetSize(this))
        { throw new CompilerException($"Can't convert {statementType} ({statementType.GetSize(this)} bytes) to {targetType} ({statementType.GetSize(this)} bytes)", typeCast.Keyword, CurrentFile); }

        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        GenerateCodeForStatement(typeCast.PrevStatement);
    }
    void GenerateCodeForStatement(ManagedTypeCast typeCast)
    {
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = GeneralType.From(typeCast.Type, FindType, TryCompute);
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (statementType.SameAs(targetType))
        {
            AnalysisCollection?.Hints.Add(new Hint($"Redundant type conversion", typeCast.Type, CurrentFile));
            GenerateCodeForStatement(typeCast.PrevStatement);
            return;
        }

        if (!Settings.DontOptimize &&
            targetType.Is(out BuiltinType? targetBuiltinType) &&
            TryComputeSimple(typeCast.PrevStatement, out CompiledValue prevValue) &&
            prevValue.TryCast(targetBuiltinType.RuntimeType, out prevValue))
        {
            AddInstruction(Opcode.Push, prevValue);
            return;
        }

        // f32 -> i32
        if (statementType.SameAs(BuiltinType.F32) &&
            targetType.SameAs(BuiltinType.I32))
        {
            GenerateCodeForStatement(typeCast.PrevStatement);
            AddInstruction(Opcode.FFrom, (InstructionOperand)StackTop, (InstructionOperand)StackTop);
            return;
        }

        // i32 -> f32
        if (statementType.SameAs(BuiltinType.I32) &&
            targetType.SameAs(BuiltinType.F32))
        {
            GenerateCodeForStatement(typeCast.PrevStatement);
            AddInstruction(Opcode.FTo, (InstructionOperand)StackTop, (InstructionOperand)StackTop);
            return;
        }

        if (statementType.GetSize(this) != targetType.GetSize(this))
        {
            if (statementType.GetSize(this) < targetType.GetSize(this))
            {
                AddComment($"Grow {statementType} ({statementType.GetSize(this)} bytes) to {targetType} ({targetType.GetSize(this)}) {{");

                AddComment("Make space");

                StackAlloc(targetType.GetSize(this), true);

                AddComment("Value");

                GenerateCodeForStatement(typeCast.PrevStatement);

                AddComment("Save");

                for (int i = 0; i < statementType.GetSize(this); i++)
                { PopTo(Register.StackPointer.ToPtr((statementType.GetSize(this) - 1) * -BytecodeProcessor.StackDirection, BitWidth._8), BitWidth._8); }

                AddComment("}");

                return;
            }
            else if (statementType.GetSize(this) > targetType.GetSize(this))
            {
                AddComment($"Shrink {statementType} ({statementType.GetSize(this)} bytes) to {targetType} ({targetType.GetSize(this)}) {{");

                AddComment("Make space");

                StackAlloc(targetType.GetSize(this), false);

                AddComment("Value");

                GenerateCodeForStatement(typeCast.PrevStatement);

                AddComment("Save");

                for (int i = 0; i < targetType.GetSize(this); i++)
                { PopTo(Register.StackPointer.ToPtr((statementType.GetSize(this) - 1) * -BytecodeProcessor.StackDirection, BitWidth._8), BitWidth._8); }

                AddComment("Discard excess");

                int excess = statementType.GetSize(this) - targetType.GetSize(this);
                Pop(excess);

                AddComment("}");

                return;
            }

            throw new CompilerException($"Can't modify the size of the value. You tried to convert from {statementType} (size of {statementType.GetSize(this)}) to {targetType} (size of {targetType.GetSize(this)})", typeCast, CurrentFile);
        }

        GenerateCodeForStatement(typeCast.PrevStatement, targetType);
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
            case BasicTypeCast v: GenerateCodeForStatement(v); break;
            case ManagedTypeCast v: GenerateCodeForStatement(v); break;
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
                Pop(item.SizeOnStack);
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
                if (item.SizeOnStack != PointerSize) throw new InternalException();
                if (item.Type is null) throw new InternalException();
                GenerateDestructor(item.Type, position);
            }
            else
            {
                Pop(item.SizeOnStack);
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

            PopTo(address, parameter.Type.GetSize(this));
        }
        else if (GetVariable(statementToSet.Content, out CompiledVariable? variable))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = variable.Type;
            statementToSet.Reference = variable;

            GeneralType valueType = FindStatementType(value, variable.Type);

            AssignTypeCheck(variable.Type, valueType, value);

            GenerateCodeForStatement(value, variable.Type);

            PopTo(GetLocalVariableAddress(variable), variable.Type.GetSize(this));
            variable.IsInitialized = true;
        }
        else if (GetGlobalVariable(statementToSet.Content, statementToSet.File, out CompiledVariable? globalVariable, out _))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = globalVariable.Type;
            statementToSet.Reference = globalVariable;

            GeneralType valueType = FindStatementType(value, globalVariable.Type);

            AssignTypeCheck(globalVariable.Type, valueType, value);

            GenerateCodeForStatement(value, globalVariable.Type);

            PopTo(GetGlobalVariableAddress(globalVariable), globalVariable.Type.GetSize(this));
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

        if (prevType.Is(out PointerType? pointerType))
        {
            if (!pointerType.To.Is(out StructType? structType))
            { throw new CompilerException($"Failed to get the field offsets of type {pointerType.To}", statementToSet.PrevStatement, CurrentFile); }

            if (!structType.GetField(statementToSet.Identifier.Content, this, out CompiledField? fieldDefinition, out int fieldOffset))
            { throw new CompilerException($"Failed to get the field offset for \"{statementToSet.Identifier}\" in type {pointerType.To}", statementToSet.Identifier, CurrentFile); }

            statementToSet.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;
            statementToSet.Reference = fieldDefinition;
            fieldDefinition.References.Add(statementToSet, CurrentFile);

            GenerateCodeForStatement(value, type);
            HeapStore(statementToSet.PrevStatement, fieldOffset, valueType.GetSize(this));
            return;
        }

        if (!prevType.Is<StructType>())
        { throw new NotImplementedException(); }

        AssignTypeCheck(type, valueType, value);

        GenerateCodeForStatement(value, type);

        StatementWithValue? dereference = NeedDereference(statementToSet);

        if (dereference is null)
        {
            Address offset = GetDataAddress(statementToSet);
            PopTo(offset, valueType.GetSize(this));
        }
        else
        {
            int offset = GetDataOffset(statementToSet, dereference);
            HeapStore(dereference, offset, valueType.GetSize(this));
        }
    }
    void GenerateCodeForValueSetter(IndexCall statementToSet, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType indexType = FindStatementType(statementToSet.Index);
        GeneralType valueType = FindStatementType(value);

        if (GetIndexSetter(prevType, valueType, indexType, CurrentFile, out FunctionQueryResult<CompiledFunction>? indexer, out _, AddCompilable))
        {
            GenerateCodeForFunctionCall_Function(new FunctionCall(
                statementToSet.PrevStatement,
                Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerSet),
                ImmutableArray.Create<StatementWithValue>(statementToSet.Index, value),
                statementToSet.Brackets,
                statementToSet.File
            ), indexer.Function);
            return;
        }

        if (prevType.Is(out ArrayType? arrayType))
        {
            if (statementToSet.PrevStatement is not Identifier identifier)
            { throw new NotSupportedException($"Only variables/parameters supported by now", statementToSet.PrevStatement, CurrentFile); }

            if (TryCompute(statementToSet.Index, out CompiledValue computedIndexData))
            {
                statementToSet.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || (arrayType.ComputedLength.HasValue && computedIndexData >= arrayType.ComputedLength.Value))
                { AnalysisCollection?.Warnings.Add(new Warning($"Index out of range", statementToSet.Index, CurrentFile)); }

                GenerateCodeForStatement(value);

                if (GetParameter(identifier.Content, out _))
                { throw new NotImplementedException(); }

                if (GetVariable(identifier.Content, out CompiledVariable? variable))
                {
                    if (!variable.Type.SameAs(arrayType))
                    { throw new NotImplementedException(); }

                    if (!variable.IsInitialized)
                    { AnalysisCollection?.Warnings.Add(new Warning($"U are using the variable {variable.Identifier} but its aint initialized.", identifier, identifier.File)); }

                    int offset = (int)computedIndexData * arrayType.Of.GetSize(this);
                    PopTo(new AddressOffset(GetLocalVariableAddress(variable), offset), arrayType.Of.GetSize(this));
                    return;
                }
            }

            throw new NotImplementedException();
        }

        if (prevType.Is(out PointerType? pointerType) && pointerType.To.Is(out arrayType))
        {
            AssignTypeCheck(arrayType.Of, valueType, value);

            GenerateCodeForStatement(value);

            GenerateCodeForStatement(statementToSet.PrevStatement);

            if (!indexType.Is<BuiltinType>())
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", statementToSet.Index, CurrentFile); }

            GenerateCodeForStatement(statementToSet.Index);

            using (RegisterUsage.Auto regIndex = Registers.GetFree())
            {
                PopTo(regIndex.Get(indexType.GetBitWidth(this)));
                AddInstruction(Opcode.MathMult, regIndex.Get(indexType.GetBitWidth(this)), arrayType.Of.GetSize(this));

                using (RegisterUsage.Auto regPtr = Registers.GetFree())
                {
                    PopTo(regPtr.Get(PointerBitWidth));

                    AddInstruction(Opcode.MathAdd, regPtr.Get(PointerBitWidth), regIndex.Get(indexType.GetBitWidth(this)));

                    AddInstruction(Opcode.Push, regPtr.Get(PointerBitWidth));

                    CheckPointerNull(false);

                    PopTo(new AddressRegisterPointer(regPtr.Get(PointerBitWidth)), valueType.GetSize(this));
                }
            }

            return;
        }
    }
    void GenerateCodeForValueSetter(Pointer statementToSet, StatementWithValue value)
    {
        GeneralType targetType = FindStatementType(statementToSet);
        GeneralType valueType = FindStatementType(value, targetType);
        GeneralType pointerValueType = FindStatementType(statementToSet.PrevStatement);

        AssignTypeCheck(targetType, valueType, value);

        if (pointerValueType.GetBitWidth(this) != PointerBitWidth)
        { throw new CompilerException($"Type {pointerValueType} cant be a pointer", statementToSet.PrevStatement, CurrentFile); }

        GenerateCodeForStatement(value, targetType);

        GenerateCodeForStatement(statementToSet.PrevStatement);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), valueType.GetSize(this));
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
            PushFrom(ReturnFlagAddress, ReturnFlagType.GetSize(this));

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(ReturnFlagType.GetBitWidth(this)));
                AddInstruction(Opcode.Compare, reg.Get(ReturnFlagType.GetBitWidth(this)), 0);
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

        int offset = (VariablesSize + ReturnFlagType.GetSize(this) + type.GetSize(this)) * BytecodeProcessor.StackDirection;

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        StackElementInformation debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Tag = compiledVariable.Identifier.Content,
            Address = offset,
            BasePointerRelative = true,
            Size = compiledVariable.Type.GetSize(this),
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

            size = new BuiltinType(computedInitialValue.Type).GetSize(this);

            AddInstruction(Opcode.Push, computedInitialValue);
            compiledVariable.IsInitialized = true;

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }
        else if (compiledVariable.Type.Is(out ArrayType? arrayType) &&
            arrayType.Of.SameAs(BasicType.Char) &&
            newVariable.InitialValue is LiteralStatement literalStatement &&
            literalStatement.Type == LiteralType.String &&
            arrayType.ComputedLength.HasValue &&
            literalStatement.Value.Length == arrayType.ComputedLength.Value)
        {
            size = arrayType.GetSize(this);
            compiledVariable.IsInitialized = true;

            for (int i = 0; i < literalStatement.Value.Length; i++)
            {
                AddInstruction(Opcode.Push, new CompiledValue(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = compiledVariable.Type.GetSize(this);
            StackAlloc(size, false);

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }

        if (size != compiledVariable.Type.GetSize(this))
        { throw new InternalException($"Variable size ({compiledVariable.Type.GetSize(this)}) and initial value size ({size}) mismatch"); }

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
        else if (compiledVariable.Type.Is(out ArrayType? arrayType) &&
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
            StackAlloc(size);

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
            { sum += variable.Type.GetSize(this); }
            return sum;
        }
    }

    int GlobalVariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariable variable in CompiledGlobalVariables)
            { sum += variable.Type.GetSize(this); }
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

        CurrentContext = function as IDefinition;
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
        { CurrentReturnType = BuiltinType.Void; }

        AddComment("Return flag");
        AddInstruction(Opcode.Push, ReturnFlagFalse);

        OnScopeEnter(function.Block ?? throw new CompilerException($"Function \"{function.ToReadable()}\" does not have a body", function, function.File));

        if (function is IHaveCompiledType returnType && !returnType.Type.SameAs(BasicType.Void))
        {
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = GetReturnValueAddress(returnType.Type).Offset,
                BasePointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = returnType.Type.GetSize(this),
                Tag = "Return Value",
                Type = returnType.Type,
            });
        }

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ReturnFlagOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = ReturnFlagType.GetSize(this),
            Tag = "Return Flag",
            Type = ReturnFlagType,
        });

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = SavedBasePointerOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = BasePointerSize,
            Tag = "Saved BasePointer",
            Type = BasePointerType,
        });

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = SavedCodePointerOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = CodePointerSize,
            Tag = "Saved CodePointer",
            Type = CodePointerType,
        });

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = AbsoluteGlobalOffset,
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
                Size = p.IsRef ? PointerSize : p.Type.GetSize(this),
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
        Pop(ReturnFlagType.GetSize(this));

        AddComment("Return");
        Return();

        if (function.Block != null && function.Block.Statements.Length > 0) AddComment("}");

        if (function.Identifier is not null)
        {
            DebugInfo?.FunctionInformation.Add(new FunctionInformation()
            {
                IsValid = true,
                Function = function,
                TypeArguments = TypeArguments.ToImmutableDictionary(),
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
            Address = SavedBasePointerOffset,
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
        AddInstruction(Opcode.Push, ReturnFlagFalse);
        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = ReturnFlagOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = ReturnFlagType.GetSize(this),
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
        Pop(ReturnFlagType.GetSize(this));

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
                Address = (ExitCodeType.GetSize(this) - 1) * BytecodeProcessor.StackDirection,
                BasePointerRelative = false,
                Kind = StackElementKind.Internal,
                Size = ExitCodeType.GetSize(this),
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
                Address = AbsoluteGlobalOffset,
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
        Pop(AbsGlobalAddressType.GetSize(this)); // Pop abs global offset

        AddInstruction(Opcode.Exit); // Exit code already there

        foreach (CompiledVariable variable in CompiledGlobalVariables)
        {
            CurrentScopeDebug.LastRef.Stack.Add(new StackElementInformation()
            {
                Address = GetGlobalVariableAddress(variable).Offset,
                BasePointerRelative = false,
                Kind = StackElementKind.Variable,
                Size = variable.Type.GetSize(this),
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
