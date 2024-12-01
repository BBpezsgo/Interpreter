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

public record struct ParameterCleanupItem(int Size, bool CanDeallocate, GeneralType Type, Location Location) { }

public partial class CodeGeneratorForMain : CodeGenerator
{
    bool AllowLoopUnrolling => !Settings.DontOptimize;
    bool AllowFunctionInlining => !Settings.DontOptimize;
    bool AllowPrecomputing => !Settings.DontOptimize;
    bool AllowEvaluating => !Settings.DontOptimize;
    bool AllowOtherOptimizations => !Settings.DontOptimize;
    bool AllowInstructionLevelOptimizations => !Settings.DontOptimize;

    #region AddInstruction()

    void AddInstruction(PreparationInstruction instruction)
    {
        if (AllowInstructionLevelOptimizations)
        {
            if ((instruction.Opcode is
                Opcode.Jump or
                Opcode.JumpIfEqual or
                Opcode.JumpIfGreater or
                Opcode.JumpIfGreaterOrEqual or
                Opcode.JumpIfLess or
                Opcode.JumpIfLessOrEqual or
                Opcode.JumpIfNotEqual) &&
                instruction.Operand1 == 1)
            { return; }

            if ((instruction.Opcode is
                Opcode.MathAdd or
                Opcode.MathSub) &&
                instruction.Operand2 == 0)
            { return; }
        }

        // if (GeneratedCode.Count > 0)
        // {
        //     PreparationInstruction last = GeneratedCode[^1];
        //     if (last.Opcode == Opcode.Move &&
        //         instruction.Opcode == Opcode.Push)
        //     {
        //         if (instruction.Operand1.Type == InstructionOperandType.PointerEAX32 &&
        //             last.Operand1 == Register.EAX &&
        //             last.Operand2 == Register.BasePointer)
        //         {
        //             GeneratedCode[^1] = new(
        //                 instruction.Opcode,
        //                 new(instruction.Operand1.Value, InstructionOperandType.PointerBP32)
        //             );
        //             return;
        //         }
        //         // Debugger.Break();
        //     }
        // }

        GeneratedCode.Add(instruction);
    }

    void AddInstruction(
        Opcode opcode)
        => AddInstruction(new PreparationInstruction(opcode));

    void AddInstruction(
        Opcode opcode,
        int operand)
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

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, parameters, size.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found: {error}", size));
            return;
        }

        CompiledFunction? allocator = result.Function;

        if (!allocator.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", size));
            return;
        }

        allocator.References.AddReference(size, size.File);

        if (!allocator.CanUse(size.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", size));
            return;
        }

        if (TryEvaluate(allocator, parameters, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            Push(returnValue.Value);
            return;
        }

        if (allocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(allocator));
                AddComment("}");
                return;
            }

            GenerateCodeForFunctionCall_External(parameters, true, allocator, externalFunction);
            return;
        }

        AddComment($"Call \"{allocator.ToReadable()}\" {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        if (allocator.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(allocator.Type.GetSize(this, Diagnostics, size), false);
            AddComment($"}}");
        }

        parameterCleanup = GenerateCodeForArguments(parameters, allocator);

        AddComment(" .:");

        int jumpInstruction = Call(allocator.InstructionOffset, size);

        if (allocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, size, allocator)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateDeallocator(StatementWithValue value)
    {
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create(value);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameters, value.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical(
                $"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found",
                value,
                notFoundError.ToError(value)));
            return;
        }
        CompiledFunction? deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue?>(value, value.File));

        if (!deallocator.CanUse(value.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", value));
            return;
        }

        if (TryEvaluate(deallocator, parameters, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            Push(returnValue.Value);
            return;
        }

        if (deallocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(deallocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(deallocator));
                AddComment("}");
                return;
            }

            GenerateCodeForFunctionCall_External(parameters, true, deallocator, externalFunction);
            return;
        }

        AddComment($"Call \"{deallocator.ToReadable()}\" {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        if (deallocator.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(deallocator.Type.GetSize(this, Diagnostics, value), false);
            AddComment($"}}");
        }

        parameterCleanup = GenerateCodeForArguments(parameters, deallocator);

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset, value);

        if (deallocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, value, deallocator)); }

        if (deallocator.ReturnSomething)
        {
            AddComment($" Clear return value:");
            Pop(deallocator.Type.GetSize(this, Diagnostics, value));
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateDeallocator(GeneralType deallocateableType, Location location)
    {
        ImmutableArray<GeneralType> parameterTypes = ImmutableArray.Create(deallocateableType);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameterTypes, location.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found ({notFoundError.Message})", location));
            return;
        }
        CompiledFunction? deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue?>(null, location.File));

        if (!deallocator.CanUse(location.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", location));
            return;
        }

        if (deallocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(deallocator.ExternalFunctionName, out _, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(deallocator));
                AddComment("}");
                return;
            }

            throw new NotImplementedException();
        }

        AddComment($"Call \"{deallocator.ToReadable()}\" {{");

        if (deallocator.ReturnSomething)
        { throw new NotImplementedException(); }

        Stack<ParameterCleanupItem> parameterCleanup = new() { new(deallocateableType.GetSize(this, Diagnostics, location), false, deallocateableType, location) };

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset, location);

        if (deallocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, location, deallocator)); }

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

    void GenerateDestructor(GeneralType deallocateableType, Location location)
    {
        ImmutableArray<GeneralType> argumentTypes = ImmutableArray.Create<GeneralType>(deallocateableType);
        FunctionQueryResult<CompiledGeneralFunction>? result;

        if (deallocateableType.Is(out PointerType? deallocateablePointerType))
        {
            if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out result, out PossibleDiagnostic? error, AddCompilable))
            {
                AddComment($"Pointer value should be already there");
                GenerateDeallocator(deallocateableType, location);
                AddComment("}");

                if (deallocateablePointerType.To.Is<StructType>())
                {
                    Diagnostics.Add(Diagnostic.Warning(
                        $"Destructor for type \"{deallocateableType}\" not found",
                        location,
                        error.ToWarning(location)));
                }

                return;
            }
        }
        else
        {
            if (!GetGeneralFunction(deallocateableType, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out result, out PossibleDiagnostic? error, AddCompilable))
            {
                Diagnostics.Add(Diagnostic.Warning(
                    $"Destructor for type \"{deallocateableType}\" not found",
                    location,
                    error.ToWarning(location)));
                return;
            }
        }

        CompiledGeneralFunction? destructor = result.Function;

        destructor.References.Add(new Reference<Statement?>(null, location.File));

        if (!destructor.CanUse(location.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Destructor for type \"{deallocateableType}\" function cannot be called due to its protection level", location));
            AddComment("}");
            return;
        }

        AddComment(" Param0 should be already there");

        AddComment(" .:");

        int jumpInstruction = Call(destructor.InstructionOffset, location);

        if (destructor.InstructionOffset == InvalidFunctionAddress)
        { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, false, location, destructor)); }

        AddComment(" Clear Param0:");

        if (deallocateableType.Is<PointerType>())
        {
            GenerateDeallocator(deallocateableType, location);
        }
        else
        {
            Pop(deallocateableType.GetSize(this, Diagnostics, location));
        }

        AddComment("}");
    }

    #region Find Size

    protected override bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = PointerSize;
        error = null;
        return true;
    }

    protected override bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = PointerSize;
        error = null;
        return true;
    }

    #endregion

    #region Generate Size

    bool GenerateSize(GeneralType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        PointerType v => GenerateSize(v, result, out error),
        ArrayType v => GenerateSize(v, result, out error),
        FunctionType v => GenerateSize(v, result, out error),
        StructType v => GenerateSize(v, result, out error),
        GenericType v => GenerateSize(v, result, out error),
        BuiltinType v => GenerateSize(v, result, out error),
        AliasType v => GenerateSize(v, result, out error),
        _ => throw new NotImplementedException(),
    };

    bool GenerateSize(PointerType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        AddInstruction(Opcode.MathAdd, result, PointerSize);
        return true;
    }
    bool GenerateSize(ArrayType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (FindSize(type, out int size, out _))
        {
            AddInstruction(Opcode.MathAdd, result, size);
            return true;
        }

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        GeneralType lengthType = FindStatementType(type.Length);
        if (!lengthType.Is<BuiltinType>())
        {
            error = new PossibleDiagnostic($"Array length must be a builtin type and not \"{lengthType}\"", type.Length);
            return false;
        }

        if (lengthType.GetBitWidth(this) != BitWidth._32)
        {
            error = new PossibleDiagnostic($"Array length must be a 32 bit integer and not \"{lengthType}\"", type.Length);
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error))
        {
            return false;
        }

        GenerateCodeForStatement(type.Length);
        using (RegisterUsage.Auto lengthRegister = Registers.GetFree())
        {
            PopTo(lengthRegister.Get(lengthType.GetBitWidth(this)));
            AddInstruction(Opcode.MathMult, lengthRegister.Get(lengthType.GetBitWidth(this)), elementSize);
            AddInstruction(Opcode.MathAdd, result, lengthRegister.Get(lengthType.GetBitWidth(this)));
        }
        return true;
    }
    bool GenerateSize(FunctionType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        AddInstruction(Opcode.MathAdd, result, PointerSize);
        return true;
    }
    bool GenerateSize(StructType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        AddInstruction(Opcode.MathAdd, result, size);
        return true;
    }
    bool GenerateSize(GenericType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new InvalidOperationException($"Generic type doesn't have a size");
    bool GenerateSize(BuiltinType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        AddInstruction(Opcode.MathAdd, result, size);
        return true;
    }
    bool GenerateSize(AliasType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Value, result, out error);

    #endregion

    #region GenerateCodeForStatement

    void GenerateCodeForStatement(VariableDeclaration newVariable)
    {
        if (newVariable.Modifiers.Contains(ModifierKeywords.Const)) return;

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        if (GetConstant(newVariable.Identifier.Content, newVariable.File, out _, out _))
        {
            Diagnostics.Add(Diagnostic.Critical($"Symbol name \"{newVariable.Identifier}\" conflicts with an another symbol name", newVariable.Identifier));
            return;
        }

        if (!GetVariable(newVariable.Identifier.Content, out CompiledVariable? compiledVariable, out _) &&
            !GetGlobalVariable(newVariable.Identifier.Content, newVariable.File, out compiledVariable, out _))
        {
            Diagnostics.Add(Diagnostic.Internal($"Variable \"{newVariable.Identifier.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", newVariable.Identifier));
            return;
        }

        if (compiledVariable.IsInitialized) return;

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        if (newVariable.InitialValue == null) return;

        AddComment($"New Variable \"{newVariable.Identifier.Content}\" {{");

        if (newVariable.InitialValue is LiteralList literalList)
        {
            for (int i = 0; i < literalList.Values.Length; i++)
            {
                StatementWithValue value = literalList.Values[i];
                GenerateCodeForValueSetter(new IndexCall(newVariable.Identifier, LiteralStatement.CreateAnonymous(i, newVariable.Identifier.Position.After(), newVariable.Identifier.File), TokenPair.CreateAnonymous(newVariable.Identifier.Position.After(), "[", "]"), newVariable.Identifier.File), value);
            }
            AddComment("}");
            return;
        }

        GenerateCodeForValueSetter(newVariable.Identifier, newVariable.InitialValue);
        AddComment("}");
    }

    void GenerateCodeForStatement(InstructionLabel instructionLabel)
    {
        if (!GetInstructionLabel(instructionLabel.Identifier.Content, out CompiledInstructionLabel? compiledInstructionLabel, out _))
        {
            Diagnostics.Add(Diagnostic.Internal($"Instruction label \"{instructionLabel.Identifier.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", instructionLabel.Identifier));
            return;
        }

        compiledInstructionLabel.InstructionOffset = GeneratedCode.Count;
    }

    void GenerateCodeForStatement(KeywordCall keywordCall)
    {
        AddComment($"Call Keyword \"{keywordCall.Identifier}\" {{");

        if (keywordCall.Identifier.Content == StatementKeywords.Return)
        {
            if (keywordCall.Arguments.Length > 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return;
            }

            if (!CanReturn)
            {
                Diagnostics.Add(Diagnostic.Critical($"Can't return for some reason", keywordCall.Identifier));
                return;
            }

            if (keywordCall.Arguments.Length == 1)
            {
                AddComment(" Param 0:");

                StatementWithValue returnValue = keywordCall.Arguments[0];
                GeneralType returnValueType = FindStatementType(returnValue);

                GenerateCodeForStatement(returnValue);

                if (!CanCastImplicitly(returnValueType, CurrentReturnType, returnValue, out PossibleDiagnostic? castError))
                {
                    Diagnostics.Add(castError.ToError(returnValue));
                }

                if (InFunction)
                {
                    AddComment(" Set return value:");
                    PopTo(GetReturnValueAddress(returnValueType), returnValueType.GetSize(this, Diagnostics, keywordCall));
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
                AddComment("Cleanup function scopes {");
                for (int i = CleanupStack.Count - 1; i >= 0; i--)
                {
                    Scope item = CleanupStack[i];

                    CleanupVariables(item.Variables, keywordCall.Location, true);

                    if (item.IsFunction) break;
                }
                AddComment("}");
            }

            ReturnInstructions.Last.Add(GeneratedCode.Count);
            AddInstruction(Opcode.Jump, 0);

            AddComment("}");

            return;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Crash)
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"{StatementKeywords.Crash}\": required {1} passed {keywordCall.Arguments}", keywordCall));
                return;
            }

            AddComment(" Param 0:");

            StatementWithValue throwValue = keywordCall.Arguments[0];
            GeneralType throwType = FindStatementType(throwValue);

            GenerateCodeForStatement(throwValue);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(throwType.GetBitWidth(this, Diagnostics, throwValue)));
                AddInstruction(Opcode.Crash, reg.Get(throwType.GetBitWidth(this, Diagnostics, throwValue)));
            }

            return;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Break)
        {
            if (BreakInstructions.Count == 0)
            {
                Diagnostics.Add(Diagnostic.Critical($"The keyword \"{StatementKeywords.Break}\" does not available in the current context", keywordCall.Identifier));
                return;
            }

            BreakInstructions.Last.Add(GeneratedCode.Count);
            AddInstruction(Opcode.Jump, 0);

            AddComment("}");

            return;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Delete)
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"{StatementKeywords.Delete}\": required {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return;
            }

            GenerateCodeForStatement(keywordCall.Arguments[0]);

            GeneralType deletableType = FindStatementType(keywordCall.Arguments[0]);
            GenerateDestructor(deletableType, keywordCall.Arguments[0].Location);

            return;
        }

        if (keywordCall.Identifier.Content == "goto")
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"{"goto"}\": required {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return;
            }

            GeneralType argumentType = FindStatementType(keywordCall.Arguments[0]);

            if (!CanCastImplicitly(argumentType, CompiledInstructionLabel.Type, null, out PossibleDiagnostic? castError))
            {
                Diagnostics.Add(castError.ToError(keywordCall.Arguments[0]));
                return;
            }

            GenerateCodeForStatement(keywordCall.Arguments[0]);

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(PointerBitWidth));
                AddInstruction(Opcode.MathSub, reg.Get(PointerBitWidth), GeneratedCode.Count + 1);

                int jumpInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.Jump, reg.Get(PointerBitWidth));
            }

            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"Unknown keyword \"{keywordCall.Identifier}\"", keywordCall.Identifier));
        return;
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

            if (argumentType.GetSize(this, Diagnostics, argument) != parameterType.GetSize(this, Diagnostics, parameter))
            { Diagnostics.Add(Diagnostic.Internal($"Bad argument type passed: expected \"{parameterType}\" passed \"{argumentType}\"", argument)); }

            AddComment($" Pass {parameter}:");

            bool typeAllowsTemp = argumentType.Is<PointerType>();

            bool calleeAllowsTemp = parameter.Modifiers.Contains(ModifierKeywords.Temp);

            bool callerAllowsTemp = StatementCanBeDeallocated(argument, out bool explicitDeallocate);

            if (callerAllowsTemp)
            {
                if (explicitDeallocate && !calleeAllowsTemp)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", argument)); }
                if (explicitDeallocate && !typeAllowsTemp)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this type", argument)); }
            }
            else
            {
                if (explicitDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value", argument)); }
            }

            if (argument is AddressGetter addressGetter &&
                addressGetter.PrevStatement is Identifier identifier &&
                GetVariable(identifier.Content, out CompiledVariable? variable,
                out _))
            {
                variable.IsInitialized = true;
            }

            GenerateCodeForStatement(argument, parameterType);

            argumentCleanup.Push(new ParameterCleanupItem(argumentType.GetSize(this, Diagnostics, argument), calleeAllowsTemp && callerAllowsTemp && typeAllowsTemp, argumentType, argument.Location));
        }

        return argumentCleanup;
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(IReadOnlyList<StatementWithValue> parameters, FunctionType function)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        for (int i = 0; i < parameters.Count; i++)
        {
            StatementWithValue argument = parameters[i];
            GeneralType argumentType = FindStatementType(argument);
            GeneralType definedParameterType = function.Parameters[i];

            AddComment($" Param {i}:");

            bool canDeallocate = true; // temp type maybe?

            canDeallocate = canDeallocate && argumentType.Is<PointerType>();

            if (StatementCanBeDeallocated(argument, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", argument)); }
            }
            else
            {
                if (explicitDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value", argument)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(argument, definedParameterType);

            parameterCleanup.Push(new ParameterCleanupItem(argumentType.GetSize(this, Diagnostics, argument), canDeallocate, argumentType, argument.Location));
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
                GenerateDestructor(passedParameter.Type, passedParameter.Location);
                continue;
            }

            Pop(passedParameter.Size);
        }
    }

    void GenerateCodeForFunctionCall_External<TFunction>(IReadOnlyList<StatementWithValue> parameters, bool saveValue, TFunction compiledFunction, IExternalFunction externalFunction)
        where TFunction : FunctionThingDefinition, ICompiledFunction, ISimpleReadable
    {
        Compiler.Compiler.CheckExternalFunctionDeclaration(this, compiledFunction, externalFunction, Diagnostics);

        AddComment($"Call \"{compiledFunction.ToReadable()}\" {{");

        if (compiledFunction.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(compiledFunction.Type.GetSize(this, Diagnostics, compiledFunction), false);
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
                PopTo(new AddressOffset(Register.StackPointer, -returnValueOffset), compiledFunction.Type.GetSize(this, Diagnostics, compiledFunction));
            }
            else
            {
                AddComment($" Clear return value:");
                Pop(compiledFunction.Type.GetSize(this, Diagnostics, compiledFunction));
            }
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }

    void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
    {
        compiledFunction.References.Add(new Reference<StatementWithValue?>(functionCall, functionCall.File));
        OnGotStatementType(functionCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(functionCall.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{compiledFunction.ToReadable()}\" could not be called due to its protection level", functionCall.Identifier));
            return;
        }

        if (functionCall.MethodArguments.Length != compiledFunction.ParameterCount)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to function \"{compiledFunction.ToReadable()}\": required {compiledFunction.ParameterCount} passed {functionCall.MethodArguments.Length}", functionCall));
            return;
        }

        if (compiledFunction.IsExternal)
        {
            if (!ExternalFunctions.TryGet(compiledFunction.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(functionCall.Identifier));
                AddComment("}");
                return;
            }

            GenerateCodeForFunctionCall_External(functionCall.MethodArguments, functionCall.SaveValue, compiledFunction, externalFunction);
            return;
        }

        if (AllowEvaluating &&
            TryEvaluate(compiledFunction, functionCall.MethodArguments, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            functionCall.PredictedValue = returnValue.Value;
            if (!functionCall.SaveValue)
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Function call fully trimmed", functionCall));
            }
            else
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Function evaluated with result \"{returnValue.Value}\"", functionCall));
                Push(returnValue.Value);
            }
            return;
        }

        if (AllowFunctionInlining &&
            compiledFunction.IsInlineable)
        {
            if (InlineMacro(compiledFunction, functionCall.MethodArguments, out Statement? inlined))
            {
                bool argumentsAreObservable = false;
                foreach (StatementWithValue argument in functionCall.MethodArguments)
                {
                    if (IsObservable(argument))
                    {
                        argumentsAreObservable = true;
                        Diagnostics.Add(Diagnostic.Warning($"Can't inline \"{compiledFunction.ToReadable()}\" because of this argument", argument));
                        break;
                    }
                }

                if (!argumentsAreObservable)
                {
                    ControlFlowUsage controlFlowUsage = inlined is Block _block2 ? FindControlFlowUsage(_block2.Statements) : FindControlFlowUsage(inlined);
                    if (!compiledFunction.ReturnSomething &&
                        controlFlowUsage == ControlFlowUsage.None)
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Function inlined", functionCall));
                        GenerateCodeForStatement(inlined);
                        return;
                    }
                    else if (compiledFunction.ReturnSomething &&
                             controlFlowUsage == ControlFlowUsage.None &&
                             inlined is StatementWithValue statementWithValue)
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Function inlined", functionCall));
                        GeneralType type = FindStatementType(statementWithValue);
                        if (!CanCastImplicitly(type, compiledFunction.Type, statementWithValue, this, out PossibleDiagnostic? castError))
                        { Diagnostics.Add(castError.ToError(statementWithValue)); }
                        GenerateCodeForStatement(inlined);
                        return;
                    }
                    else
                    {
                        Debugger.Break();
                    }
                }
            }
            else
            {
                Diagnostics.Add(Diagnostic.Warning($"Failed to inline \"{compiledFunction.ToReadable()}\"", functionCall));
            }
        }

        AddComment($"Call \"{compiledFunction.ToReadable()}\" {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        if (compiledFunction.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(compiledFunction.Type.GetSize(this, Diagnostics, compiledFunction), false);
            AddComment($"}}");
        }

        parameterCleanup = GenerateCodeForArguments(functionCall.MethodArguments, compiledFunction);

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset, functionCall);

        if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, functionCall, compiledFunction)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            Pop(compiledFunction.Type.GetSize(this, Diagnostics, compiledFunction));
        }

        AddComment("}");
    }

    void GenerateCodeForStatement(AnyCall anyCall)
    {
        if (anyCall.PrevStatement is Identifier _identifier &&
            _identifier.Content == "sizeof")
        {
            _identifier.AnalyzedType = TokenAnalyzedType.Keyword;

            if (anyCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"sizeof\": required {1} passed {anyCall.Arguments.Length}", anyCall));
                return;
            }

            StatementWithValue param = anyCall.Arguments[0];
            GeneralType paramType;
            if (param is TypeStatement typeStatement)
            { paramType = GeneralType.From(typeStatement.Type, FindType, TryCompute); }
            else if (param is CompiledTypeStatement compiledTypeStatement)
            { paramType = compiledTypeStatement.Type; }
            else
            { paramType = FindStatementType(param); }

            OnGotStatementType(anyCall, BuiltinType.I32);

            if (FindSize(paramType, out int size, out PossibleDiagnostic? findSizeError))
            {
                Push(size);
                anyCall.PredictedValue = size;
            }
            else
            {
                using RegisterUsage.Auto reg = Registers.GetFree();
                AddInstruction(Opcode.Move, reg.Get(BuiltinType.I32.GetBitWidth(this)), 0);
                if (!GenerateSize(paramType, reg.Get(BuiltinType.I32.GetBitWidth(this)), out PossibleDiagnostic? generateSizeError))
                { Diagnostics.Add(generateSizeError.ToError(param)); }
                Push(reg.Get(BuiltinType.I32.GetBitWidth(this)));
            }

            return;
        }

        if (GetFunction(anyCall, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? notFound, AddCompilable) &&
            anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            CompiledFunction compiledFunction = result.Function;

            if (anyCall.PrevStatement is Identifier _identifier2)
            { _identifier2.AnalyzedType = TokenAnalyzedType.FunctionName; }
            anyCall.Reference = compiledFunction;
            if (anyCall.PrevStatement is IReferenceableTo<CompiledFunction> _ref1)
            { _ref1.Reference = compiledFunction; }

            GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);

            if (functionCall.CompiledType is not null)
            { OnGotStatementType(anyCall, functionCall.CompiledType); }

            return;
        }

        GeneralType prevType = FindStatementType(anyCall.PrevStatement);
        if (!prevType.Is(out FunctionType? functionType))
        {
            if (notFound is not null)
            {
                Diagnostics.Add(notFound.ToError(anyCall.PrevStatement));
            }
            else
            {
                Diagnostics.Add(Diagnostic.Critical($"This isn't a function", anyCall.PrevStatement));
            }

            return;
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
        {
            if (notFound is not null) Diagnostics.Add(notFound.ToError(anyCall.PrevStatement));
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{functionType}\": required {functionType.Parameters.Length} passed {anyCall.Arguments.Length}", new Position(anyCall.Arguments.As<IPositioned>().Or(anyCall.Brackets)), anyCall.File));
            return;
        }

        PossibleDiagnostic? argumentError = null;
        if (!Utils.SequenceEquals(anyCall.Arguments, functionType.Parameters, (argument, parameter) =>
        {
            GeneralType argumentType = FindStatementType(argument, parameter);

            if (argument.Equals(parameter))
            { return true; }

            if (CodeGenerator.CanCastImplicitly(argumentType, parameter, null, this, out argumentError))
            { return true; }

            argumentError = argumentError.TrySetLocation(argument);

            return false;
        }))
        {
            if (notFound is not null) Diagnostics.Add(notFound.ToError(anyCall.PrevStatement));
            Diagnostics.Add(Diagnostic.Critical(
                $"Argument types of caller \"{anyCall.ToReadable(FindStatementType)}\" doesn't match with callee \"{functionType}\"",
                anyCall,
                argumentError?.ToError(anyCall)));
            return;
        }

        AddComment($"Call (runtime) \"{functionType}\" {{");

        if (functionType.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(functionType.ReturnType.GetSize(this, Diagnostics, anyCall), false);
            AddComment($"}}");
        }

        Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForParameterPassing(anyCall.Arguments, functionType);

        AddComment(" .:");

        CallRuntime(anyCall.PrevStatement);

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (functionType.ReturnSomething && !anyCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            Pop(functionType.ReturnType.GetSize(this, Diagnostics, anyCall));
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(BinaryOperatorCall @operator, GeneralType? expectedType = null)
    {
        if (AllowEvaluating &&
            TryCompute(@operator, out CompiledValue predictedValue))
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{predictedValue}\"", @operator));
            OnGotStatementType(@operator, new BuiltinType(predictedValue.Type));
            @operator.PredictedValue = predictedValue;

            Push(predictedValue);
            return;
        }

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperator>? result, out PossibleDiagnostic? notFoundError))
        {
            CompiledOperator? operatorDefinition = result.Function;

            operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File));
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            if (!operatorDefinition.CanUse(@operator.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Operator \"{operatorDefinition.ToReadable()}\" cannot be called due to its protection level", @operator.Operator, @operator.File));
                return;
            }

            if (BinaryOperatorCall.ParameterCount != operatorDefinition.ParameterCount)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to operator \"{operatorDefinition.ToReadable()}\": required {operatorDefinition.ParameterCount} passed {BinaryOperatorCall.ParameterCount}", @operator));
                return;
            }

            if (operatorDefinition.IsExternal)
            {
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
                {
                    Diagnostics.Add(exception.ToError(@operator.Operator, @operator.File));
                    AddComment("}");
                    return;
                }

                GenerateCodeForFunctionCall_External(@operator.Arguments, @operator.SaveValue, operatorDefinition, externalFunction);
                return;
            }

            AddComment($"Call \"{operatorDefinition.Identifier}\" {{");

            StackAlloc(operatorDefinition.Type.GetSize(this, Diagnostics, operatorDefinition), false);

            Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForArguments(@operator.Arguments, operatorDefinition);

            AddComment(" .:");

            int jumpInstruction = Call(operatorDefinition.InstructionOffset, @operator);

            if (operatorDefinition.InstructionOffset == InvalidFunctionAddress)
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                Pop(operatorDefinition.Type.GetSize(this, Diagnostics, operatorDefinition));
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
                Diagnostics.Add(notFoundError.ToError(@operator));
                return;
            }

            BitWidth leftBitWidth = leftType.GetBitWidth(this, Diagnostics, left);
            BitWidth rightBitWidth = rightType.GetBitWidth(this, Diagnostics, right);
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
                PushFrom(StackTop, leftType.GetSize(this, Diagnostics, left));

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
                PushFrom(StackTop, leftType.GetSize(this, Diagnostics, left));

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
                Diagnostics.Add(notFoundError.ToError(@operator));
                return;
            }

            bool isFloat = leftType.SameAs(BasicType.F32) || rightType.SameAs(BasicType.F32);

            using (RegisterUsage.Auto regLeft = Registers.GetFree())
            using (RegisterUsage.Auto regRight = Registers.GetFree())
            {
                PopTo(regRight.Get(bitWidth), rightBitWidth);
                PopTo(regLeft.Get(bitWidth), leftBitWidth);

                switch (@operator.Operator.Content)
                {
                    case BinaryOperatorCall.Addition:
                        AddInstruction(isFloat ? Opcode.FMathAdd : Opcode.MathAdd, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Subtraction:
                        AddInstruction(isFloat ? Opcode.FMathSub : Opcode.MathSub, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Multiplication:
                        AddInstruction(isFloat ? Opcode.FMathMult : Opcode.MathMult, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Division:
                        AddInstruction(isFloat ? Opcode.FMathDiv : Opcode.MathDiv, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.Modulo:
                        AddInstruction(isFloat ? Opcode.FMathMod : Opcode.MathMod, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.LogicalAND:
                        AddInstruction(Opcode.LogicAND, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.LogicalOR:
                        AddInstruction(Opcode.LogicOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitwiseAND:
                        AddInstruction(Opcode.BitsAND, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitwiseOR:
                        AddInstruction(Opcode.BitsOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitwiseXOR:
                        AddInstruction(Opcode.BitsXOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitshiftLeft:
                        AddInstruction(Opcode.BitsShiftLeft, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case BinaryOperatorCall.BitshiftRight:
                        AddInstruction(Opcode.BitsShiftRight, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;

                    case BinaryOperatorCall.CompEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfEqual, 3);
                        Push(false);
                        AddInstruction(Opcode.Jump, 2);
                        Push(true);
                        break;

                    case BinaryOperatorCall.CompNEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfNotEqual, 3);
                        Push(false);
                        AddInstruction(Opcode.Jump, 2);
                        Push(true);
                        break;

                    case BinaryOperatorCall.CompGT:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLessOrEqual, 3);
                        Push(true);
                        AddInstruction(Opcode.Jump, 2);
                        Push(false);
                        break;

                    case BinaryOperatorCall.CompGEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLess, 3);
                        Push(true);
                        AddInstruction(Opcode.Jump, 2);
                        Push(false);
                        break;

                    case BinaryOperatorCall.CompLT:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLess, 3);
                        Push(false);
                        AddInstruction(Opcode.Jump, 2);
                        Push(true);
                        break;

                    case BinaryOperatorCall.CompLEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        AddInstruction(Opcode.JumpIfLessOrEqual, 3);
                        Push(false);
                        AddInstruction(Opcode.Jump, 2);
                        Push(true);
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
        {
            Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, @operator.File));
        }
    }
    void GenerateCodeForStatement(UnaryOperatorCall @operator)
    {
        if (AllowEvaluating &&
            TryCompute(@operator, out CompiledValue predictedValue))
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{predictedValue}\"", @operator));
            OnGotStatementType(@operator, new BuiltinType(predictedValue.Type));
            @operator.PredictedValue = predictedValue;

            Push(predictedValue);
            return;
        }

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperator>? result, out PossibleDiagnostic? operatorNotFoundError))
        {
            CompiledOperator? operatorDefinition = result.Function;

            operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File));
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            if (!operatorDefinition.CanUse(@operator.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Operator \"{operatorDefinition.ToReadable()}\" cannot be called due to its protection level", @operator.Operator, @operator.File));
                return;
            }

            if (UnaryOperatorCall.ParameterCount != operatorDefinition.ParameterCount)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to operator \"{operatorDefinition.ToReadable()}\": required {operatorDefinition.ParameterCount} passed {UnaryOperatorCall.ParameterCount}", @operator));
                return;
            }

            if (operatorDefinition.IsExternal)
            {
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
                {
                    Diagnostics.Add(exception.ToError(@operator.Operator, @operator.File));
                    AddComment("}");
                    return;
                }

                GenerateCodeForFunctionCall_External(@operator.Arguments, @operator.SaveValue, operatorDefinition, externalFunction);
                return;
            }

            AddComment($"Call \"{operatorDefinition.Identifier}\" {{");

            StackAlloc(operatorDefinition.Type.GetSize(this, Diagnostics, operatorDefinition), false);

            Stack<ParameterCleanupItem> parameterCleanup = GenerateCodeForArguments(@operator.Arguments, operatorDefinition);

            AddComment(" .:");

            int jumpInstruction = Call(operatorDefinition.InstructionOffset, @operator);

            if (operatorDefinition.InstructionOffset == InvalidFunctionAddress)
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                Pop(operatorDefinition.Type.GetSize(this, Diagnostics, operatorDefinition));
            }

            AddComment("}");
        }
        else if (LanguageOperators.UnaryOperators.Contains(@operator.Operator.Content))
        {
            GeneralType leftType = FindStatementType(@operator.Left);
            BitWidth bitWidth = leftType.GetBitWidth(this, Diagnostics, @operator.Left);

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
                        Push(false);
                        AddInstruction(Opcode.Jump, 2);
                        Push(true);
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
                        Push(reg.Get(bitWidth));
                    }

                    return;
                }
                default:
                {
                    Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, @operator.File));
                    return;
                }
            }
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical(
                $"Unknown operator \"{@operator.Operator.Content}\"",
                @operator.Operator,
                @operator.File,
                operatorNotFoundError.ToError(@operator)));
        }
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
                            Push(new CompiledValue((byte)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if (literal.GetInt() is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I8);
                            Push(new CompiledValue((sbyte)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U16))
                    {
                        if (literal.GetInt() is >= ushort.MinValue and <= ushort.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.Char);
                            Push(new CompiledValue((ushort)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if (literal.GetInt() is >= short.MinValue and <= short.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I16);
                            Push(new CompiledValue((short)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.GetInt() >= (int)uint.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U32);
                            Push(new CompiledValue((uint)literal.GetInt()));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I32))
                    {
                        OnGotStatementType(literal, BuiltinType.I32);
                        Push(new CompiledValue(literal.GetInt()));
                        return;
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        OnGotStatementType(literal, BuiltinType.F32);
                        Push(new CompiledValue((float)literal.GetInt()));
                        return;
                    }
                }

                OnGotStatementType(literal, BuiltinType.I32);
                Push(new CompiledValue(literal.GetInt()));
                break;
            }
            case LiteralType.Float:
            {
                OnGotStatementType(literal, BuiltinType.F32);

                Push(new CompiledValue(literal.GetFloat()));
                break;
            }
            case LiteralType.String:
            {
                if (expectedType is not null &&
                    expectedType.Is(out PointerType? pointerType) &&
                    pointerType.To.Is(out ArrayType? arrayType) &&
                    arrayType.Of.SameAs(BasicType.U8))
                {
                    OnGotStatementType(literal, new PointerType(new ArrayType(BuiltinType.U8, LiteralStatement.CreateAnonymous(literal.Value.Length + 1, literal, literal.File), literal.Value.Length + 1)));
                    GenerateCodeForLiteralString(literal.Value, literal.Location, true);
                }
                else
                {
                    OnGotStatementType(literal, new PointerType(new ArrayType(BuiltinType.Char, LiteralStatement.CreateAnonymous(literal.Value.Length + 1, literal, literal.File), literal.Value.Length + 1)));
                    GenerateCodeForLiteralString(literal.Value, literal.Location, false);
                }
                break;
            }
            case LiteralType.Char:
            {
                if (literal.Value.Length != 1)
                {
                    Diagnostics.Add(Diagnostic.Internal($"Literal char contains {literal.Value.Length} characters but only 1 allowed", literal, literal.File));
                    if (literal.Value.Length == 0) break;
                }

                if (expectedType is not null)
                {
                    if (expectedType.SameAs(BasicType.U8))
                    {
                        if ((int)literal.Value[0] is >= byte.MinValue and <= byte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U8);
                            Push(new CompiledValue((byte)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if ((int)literal.Value[0] is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I8);
                            Push(new CompiledValue((sbyte)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U16))
                    {
                        OnGotStatementType(literal, BuiltinType.Char);
                        Push(new CompiledValue(literal.Value[0]));
                        return;
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if ((int)literal.Value[0] is >= short.MinValue and <= short.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I16);
                            Push(new CompiledValue((short)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.Value[0] >= uint.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U32);
                            Push(new CompiledValue((short)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I32))
                    {
                        if (literal.Value[0] >= int.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I32);
                            Push(new CompiledValue((short)literal.Value[0]));
                            return;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        OnGotStatementType(literal, BuiltinType.F32);
                        Push(new CompiledValue((float)literal.Value[0]));
                        return;
                    }
                }

                OnGotStatementType(literal, BuiltinType.Char);
                Push(new CompiledValue(literal.Value[0]));
                break;
            }
            default: throw new UnreachableException();
        }
    }

    void GenerateCodeForLiteralString(string literal, Location location, bool withBytes)
    {
        BuiltinType type = withBytes ? BuiltinType.U8 : BuiltinType.Char;

        AddComment($"Create String \"{literal}\" {{");

        AddComment("Allocate String object {");

        GenerateAllocator(LiteralStatement.CreateAnonymous((1 + literal.Length) * type.GetSize(this), location.Position, location.File));

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
        if (RegisterKeywords.TryGetValue(variable.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            AddInstruction(Opcode.Push, registerKeyword.Register);
            return;
        }

        if (GetConstant(variable.Content, variable.File, out IConstant? constant, out PossibleDiagnostic? constantNotFoundError))
        {
            OnGotStatementType(variable, constant.Type);
            variable.PredictedValue = constant.Value;
            variable.Reference = constant;
            variable.AnalyzedType = TokenAnalyzedType.ConstantName;

            Push(constant.Value);
            return;
        }

        if (GetParameter(variable.Content, out CompiledParameter? param, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (variable.Content != StatementKeywords.This)
            { variable.AnalyzedType = TokenAnalyzedType.ParameterName; }
            variable.Reference = param;
            OnGotStatementType(variable, param.Type);

            Address address = GetParameterAddress(param);

            if (param.IsRef && resolveReference)
            { address = new AddressPointer(address); }

            PushFrom(address, param.Type.GetSize(this, Diagnostics, param));

            return;
        }

        if (GetVariable(variable.Content, out CompiledVariable? val, out PossibleDiagnostic? variableNotFoundError))
        {
            variable.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = val;
            OnGotStatementType(variable, val.Type);

            if (!val.IsInitialized)
            { Diagnostics.Add(Diagnostic.Warning($"U are using the variable \"{val.Identifier}\" but its aint initialized.", variable)); }

            PushFrom(GetLocalVariableAddress(val), val.Type.GetSize(this, Diagnostics, val));

            return;
        }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariable? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            variable.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = globalVariable;
            OnGotStatementType(variable, globalVariable.Type);

            PushFrom(GetGlobalVariableAddress(globalVariable), globalVariable.Type.GetSize(this, Diagnostics, globalVariable));

            return;
        }

        if (GetFunction(variable.Content, expectedType, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? functionNotFoundError))
        {
            CompiledFunction? compiledFunction = result.Function;

            compiledFunction.References.AddReference(variable, variable.File);
            variable.AnalyzedType = TokenAnalyzedType.FunctionName;
            variable.Reference = compiledFunction;
            OnGotStatementType(variable, new FunctionType(compiledFunction));

            if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
            { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(GeneratedCode.Count, true, variable, compiledFunction)); }

            Push(compiledFunction.InstructionOffset);

            return;
        }

        if (GetInstructionLabel(variable.Content, out CompiledInstructionLabel? instructionLabel, out _))
        {
            variable.Reference = instructionLabel;
            OnGotStatementType(variable, CompiledInstructionLabel.Type);

            if (instructionLabel.InstructionOffset == InvalidFunctionAddress)
            { UndefinedInstructionLabels.Add(new UndefinedOffset<CompiledInstructionLabel>(GeneratedCode.Count, true, variable, instructionLabel)); }

            Push(instructionLabel.InstructionOffset);

            return;
        }

        Diagnostics.Add(Diagnostic.Critical(
            $"Symbol \"{variable.Content}\" not found",
            variable,
            constantNotFoundError.ToError(variable),
            parameterNotFoundError.ToError(variable),
            variableNotFoundError.ToError(variable),
            globalVariableNotFoundError.ToError(variable),
            functionNotFoundError.ToError(variable)));
    }
    void GenerateCodeForStatement(AddressGetter addressGetter)
    {
        if (!GetAddress(addressGetter.PrevStatement, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(addressGetter.PrevStatement));
            return;
        }
        GenerateAddressResolver(address);
    }
    void GenerateCodeForStatement(Pointer pointer)
    {
        GenerateCodeForStatement(pointer.PrevStatement);

        GeneralType addressType = FindStatementType(pointer.PrevStatement);
        if (!addressType.Is(out PointerType? pointerType))
        {
            Diagnostics.Add(Diagnostic.Critical($"This isn't a pointer", pointer.PrevStatement));
            return;
        }

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(pointerType.GetBitWidth(this, Diagnostics, pointer)));
            PushFrom(new AddressRegisterPointer(reg.Get(pointerType.GetBitWidth(this, Diagnostics, pointer))), pointerType.To.GetSize(this, Diagnostics, pointer.PrevStatement));
        }
    }
    void GenerateCodeForStatement(WhileLoop whileLoop)
    {
        if (AllowEvaluating &&
            TryCompute(whileLoop.Condition, out CompiledValue condition))
        {
            if (condition)
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"While loop condition evaluated as true", whileLoop.Condition));
                AddComment("while (1) {");

                OnScopeEnter(whileLoop.Block, false);

                int beginOffset = GeneratedCode.Count;

                BreakInstructions.Push(new List<int>());

                GenerateCodeForStatement(whileLoop.Block, true);

                AddComment("Jump Back");
                AddInstruction(Opcode.Jump, beginOffset - GeneratedCode.Count);

                FinishJumpInstructions(BreakInstructions.Last);

                OnScopeExit(whileLoop.Block.Brackets.End.Position, whileLoop.Block.File);

                BreakInstructions.Pop();

                AddComment("}");
            }
            else
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"While loop fully trimmed", whileLoop));
                AddComment("while (0) { }");
            }
            return;
        }

        AddComment("while (...) {");

        OnScopeEnter(whileLoop.Block, false);

        AddComment("Condition");
        int conditionOffset = GeneratedCode.Count;
        GenerateCodeForStatement(whileLoop.Condition);

        GeneralType conditionType = FindStatementType(whileLoop.Condition);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(conditionType.GetBitWidth(this, Diagnostics, whileLoop.Condition)));
            AddInstruction(Opcode.Compare, reg.Get(conditionType.GetBitWidth(this, Diagnostics, whileLoop.Condition)), 0);
            AddInstruction(Opcode.JumpIfEqual, 0);
        }
        int conditionJumpOffset = GeneratedCode.Count - 1;

        BreakInstructions.Push(new List<int>());

        GenerateCodeForStatement(whileLoop.Block, true);

        AddComment("Jump Back");
        AddInstruction(Opcode.Jump, conditionOffset - GeneratedCode.Count);

        FinishJumpInstructions(BreakInstructions.Last);

        GeneratedCode[conditionJumpOffset].Operand1 = GeneratedCode.Count - conditionJumpOffset;

        OnScopeExit(whileLoop.Block.Brackets.End.Position, whileLoop.Block.File);

        AddComment("}");

        BreakInstructions.Pop();
    }
    void GenerateCodeForStatement(ForLoop forLoop)
    {
        AddComment("for (...) {");

        OnScopeEnter(forLoop.Position, forLoop.File, Enumerable.Repeat(forLoop.VariableDeclaration, 1), false);

        AddComment("For-loop variable");
        GenerateCodeForStatement(forLoop.VariableDeclaration);

        if (AllowEvaluating &&
            TryCompute(forLoop.Condition, out CompiledValue condition))
        {
            if (condition)
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"For-loop condition evaluated as true", forLoop.Condition));

                AddComment("For-loop condition");
                int beginOffset = GeneratedCode.Count;

                BreakInstructions.Push(new List<int>());

                GenerateCodeForStatement(forLoop.Block);

                AddComment("For-loop expression");
                GenerateCodeForStatement(forLoop.Expression);

                AddComment("Jump back");
                AddInstruction(Opcode.Jump, beginOffset - GeneratedCode.Count);

                FinishJumpInstructions(BreakInstructions.Pop());

                OnScopeExit(forLoop.Position.After(), forLoop.File);

                AddComment("}");
            }
            else
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"For-loop fully trimmed", forLoop));

                OnScopeExit(forLoop.Position.After(), forLoop.File);

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
            PopTo(reg.Get(conditionType.GetBitWidth(this, Diagnostics, forLoop.Condition)));
            AddInstruction(Opcode.Compare, reg.Get(conditionType.GetBitWidth(this, Diagnostics, forLoop.Condition)), 0);
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

        OnScopeExit(forLoop.Position.After(), forLoop.File);

        AddComment("}");
    }
    void GenerateCodeForStatement(IfContainer @if)
    {
        List<int> jumpOutInstructions = new();

        foreach (BaseBranch ifSegment in @if.Branches)
        {
            if (ifSegment is IfBranch partIf)
            {
                if (AllowEvaluating &&
                    TryCompute(partIf.Condition, out CompiledValue condition))
                {
                    if (!condition)
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"If branch fully trimmed", partIf));

                        AddComment("if (0) { }");
                        continue;
                    }
                    else
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"If condition evaluated as true", partIf.Condition));

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
                    PopTo(reg.Get(conditionType.GetBitWidth(this, Diagnostics, partIf.Condition)));
                    AddInstruction(Opcode.Compare, reg.Get(conditionType.GetBitWidth(this, Diagnostics, partIf.Condition)), 0);
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
                if (AllowEvaluating &&
                    TryCompute(partElseif.Condition, out CompiledValue condition))
                {
                    if (!condition)
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Else-if branch fully trimmed", partElseif));

                        AddComment("elseif (0) { }");
                        continue;
                    }
                    else
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Else-if condition evaluated as ture", partElseif.Condition));

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
                    PopTo(reg.Get(conditionType.GetBitWidth(this, Diagnostics, partElseif.Condition)));
                    AddInstruction(Opcode.Compare, reg.Get(conditionType.GetBitWidth(this, Diagnostics, partElseif.Condition)), 0);
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
    void GenerateCodeForStatement(NewInstance newInstance)
    {
        AddComment($"new \"{newInstance.Type}\" {{");

        GeneralType instanceType = GeneralType.From(newInstance.Type, FindType, TryCompute);

        newInstance.Type.SetAnalyzedType(instanceType);
        OnGotStatementType(newInstance, instanceType);

        switch (instanceType)
        {
            case PointerType pointerType:
            {
                GenerateAllocator(new AnyCall(
                    new Identifier(Token.CreateAnonymous("sizeof"), newInstance.File),
                    new CompiledTypeStatement[] { new(Token.CreateAnonymous(StatementKeywords.Type), pointerType.To, newInstance.File) },
                    Array.Empty<Token>(),
                    TokenPair.CreateAnonymous(newInstance.Position, "(", ")"),
                    newInstance.File
                ));

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
                structType.Struct.References.AddReference(newInstance.Type, newInstance.File);

                StackAlloc(structType.GetSize(this, Diagnostics, newInstance), true);
                break;
            }

            case ArrayType arrayType:
            {
                StackAlloc(arrayType.GetSize(this, Diagnostics, newInstance), true);
                break;
            }

            default:
            {
                Diagnostics.Add(Diagnostic.Critical($"Unknown type \"{instanceType}\"", newInstance.Type, newInstance.File));
                break;
            }
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(ConstructorCall constructorCall)
    {
        GeneralType instanceType = GeneralType.From(constructorCall.Type, FindType, TryCompute);
        ImmutableArray<GeneralType> parameters = FindStatementTypes(constructorCall.Arguments);

        if (!GetConstructor(instanceType, parameters, constructorCall.File, out FunctionQueryResult<CompiledConstructor>? result, out PossibleDiagnostic? notFound, AddCompilable))
        {
            Diagnostics.Add(notFound.ToError(constructorCall.Type, constructorCall.File));
            return;
        }

        CompiledConstructor? compiledFunction = result.Function;

        compiledFunction.References.AddReference(constructorCall);
        OnGotStatementType(constructorCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(constructorCall.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Constructor \"{compiledFunction.ToReadable()}\" could not be called due to its protection level", constructorCall.Type, constructorCall.File));
            return;
        }

        AddComment($"Call \"{compiledFunction.ToReadable()}\" {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        NewInstance newInstance = constructorCall.ToInstantiation();
        GenerateCodeForStatement(newInstance);

        AddComment(" Pass arguments (\"this\" is already passed):");

        parameterCleanup = GenerateCodeForArguments(constructorCall.Arguments, compiledFunction, 1);

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset, constructorCall);

        if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
        { UndefinedConstructorOffsets.Add(new UndefinedOffset<CompiledConstructor>(jumpInstruction, false, constructorCall, compiledFunction)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }
    void GenerateCodeForStatement(Field field)
    {
        field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType.Is(out ArrayType? arrayType) && field.Identifier.Content == "Length")
        {
            if (arrayType.Length is null)
            {
                Diagnostics.Add(Diagnostic.Critical("Array type's length isn't defined", field));
                return;
            }

            if (!arrayType.ComputedLength.HasValue)
            {
                Diagnostics.Add(Diagnostic.Critical("I will eventually implement this", field));
                return;
            }

            OnGotStatementType(field, ArrayLengthType);
            field.PredictedValue = arrayType.ComputedLength.Value;

            Push(arrayType.ComputedLength.Value);
            return;
        }

        if (prevType.Is(out PointerType? pointerType2))
        {
            GenerateCodeForStatement(field.PrevStatement);
            CheckPointerNull(field.PrevStatement.Location);
            prevType = pointerType2.To;

            while (prevType.Is(out pointerType2))
            {
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(PointerBitWidth));
                    PushFrom(new AddressRegisterPointer(
                        reg.Get(PointerBitWidth)),
                        PointerSize
                    );
                }
                CheckPointerNull(field.PrevStatement.Location);
                prevType = pointerType2.To;
            }

            if (!prevType.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{prevType}\"", field.PrevStatement));
                return;
            }

            if (!structPointerType.GetField(field.Identifier.Content, this, out CompiledField? fieldDefinition, out int fieldOffset, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(field.Identifier, field.File));
                return;
            }

            field.CompiledType = fieldDefinition.Type;
            field.Reference = fieldDefinition;
            fieldDefinition.References.AddReference(field);

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(PointerBitWidth));
                PushFrom(new AddressOffset(
                    new AddressRegisterPointer(reg.Get(PointerBitWidth)),
                    fieldOffset
                    ), fieldDefinition.Type.GetSize(this, Diagnostics, fieldDefinition));
            }
            return;
        }

        if (!prevType.Is(out StructType? structType)) throw new NotImplementedException();

        GeneralType type = FindStatementType(field);

        if (!structType.GetField(field.Identifier.Content, this, out CompiledField? compiledField, out _, out PossibleDiagnostic? error2))
        {
            Diagnostics.Add(error2.ToError(field.Identifier, field.File));
            return;
        }

        field.CompiledType = compiledField.Type;
        field.Reference = compiledField;
        compiledField.References.AddReference(field);

        // if (CurrentContext?.Context != null)
        // {
        //     switch (compiledField.Protection)
        //     {
        //         case Protection.Private:
        //             if (CurrentContext.Context.Identifier.Content != compiledField.Class.Identifier.Content)
        //             { Diagnostics.Add(Diagnostic.Critical($"Can not access field \"{compiledField.Identifier.Content}\" of class \"{compiledField.Class.Identifier}\" due to it's protection level", field.Identifier, field.File); }
        //             break;
        //         case Protection.Public:
        //             break;
        //         default: throw new UnreachableException();
        //     }
        // }

        // TODO: what the hell is that

        StatementWithValue? dereference = NeedDereference(field);

        if (!GetAddress(field, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(field));
            return;
        }

        if (dereference is null)
        { PushFrom(address, type.GetSize(this, Diagnostics, field)); }
        else
        { PushFrom(address, type.GetSize(this, Diagnostics, field), dereference.Location); }
    }
    void GenerateCodeForStatement(IndexCall index)
    {
        GeneralType prevType = FindStatementType(index.PrevStatement);
        GeneralType indexType = FindStatementType(index.Index);

        if (GetIndexGetter(prevType, indexType, index.File, out FunctionQueryResult<CompiledFunction>? indexer, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            GenerateCodeForFunctionCall_Function(new FunctionCall(
                    index.PrevStatement,
                    new(Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerGet), index.File),
                    ImmutableArray.Create(index.Index),
                    index.Brackets,
                    index.File
                ), indexer.Function);
            return;
        }

        if (prevType.Is(out ArrayType? arrayType))
        {
            if (index.PrevStatement is not Identifier identifier)
            { throw new NotSupportedException($"Only variables/parameters supported by now", index.PrevStatement); }

            if (TryCompute(index.Index, out CompiledValue computedIndexData))
            {
                index.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || (arrayType.ComputedLength.HasValue && computedIndexData >= arrayType.ComputedLength.Value))
                { Diagnostics.Add(Diagnostic.Warning($"Index out of range", index.Index)); }

                if (GetParameter(identifier.Content, out CompiledParameter? param, out _))
                {
                    if (!param.Type.SameAs(arrayType))
                    { throw new NotImplementedException(); }

                    Address offset = GetParameterAddress(param, (int)computedIndexData * arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement));
                    PushFrom(offset, arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement));

                    throw new NotImplementedException();
                }

                if (GetVariable(identifier.Content, out CompiledVariable? val, out _))
                {
                    if (!val.Type.SameAs(arrayType))
                    { throw new NotImplementedException(); }

                    // TODO: this
                    // if (!val.IsInitialized)
                    // { AnalysisCollection?.Warnings.Add(Diagnostic.Warning($"U are using the variable \"{val.Identifier}\" but its aint initialized.", identifier, identifier.File)); }

                    int offset = (int)computedIndexData * arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement);
                    Address address = GetLocalVariableAddress(val);

                    PushFrom(new AddressOffset(address, offset), arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement));

                    return;
                }
            }

            {
                using (RegisterUsage.Auto regPtr = Registers.GetFree())
                {
                    if (!GetAddress(identifier, out Address? address, out PossibleDiagnostic? error))
                    {
                        Diagnostics.Add(error.ToError(identifier));
                        return;
                    }
                    GenerateAddressResolver(address);
                    PopTo(regPtr.Get(PointerBitWidth));

                    GenerateCodeForStatement(index.Index);

                    if (!indexType.Is<BuiltinType>())
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Index must be a builtin type (i.e. int) and not \"{indexType}\"", index.Index));
                        return;
                    }

                    using (RegisterUsage.Auto regIndex = Registers.GetFree())
                    {
                        PopTo(regIndex.Get(indexType.GetBitWidth(this, Diagnostics, index.Index)));
                        AddInstruction(Opcode.MathMult, regIndex.Get(indexType.GetBitWidth(this, Diagnostics, index.Index)), arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement));
                        AddInstruction(Opcode.MathAdd, regPtr.Get(PointerBitWidth), regIndex.Get(indexType.GetBitWidth(this, Diagnostics, index.Index)));
                    }

                    PushFrom(new AddressRegisterPointer(regPtr.Get(PointerBitWidth)), arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement));
                }
                return;
            }

            throw new NotImplementedException();
        }

        if (prevType.Is(out PointerType? pointerType) && pointerType.To.Is(out arrayType))
        {
            int elementSize = arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement);
            if (!GetAddress(index, out Address? _address, out PossibleDiagnostic? _error))
            {
                Diagnostics.Add(_error.ToError(index));
                return;
            }
            PushFrom(_address, elementSize);
            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"Index getter for type \"{prevType}\" not found", index));
    }

    void GenerateAddressResolver(Address address)
    {
        switch (address)
        {
            case AddressPointer runtimePointer:
            {
                PushFrom(runtimePointer.PointerAddress, PointerSize);
                break;
            }
            case AddressRegisterPointer registerPointer:
            {
                Push(registerPointer.Register);
                break;
            }
            case AddressOffset addressOffset:
            {
                GenerateAddressResolver(addressOffset.Base);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(PointerBitWidth));
                    AddInstruction(Opcode.MathAdd, reg.Get(PointerBitWidth), addressOffset.Offset);
                    Push(reg.Get(PointerBitWidth));
                }
                break;
            }
            case AddressRuntimePointer runtimePointer:
            {
                GenerateCodeForStatement(runtimePointer.PointerValue);
                CheckPointerNull(runtimePointer.PointerValue.Location);
                break;
            }
            case AddressRuntimeIndex runtimeIndex:
            {
                GenerateAddressResolver(runtimeIndex.Base);

                GeneralType indexType = FindStatementType(runtimeIndex.IndexValue);

                if (!indexType.Is<BuiltinType>())
                {
                    Diagnostics.Add(Diagnostic.Critical($"Index type must be builtin (ie. \"int\") and not \"{indexType}\"", runtimeIndex.IndexValue));
                    return;
                }

                GenerateCodeForStatement(runtimeIndex.IndexValue);

                using (RegisterUsage.Auto regIndex = Registers.GetFree())
                {
                    PopTo(regIndex.Get(indexType.GetBitWidth(this, Diagnostics, runtimeIndex.IndexValue)));
                    AddInstruction(Opcode.MathMult, regIndex.Get(indexType.GetBitWidth(this, Diagnostics, runtimeIndex.IndexValue)), runtimeIndex.ElementSize);
                    using (RegisterUsage.Auto regBase = Registers.GetFree())
                    {
                        PopTo(regBase.Get(PointerBitWidth));
                        AddInstruction(Opcode.MathAdd, regBase.Get(PointerBitWidth), regIndex.Get(indexType.GetBitWidth(this, Diagnostics, runtimeIndex.IndexValue)));
                        Push(regBase.Get(PointerBitWidth));
                    }
                }

                break;
            }
            default: throw new NotImplementedException();
        }
    }

    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Content == ModifierKeywords.Ref)
        {
            throw new NotImplementedException();
        }

        if (modifier.Content == ModifierKeywords.Temp)
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

        if (statementType.Equals(targetType))
        { Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Keyword, typeCast.File)); }

        if (statementType.GetSize(this, Diagnostics, typeCast.PrevStatement) != targetType.GetSize(this, Diagnostics, typeCast))
        {
            Diagnostics.Add(Diagnostic.Critical($"Can't convert \"{statementType}\" ({statementType.GetSize(this, Diagnostics, typeCast.PrevStatement)} bytes) to \"{targetType}\" ({targetType.GetSize(this, Diagnostics, typeCast)} bytes)", typeCast.Keyword, typeCast.File));
            return;
        }

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

        if (statementType.Equals(targetType))
        {
            Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Type, typeCast.File));
            GenerateCodeForStatement(typeCast.PrevStatement);
            return;
        }

        if (AllowEvaluating &&
            targetType.Is(out BuiltinType? targetBuiltinType) &&
            TryComputeSimple(typeCast.PrevStatement, out CompiledValue prevValue) &&
            prevValue.TryCast(targetBuiltinType.RuntimeType, out CompiledValue castedValue))
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Type cast evaluated, converting {prevValue} to {castedValue}", typeCast));

            Push(castedValue);
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

        int statementSize = statementType.GetSize(this, Diagnostics, typeCast.PrevStatement);
        int targetSize = targetType.GetSize(this, Diagnostics, new Location(typeCast.Type.Position, typeCast.File));

        if (statementSize != targetSize)
        {
            if (statementSize < targetSize)
            {
                AddComment($"Grow \"{statementType}\" ({statementSize} bytes) to \"{targetType}\" ({targetSize}) {{");

                AddComment("Make space");

                StackAlloc(targetSize, true);

                AddComment("Value");

                GenerateCodeForStatement(typeCast.PrevStatement);

                AddComment("Save");

                for (int i = 0; i < statementSize; i++)
                { PopTo(Register.StackPointer.ToPtr((statementSize - 1) * -BytecodeProcessor.StackDirection, BitWidth._8), BitWidth._8); }

                AddComment("}");

                return;
            }
            else if (statementSize > targetSize)
            {
                AddComment($"Shrink \"{statementType}\" ({statementSize} bytes) to \"{targetType}\" ({targetSize}) {{");

                AddComment("Make space");

                StackAlloc(targetSize, false);

                AddComment("Value");

                GenerateCodeForStatement(typeCast.PrevStatement);

                AddComment("Save");

                for (int i = 0; i < targetSize; i++)
                { PopTo(Register.StackPointer.ToPtr((statementSize - 1) * -BytecodeProcessor.StackDirection, BitWidth._8), BitWidth._8); }

                AddComment("Discard excess");

                int excess = statementSize - targetSize;
                Pop(excess);

                AddComment("}");

                return;
            }

            Diagnostics.Add(Diagnostic.Critical($"Can't modify the size of the value. You tried to convert from \"{statementType}\" (size of {statementSize}) to \"{targetType}\" (size of {targetSize})", typeCast));
            return;
        }

        GenerateCodeForStatement(typeCast.PrevStatement, targetType);
    }

    void GenerateCodeForStatement(Block block, bool ignoreScope = false)
    {
        if (!Settings.DontOptimize &&
            block.Statements.Length == 0)
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Empty block", block));
            AddComment("Statements { }");
            return;
        }

        if (ignoreScope)
        {
            AddComment("Statements {");
            for (int i = 0; i < block.Statements.Length; i++)
            { GenerateCodeForStatement(block.Statements[i]); }
            AddComment("}");

            return;
        }

        OnScopeEnter(block, false);

        AddComment("Statements {");
        for (int i = 0; i < block.Statements.Length; i++)
        { GenerateCodeForStatement(block.Statements[i]); }
        AddComment("}");

        OnScopeExit(block.Brackets.End.Position, block.File);
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
            case InstructionLabel v: GenerateCodeForStatement(v); break;
            default: throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\"");
        }

        if (startInstruction != GeneratedCode.Count)
        {
            DebugInfo?.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (startInstruction, GeneratedCode.Count - 1),
                Location = statement.Location,
            });
        }
    }

    static bool IsStringOnStack(GeneralType type, StatementWithValue? value, [NotNullWhen(true)] out LiteralStatement? literal, out PossibleDiagnostic? error)
    {
        error = null;
        if (type.Is(out ArrayType? arrayType) &&
            arrayType.Of.SameAs(BasicType.U16) &&
            value is LiteralStatement literalStatement &&
            literalStatement.Type == LiteralType.String &&
            arrayType.ComputedLength.HasValue)
        {
            if (literalStatement.Value.Length != arrayType.ComputedLength.Value)
            {
                error = new PossibleDiagnostic($"String literal's length ({literalStatement.Value.Length}) aint match with the array's length ({arrayType.ComputedLength})", literalStatement);
            }
            literal = literalStatement;
            return true;
        }
        else
        {
            literal = null;
            return false;
        }
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

    void CleanupVariables(ImmutableArray<CleanupItem> cleanupItems, Location location, bool justGenerateCode)
    {
        if (cleanupItems.Length == 0) return;
        AddComment("Clear Variables");
        for (int i = cleanupItems.Length - 1; i >= 0; i--)
        {
            CleanupVariables(cleanupItems[i], location, justGenerateCode);
        }
    }

    void CleanupVariables(CleanupItem cleanupItem, Location location, bool justGenerateCode)
    {
        if (cleanupItem.ShouldDeallocate)
        {
            if (cleanupItem.Type is null) throw new InternalExceptionWithoutContext();
            GenerateDestructor(cleanupItem.Type, location);
        }
        else
        {
            Pop(cleanupItem.SizeOnStack);
        }

        if (!justGenerateCode) CompiledLocalVariables.Pop();
    }

    void GenerateCodeForValueSetter(Statement statementToSet, StatementWithValue value)
    {
        switch (statementToSet)
        {
            case Identifier v: GenerateCodeForValueSetter(v, value); break;
            case Field v: GenerateCodeForValueSetter(v, value); break;
            case IndexCall v: GenerateCodeForValueSetter(v, value); break;
            case Pointer v: GenerateCodeForValueSetter(v, value); break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"The left side of the assignment operator should be a variable, field or memory address. Passed \"{statementToSet.GetType().Name}\"", statementToSet));
                return;
        }
    }
    void GenerateCodeForValueSetter(Identifier statementToSet, StatementWithValue value)
    {
        if (RegisterKeywords.TryGetValue(statementToSet.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            GeneralType valueType = FindStatementType(value, registerKeyword.Type);

            if (!CanCastImplicitly(valueType, registerKeyword.Type, value, out PossibleDiagnostic? castError))
            { Diagnostics.Add(castError.ToError(value)); }

            GenerateCodeForStatement(value, registerKeyword.Type);
            PopTo(registerKeyword.Register);
            return;
        }

        if (GetConstant(statementToSet.Content, statementToSet.File, out _, out _))
        {
            statementToSet.AnalyzedType = TokenAnalyzedType.ConstantName;
            Diagnostics.Add(Diagnostic.Critical($"Can not set constant value: it is readonly", statementToSet));
            return;
        }

        if (GetParameter(statementToSet.Content, out CompiledParameter? parameter, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (statementToSet.Content != StatementKeywords.This)
            { statementToSet.AnalyzedType = TokenAnalyzedType.ParameterName; }
            statementToSet.CompiledType = parameter.Type;
            statementToSet.Reference = parameter;

            GeneralType valueType = FindStatementType(value, parameter.Type);

            if (!CanCastImplicitly(valueType, parameter.Type, value, out PossibleDiagnostic? castError))
            {
                Diagnostics.Add(castError.ToError(value));
            }

            GenerateCodeForStatement(value, parameter.Type);

            Address address = GetParameterAddress(parameter);

            if (parameter.IsRef)
            { address = new AddressPointer(address); }

            PopTo(address, parameter.Type.GetSize(this, Diagnostics, statementToSet));
            return;
        }

        if (GetVariable(statementToSet.Content, out CompiledVariable? variable, out PossibleDiagnostic? variableNotFoundError))
        {
            statementToSet.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = variable.Type;
            statementToSet.Reference = variable;

            GeneralType valueType = FindStatementType(value, variable.Type);

            if (variable.InitialValue is not null &&
                IsStringOnStack(variable.Type, variable.InitialValue, out LiteralStatement? literal, out PossibleDiagnostic? error))
            {
                if (error is not null) Diagnostics.Add(error.ToError(value));
                variable.IsInitialized = true;

                for (int i = 0; i < literal.Value.Length; i++)
                { Push(new CompiledValue(literal.Value[i])); }
            }
            else
            {
                if (!CanCastImplicitly(valueType, variable.Type, value, out PossibleDiagnostic? castError))
                {
                    Diagnostics.Add(castError.ToError(value));
                }

                GenerateCodeForStatement(value, variable.Type);
            }

            PopTo(GetLocalVariableAddress(variable), variable.Type.GetSize(this, Diagnostics, statementToSet));
            variable.IsInitialized = true;
            return;
        }

        if (GetGlobalVariable(statementToSet.Content, statementToSet.File, out CompiledVariable? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            statementToSet.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = globalVariable.Type;
            statementToSet.Reference = globalVariable;

            GeneralType valueType = FindStatementType(value, globalVariable.Type);

            if (globalVariable.InitialValue is not null &&
                IsStringOnStack(globalVariable.Type, globalVariable.InitialValue, out LiteralStatement? literal, out PossibleDiagnostic? error))
            {
                if (error is not null) Diagnostics.Add(error.ToError(literal));

                globalVariable.IsInitialized = true;

                for (int i = 0; i < literal.Value.Length; i++)
                { Push(new CompiledValue(literal.Value[i])); }
            }
            else
            {
                if (!CanCastImplicitly(valueType, globalVariable.Type, value, out PossibleDiagnostic? castError))
                {
                    Diagnostics.Add(castError.ToError(value));
                }

                GenerateCodeForStatement(value, globalVariable.Type);
            }

            PopTo(GetGlobalVariableAddress(globalVariable), globalVariable.Type.GetSize(this, Diagnostics, globalVariable));
            return;
        }

        Diagnostics.Add(Diagnostic.Critical(
            $"Symbol \"{statementToSet.Content}\" not found",
            statementToSet,
            parameterNotFoundError.ToError(statementToSet),
            variableNotFoundError.ToError(statementToSet),
            globalVariableNotFoundError.ToError(statementToSet)));
    }
    void GenerateCodeForValueSetter(Field statementToSet, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType type = FindStatementType(statementToSet);
        GeneralType valueType = FindStatementType(value, type);

        if (prevType.Is(out PointerType? pointerType))
        {
            if (!pointerType.To.Is(out StructType? structType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Failed to get the field offsets of type \"{pointerType.To}\"", statementToSet.PrevStatement));
                return;
            }

            if (!structType.GetField(statementToSet.Identifier.Content, this, out CompiledField? fieldDefinition, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(statementToSet.Identifier, statementToSet.File));
                return;
            }

            statementToSet.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;
            statementToSet.Reference = fieldDefinition;
            fieldDefinition.References.AddReference(statementToSet);

            if (!CanCastImplicitly(valueType, fieldDefinition.Type, value, out PossibleDiagnostic? castError1))
            {
                Diagnostics.Add(castError1.ToError(value));
            }

            GenerateCodeForStatement(value, type);
            PopTo(statementToSet.PrevStatement, fieldOffset, valueType.GetSize(this, Diagnostics, value));
            return;
        }

        if (!prevType.Is<StructType>())
        { throw new NotImplementedException(); }

        if (!CanCastImplicitly(valueType, type, value, out PossibleDiagnostic? castError2))
        {
            Diagnostics.Add(castError2.ToError(value));
        }

        GenerateCodeForStatement(value, type);

        StatementWithValue? dereference = NeedDereference(statementToSet);

        if (dereference is null)
        {
            if (!GetAddress(statementToSet, out Address? address, out PossibleDiagnostic? error2))
            {
                Diagnostics.Add(error2.ToError(statementToSet));
                return;
            }
            PopTo(address, valueType.GetSize(this, Diagnostics, value));
        }
        else
        {
            if (!GetAddress(statementToSet, out Address? address, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(statementToSet));
                return;
            }
            PopTo(address, valueType.GetSize(this, Diagnostics, value), dereference.Location);
        }
    }
    void GenerateCodeForValueSetter(IndexCall statementToSet, StatementWithValue value)
    {
        GeneralType itemType = FindStatementType(statementToSet);
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType indexType = FindStatementType(statementToSet.Index);
        GeneralType valueType = FindStatementType(value);

        if (GetIndexSetter(prevType, valueType, indexType, statementToSet.File, out FunctionQueryResult<CompiledFunction>? indexer, out PossibleDiagnostic? indexerNotFoundError, AddCompilable))
        {
            GenerateCodeForFunctionCall_Function(new FunctionCall(
                statementToSet.PrevStatement,
                new(Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerSet), statementToSet.File),
                ImmutableArray.Create<StatementWithValue>(statementToSet.Index, value),
                statementToSet.Brackets,
                statementToSet.File
            ), indexer.Function);
            return;
        }

        if (!CanCastImplicitly(valueType, itemType, value, out PossibleDiagnostic? castError))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        if (!GetAddress(statementToSet, out Address? _address, out PossibleDiagnostic? _error))
        {
            Diagnostics.Add(_error.ToError(statementToSet));
            Diagnostics.Add(indexerNotFoundError.ToError(statementToSet));
            return;
        }
        GenerateCodeForStatement(value);
        PopTo(_address, valueType.GetSize(this, Diagnostics, value));
        return;
    }
    void GenerateCodeForValueSetter(Pointer statementToSet, StatementWithValue value)
    {
        GeneralType targetType = FindStatementType(statementToSet);
        GeneralType valueType = FindStatementType(value, targetType);
        GeneralType pointerValueType = FindStatementType(statementToSet.PrevStatement);

        if (!CanCastImplicitly(valueType, targetType, value, out PossibleDiagnostic? castError))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        if (pointerValueType.GetBitWidth(this, Diagnostics, statementToSet.PrevStatement) != PointerBitWidth)
        {
            Diagnostics.Add(Diagnostic.Critical($"Type \"{pointerValueType}\" cant be a pointer", statementToSet.PrevStatement));
            return;
        }

        GenerateCodeForStatement(value, targetType);

        GenerateCodeForStatement(statementToSet.PrevStatement);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), valueType.GetSize(this, Diagnostics, value));
        }
    }

    #endregion

    void OnScopeEnter(Block block, bool isFunction) => OnScopeEnter(block.Position, block.File, block.Statements.OfType<VariableDeclaration>(), isFunction);

    void OnScopeEnter(Position position, Uri file, IEnumerable<VariableDeclaration> variables, bool isFunction)
    {
        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                Location = new Location(position, file),
            },
            Stack = new List<StackElementInformation>(),
        });

        AddComment("Scope enter");

        CompileLocalConstants(variables);

        CleanupStack.Push(new Scope(
            CompileVariables(variables, CurrentContext is null),
            isFunction
        ));
    }

    void OnScopeExit(Position position, Uri file)
    {
        AddComment("Scope exit");

        CleanupVariables(CleanupStack.Pop().Variables, new Location(position, file), false);

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

        foreach (CompiledVariable other in CompiledLocalVariables)
        {
            if (other.Identifier.Content != newVariable.Identifier.Content) continue;
            Diagnostics.Add(Diagnostic.Warning($"Variable \"{other.Identifier}\" already defined", newVariable.Identifier));
        }

        GeneralType type;
        if (newVariable.Type == StatementKeywords.Var)
        {
            if (newVariable.InitialValue == null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Initial value for variable declaration with implicit type is required", newVariable));
                return default;
            }

            type = FindStatementType(newVariable.InitialValue);
        }
        else
        {
            type = GeneralType.From(newVariable.Type, FindType, TryCompute);

            newVariable.Type.SetAnalyzedType(type);
            newVariable.CompiledType = type;
        }

        int offset = (VariablesSize + type.GetSize(this, Diagnostics, newVariable)) * BytecodeProcessor.StackDirection;

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        StackElementInformation debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Identifier = compiledVariable.Identifier.Content,
            Address = offset,
            BasePointerRelative = true,
            Size = compiledVariable.Type.GetSize(this, Diagnostics, compiledVariable),
            Type = compiledVariable.Type
        };
        newVariable.CompiledType = compiledVariable.Type;

        if (CurrentScopeDebug.Count > 0) CurrentScopeDebug.Last.Stack.Add(debugInfo);

        CompiledLocalVariables.Add(compiledVariable);

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        int size;

        if (AllowEvaluating &&
            TryCompute(newVariable.InitialValue, out CompiledValue computedInitialValue))
        {
            if (computedInitialValue.TryCast(compiledVariable.Type, out CompiledValue castedInitialValue))
            { computedInitialValue = castedInitialValue; }

            Diagnostics.Add(Diagnostic.OptimizationNotice($"Variable initial value evaluated as {castedInitialValue}", newVariable.InitialValue));

            newVariable.InitialValue.PredictedValue = computedInitialValue;

            AddComment($"Initial value {{");

            size = new BuiltinType(computedInitialValue.Type).GetSize(this, Diagnostics, newVariable.InitialValue);

            Push(computedInitialValue);
            compiledVariable.IsInitialized = true;

            if (size <= 0)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable has a size of {size}", newVariable));
                return default;
            }

            if (newVariable.InitialValue is not null)
            {
                GeneralType initialValueType = FindStatementType(newVariable.InitialValue);
                if (!CanCastImplicitly(initialValueType, type, newVariable.InitialValue, out PossibleDiagnostic? castError))
                { Diagnostics.Add(castError.ToError(newVariable.InitialValue)); }
            }

            AddComment("}");
        }
        else if (compiledVariable.Type.Is(out ArrayType? arrayType) &&
            arrayType.Of.SameAs(BasicType.U16) &&
            newVariable.InitialValue is LiteralStatement literalStatement &&
            literalStatement.Type == LiteralType.String &&
            arrayType.ComputedLength.HasValue &&
            literalStatement.Value.Length == arrayType.ComputedLength.Value)
        {
            size = arrayType.GetSize(this, Diagnostics, compiledVariable);
            compiledVariable.IsInitialized = true;

            for (int i = 0; i < literalStatement.Value.Length; i++)
            {
                Push(new CompiledValue(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = compiledVariable.Type.GetSize(this, Diagnostics, compiledVariable);
            StackAlloc(size, false);

            if (size <= 0)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable has a size of {size}", newVariable));
                return default;
            }

            if (newVariable.InitialValue is not null)
            {
                GeneralType initialValueType = FindStatementType(newVariable.InitialValue);
                if (!CanCastImplicitly(initialValueType, type, newVariable.InitialValue, out PossibleDiagnostic? castError))
                { Diagnostics.Add(castError.ToError(newVariable.InitialValue)); }
            }

            AddComment("}");
        }

        if (size != compiledVariable.Type.GetSize(this, Diagnostics, compiledVariable))
        { Diagnostics.Add(Diagnostic.Internal($"Variable size ({compiledVariable.Type.GetSize(this, Diagnostics, compiledVariable)}) and initial value size ({size}) mismatch", newVariable.InitialValue!)); }

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

    #region GenerateCodeForInstructionLabel

    void GenerateCodeForInstructionLabel(InstructionLabel instructionLabel)
    {
        foreach (CompiledInstructionLabel other in CompiledInstructionLabels)
        {
            if (other.Identifier.Content != instructionLabel.Identifier.Content) continue;
            Diagnostics.Add(Diagnostic.Warning($"Instruction label \"{other.Identifier}\" already defined", instructionLabel.Identifier));
        }

        CompiledInstructionLabels.Add(new CompiledInstructionLabel(
            InvalidFunctionAddress,
            instructionLabel
        ));
    }
    void GenerateCodeForInstructionLabel(Statement statement)
    {
        if (statement is InstructionLabel instructionLabel)
        { GenerateCodeForInstructionLabel(instructionLabel); }
    }
    void GenerateCodeForInstructionLabel(IEnumerable<Statement> statements)
    {
        foreach (Statement statement in statements)
        {
            GenerateCodeForInstructionLabel(statement);
        }
    }

    #endregion

    #region GenerateCodeFor...

    int VariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariable variable in CompiledLocalVariables)
            { sum += variable.Type.GetSize(this, Diagnostics, variable); }
            return sum;
        }
    }

    int GlobalVariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariable variable in CompiledGlobalVariables)
            { sum += variable.Type.GetSize(this, Diagnostics, variable); }
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
        {
            Diagnostics.Add(Diagnostic.Critical($"The identifier \"{function.Identifier}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.File));
            return default;
        }

        if (function.Identifier is not null)
        { function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName; }

        if (function is FunctionDefinition functionDefinition)
        {
            for (int i = 0; i < functionDefinition.Attributes.Length; i++)
            {
                if (functionDefinition.Attributes[i].Identifier.Content == AttributeConstants.ExternalIdentifier)
                { return false; }
            }
        }

        Print?.Invoke($"Generate \"{function.ToReadable()}\" ...", LogType.Debug);

        if (function.Identifier is not null)
        { AddComment(function.Identifier.Content + ((function.Parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }")); }
        else
        { AddComment("null" + ((function.Parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }")); }

        CurrentContext = function as IDefinition;
        InFunction = true;

        CompiledParameters.Clear();
        CompiledLocalVariables.Clear();
        ReturnInstructions.Clear();
        ScopeSizes.Push(0);
        int savedInstructionLabelCount = CompiledInstructionLabels.Count;

        CompileParameters(function.Parameters);

        int instructionStart = GeneratedCode.Count;

        if (function is IHaveCompiledType functionWithType)
        { CurrentReturnType = functionWithType.Type; }
        else
        { CurrentReturnType = BuiltinType.Void; }

        if (function.Block is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{function.ToReadable()}\" does not have a body", function));
            return default;
        }

        GenerateCodeForInstructionLabel(function.Block.GetStatementsRecursively(false));

        OnScopeEnter(function.Block, true);

        if (function is IHaveCompiledType returnType && !returnType.Type.SameAs(BasicType.Void))
        {
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = GetReturnValueAddress(returnType.Type).Offset,
                BasePointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = returnType.Type.GetSize(this, Diagnostics, function),
                Identifier = "Return Value",
                Type = returnType.Type,
            });
        }

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = SavedBasePointerOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = BasePointerSize,
            Identifier = "Saved BasePointer",
            Type = BasePointerType,
        });

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = SavedCodePointerOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = CodePointerSize,
            Identifier = "Saved CodePointer",
            Type = CodePointerType,
        });

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = AbsoluteGlobalOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = AbsGlobalAddressSize,
            Identifier = "Absolute Global Offset",
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
                Size = p.IsRef ? PointerSize : p.Type.GetSize(this, Diagnostics, p),
                Identifier = p.Identifier.Content,
                Type = p.IsRef ? new PointerType(p.Type) : p.Type,
            };
            CurrentScopeDebug.Last.Stack.Add(debugInfo);
        }

        ReturnInstructions.Push(new List<int>());

        AddComment("Statements {");
        for (int i = 0; i < function.Block.Statements.Length; i++)
        { GenerateCodeForStatement(function.Block.Statements[i]); }
        AddComment("}");

        CurrentReturnType = null;

        OnScopeExit(function.Block.Brackets.End.Position, function.Block.File);

        FinishJumpInstructions(ReturnInstructions.Last);
        ReturnInstructions.Pop();

        AddComment("Return");
        Return(function.Block.Location.After());

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

        SetUndefinedFunctionOffsets(UndefinedInstructionLabels, false);

        while (CompiledInstructionLabels.Count > savedInstructionLabelCount)
        {
            CompiledInstructionLabels.Pop();
        }

        CompiledParameters.Clear();
        CompiledLocalVariables.Clear();
        ReturnInstructions.Clear();
        if (ScopeSizes.Pop() != 0) { } // throw new InternalException("Bruh", function.Block!, function.File);

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

    void GenerateCodeForTopLevelStatements(ImmutableArray<Statement> statements, Uri file)
    {
        if (statements.IsDefaultOrEmpty) return;

        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                Location = new Location(new Position(statements), file),
            },
            Stack = new List<StackElementInformation>(),
        });

        ScopeSizes.Push(0);

        AddComment("TopLevelStatements {");

        AddComment("Create stack frame");
        Push(Register.BasePointer);
        AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
        {
            Address = SavedBasePointerOffset,
            BasePointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = BasePointerSize,
            Identifier = "Saved BasePointer",
            Type = BasePointerType,
        });

        CurrentContext = null;
        ReturnInstructions.Push(new List<int>());

        CurrentReturnType = ExitCodeType;

        /*
        AddComment("Variables");
        CleanupStack.Push(new Scope(
            GenerateCodeForLocalVariable(statements),
            true
        ));
        */

        AddComment("Statements {");
        foreach (Statement statement in statements)
        { GenerateCodeForStatement(statement); }
        AddComment("}");

        /*
        AddComment("Save global variables {");
        foreach (CompiledVariable variable in CompiledVariables)
        {
            if (!GetGlobalVariable(variable.Identifier.Content, variable.File, out CompiledVariable? globalVariable, out _))
            { continue; }
            if (!variable.Type.Equals(globalVariable.Type))
            { continue; }
            PushFrom(GetLocalVariableAddress(variable), variable.Type.GetSize(this, Diagnostics, variable));
            PopTo(GetGlobalVariableAddress(globalVariable), globalVariable.Type.GetSize(this, Diagnostics, globalVariable));
        }
        AddComment("}");
        */

        // CompiledGlobalVariables.AddRange(CompiledVariables);
        // CleanupVariables(CleanupStack.Pop().Variables, new Location(statements[^1].Position.NextLine(), statements[^1].File), false);

        FinishJumpInstructions(ReturnInstructions.Last);
        ReturnInstructions.Pop();

        CurrentReturnType = null;

        AddComment("Pop stack frame");
        PopTo(Register.BasePointer);
        if (ScopeSizes.Pop() != 0) { } // throw new InternalException("Bruh");

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
        ScopeSizes.Push(0);

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
                Location = new Location(Position.UnknownPosition, compilerResult.File),
            },
            Stack = new List<StackElementInformation>(),
        });

        {
            // Exit code

            AddComment("Push exit code");
            Push(new CompiledValue(0));

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = (ExitCodeType.GetSize(this) - 1) * BytecodeProcessor.StackDirection,
                BasePointerRelative = false,
                Kind = StackElementKind.Internal,
                Size = ExitCodeType.GetSize(this),
                Identifier = "Exit Code",
                Type = ExitCodeType,
            });
        }

        IEnumerable<InstructionLabel> globalInstructionLabels = compilerResult.TopLevelStatements
            .Select(v => v.Statements)
            .Aggregate(Enumerable.Empty<Statement>(), (a, b) => a.Concat(b))
            .Select(v => v as InstructionLabel)
            .Where(v => v is not null)!;

        GenerateCodeForInstructionLabel(globalInstructionLabels);

        IEnumerable<VariableDeclaration> globalVariableDeclarations = compilerResult.TopLevelStatements
            .Select(v => v.Statements)
            .Aggregate(Enumerable.Empty<Statement>(), (a, b) => a.Concat(b))
            .Select(v => v as VariableDeclaration)
            .Where(v => v is not null)
            .Where(v => !v!.Modifiers.Contains(ModifierKeywords.Const))!;

        Stack<CleanupItem> globalVariablesCleanup = new();
        foreach (VariableDeclaration variableDeclaration in globalVariableDeclarations)
        {
            GeneralType type;
            if (variableDeclaration.Type == StatementKeywords.Var)
            {
                if (variableDeclaration.InitialValue == null)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Initial value for variable declaration with implicit type is required", variableDeclaration));
                    return default;
                }

                type = FindStatementType(variableDeclaration.InitialValue);
            }
            else
            {
                type = GeneralType.From(variableDeclaration.Type, FindType, TryCompute);

                variableDeclaration.Type.SetAnalyzedType(type);
                variableDeclaration.CompiledType = type;
            }

            int size = type.GetSize(this, Diagnostics, variableDeclaration);
            int currentOffset = GlobalVariablesSize;

            CompiledVariable variable = CompileVariable(variableDeclaration, currentOffset);
            CompiledGlobalVariables.Add(variable);
            CleanupItem cleanupItem = new(size, variable.Modifiers.Contains(ModifierKeywords.Temp), variable.Type);
            globalVariablesCleanup.Insert(0, cleanupItem);
        }

        AddComment("Allocate global variables {");
        StackAlloc(GlobalVariablesSize, false);
        AddComment("}");

        {
            // Absolute global offset

            AddComment("Abs global address");

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                // AddInstruction(Opcode.Move, reg.Get(PointerBitWidth), Register.StackPointer);
                // AddInstruction(Opcode.MathAdd, reg.Get(PointerBitWidth), GlobalVariablesSize);
                AddInstruction(Opcode.Push, Register.StackPointer);
            }

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = AbsoluteGlobalOffset,
                BasePointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = AbsGlobalAddressSize,
                Identifier = "Absolute Global Offset",
                Type = AbsGlobalAddressType,
            });
        }

        for (int i = 0; i < compilerResult.TopLevelStatements.Length - 1; i++)
        {
            (ImmutableArray<Statement> statements, Uri file) = compilerResult.TopLevelStatements[i];
            Print?.Invoke($"Generating top level statements for file \"{file}\" ...", LogType.Debug);
            GenerateCodeForTopLevelStatements(statements, file);
        }

        if (compilerResult.TopLevelStatements.Length > 0)
        {
            (ImmutableArray<Statement> statements, Uri file) = compilerResult.TopLevelStatements[^1];
            Print?.Invoke($"Generating top level statements for file \"{file}\" ...", LogType.Debug);
            if (compilerResult.IsInteractive &&
                statements.Length == 1 &&
                statements[0] is StatementWithValue statementWithValue)
            {
                GeneralType type = FindStatementType(statementWithValue);
                if (type.SameAs(BasicType.I32))
                {
                    statements = ImmutableArray.Create<Statement>(
                        new KeywordCall(
                            (Token)StatementKeywords.Return,
                            Enumerable.Repeat(statementWithValue, 1),
                            statementWithValue.File
                        )
                    );
                }
            }
            GenerateCodeForTopLevelStatements(statements, file);
        }

        AddComment("Pop abs global address");
        Pop(AbsGlobalAddressType.GetSize(this)); // Pop abs global offset

        AddComment("Cleanup global variables {");
        CleanupVariables(globalVariablesCleanup.ToImmutableArray(), default, true);
        AddComment("}");

        AddInstruction(Opcode.Exit); // Exit code already there

        // 4 -> exit code
        if (ScopeSizes.Pop() != 4) { } // throw new InternalException("Bruh");

        // foreach (CompiledVariable variable in CompiledGlobalVariables)
        // {
        //     int absoluteGlobalAddress = ExitCodeType.GetSize(this) + GlobalVariablesSize;
        //     CurrentScopeDebug.LastRef.Stack.Add(new StackElementInformation()
        //     {
        //         Address = -(absoluteGlobalAddress + variable.MemoryAddress - 1),
        //         BasePointerRelative = false,
        //         Kind = StackElementKind.GlobalVariable,
        //         Size = variable.Type.GetSize(this, Diagnostics, variable),
        //         Identifier = variable.Identifier.Content,
        //         Type = variable.Type,
        //     });
        // }

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

        SetUndefinedFunctionOffsets(UndefinedFunctionOffsets, true);
        SetUndefinedFunctionOffsets(UndefinedConstructorOffsets, true);
        SetUndefinedFunctionOffsets(UndefinedOperatorFunctionOffsets, true);
        SetUndefinedFunctionOffsets(UndefinedGeneralFunctionOffsets, true);
        SetUndefinedFunctionOffsets(UndefinedInstructionLabels, true);

        // {
        //     ScopeInformation scope = CurrentScopeDebug.Pop();
        //     scope.Location.Instructions.End = GeneratedCode.Count - 1;
        //     DebugInfo?.ScopeInformation.Add(scope);
        // }

        Print?.Invoke("Code generated", LogType.Debug);

        return new BBLangGeneratorResult()
        {
            Code = GeneratedCode.Select(v => new Instruction(v)).ToImmutableArray(),
            DebugInfo = DebugInfo,
        };
    }
}
