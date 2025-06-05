using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    bool AllowInstructionLevelOptimizations => !Settings.DontOptimize;

    GeneratorStatistics _statistics;

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
            {
                _statistics.InstructionLevelOptimizations++;
                return;
            }

            if ((instruction.Opcode is
                Opcode.MathAdd or
                Opcode.MathSub) &&
                instruction.Operand2 == 0)
            {
                _statistics.InstructionLevelOptimizations++;
                return;
            }
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

    void GenerateDeallocator(CompiledCleanup cleanup)
    {
        if (cleanup.Deallocator is null)
        {
            return;
        }
        CompiledFunctionDefinition? deallocator = cleanup.Deallocator;

        if (deallocator.ExternalFunctionName is not null)
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

        AddComment(" .:");

        int jumpInstruction = Call(deallocator.InstructionOffset, cleanup.Location);

        if (deallocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(jumpInstruction, false, cleanup.Location, deallocator)); }

        if (deallocator.ReturnSomething)
        {
            AddComment($" Clear return value:");

            // TODO: wtf?
            const int returnValueSize = 0;
            Pop(returnValueSize);
        }

        AddComment("}");
    }

    void GenerateDestructor(CompiledCleanup cleanup)
    {
        GeneralType deallocateableType = cleanup.TrashType;

        if (cleanup.TrashType.Is<PointerType>())
        {
            if (cleanup.Destructor is null)
            {
                AddComment($"Pointer value should be already there");
                GenerateDeallocator(cleanup);

                return;
            }
        }
        else
        {
            if (cleanup.Destructor is null)
            {
                return;
            }
        }

        AddComment(" Param0 should be already there");

        AddComment(" .:");

        int jumpInstruction = Call(cleanup.Destructor.InstructionOffset, cleanup.Location);

        if (cleanup.Destructor.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(jumpInstruction, false, cleanup.Location, cleanup.Destructor)); }

        if (deallocateableType.Is<PointerType>())
        {
            GenerateDeallocator(cleanup);
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

        GeneralType lengthType = type.Length.Type;
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

    void GenerateCodeForStatement(CompiledVariableDeclaration newVariable)
    {
        // if (newVariable.Getters.Count == 0 &&
        //     newVariable.Setters.Count == 0)
        // {
        //     if (newVariable.InitialValue is not null)
        //     {
        //         GenerateCodeForStatement(newVariable.InitialValue);
        //         Pop(newVariable.InitialValue.Type.GetSize(this, Diagnostics, newVariable.InitialValue));
        //     }
        //     return;
        // }

        if (newVariable.InitialValue == null) return;

        AddComment($"New Variable \"{newVariable.Identifier}\" {{");

        if (newVariable.InitialValue is CompiledLiteralList literalList)
        {
            for (int i = 0; i < literalList.Values.Length; i++)
            {
                CompiledStatementWithValue value = literalList.Values[i];
                GenerateCodeForValueSetter(new CompiledIndexSetter()
                {
                    Base = new CompiledVariableGetter()
                    {
                        Variable = newVariable,
                        Location = newVariable.Location,
                        SaveValue = true,
                        Type = newVariable.Type,
                    },
                    Index = new CompiledEvaluatedValue()
                    {
                        Value = i,
                        Location = value.Location,
                        SaveValue = true,
                        Type = BuiltinType.I32,
                    },
                    IsCompoundAssignment = false,
                    Location = value.Location,
                    Value = value,
                });
            }
            AddComment("}");
            return;
        }

        GenerateCodeForValueSetter(new CompiledVariableSetter()
        {
            Variable = newVariable,
            Value = newVariable.InitialValue,
            Location = newVariable.Location,
            IsCompoundAssignment = false,
        });
        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledInstructionLabelDeclaration instructionLabel)
    {
        foreach (ControlFlowFrame v in ReturnInstructions) v.IsSkipping = false;
        foreach (ControlFlowFrame v in BreakInstructions) v.IsSkipping = false;

        if (!GeneratedInstructionLabels.TryGetValue(instructionLabel, out GeneratedInstructionLabel? generatedInstructionLabel))
        {
            generatedInstructionLabel = GeneratedInstructionLabels[instructionLabel] = new()
            {
                InstructionOffset = GeneratedCode.Count,
            };
        }
        generatedInstructionLabel.InstructionOffset = GeneratedCode.Count;
    }
    void GenerateCodeForStatement(CompiledReturn keywordCall)
    {
        AddComment($"Return {{");

        if (!CanReturn)
        {
            Diagnostics.Add(Diagnostic.Critical($"Can't return for some reason", keywordCall.Location));
            return;
        }

        if (keywordCall.Value is not null)
        {
            AddComment(" Param 0:");

            CompiledStatementWithValue returnValue = keywordCall.Value;
            GeneralType returnValueType = returnValue.Type;

            GenerateCodeForStatement(returnValue);

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
            for (int i = CleanupStack2.Count - 1; i >= 0; i--)
            {
                CleanupVariables(CleanupStack2[i].Variables, keywordCall.Location, true);
                if (CleanupStack2[i].IsFunction) break;
            }
            AddComment("}");
        }

        ReturnInstructions.Last.Offsets.Add(GeneratedCode.Count);
        ReturnInstructions.Last.IsSkipping = true;
        AddInstruction(Opcode.Jump, 0);

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledCrash keywordCall)
    {
        CompiledStatementWithValue throwValue = keywordCall.Value;
        GeneralType throwType = throwValue.Type;

        if (throwValue is CompiledStringInstance literalThrowValue)
        {
            _statistics.Optimizations++;
            Diagnostics.Add(Diagnostic.OptimizationNotice("String allocated on stack", throwValue));
            for (int i = literalThrowValue.Value.Length - 1; i >= 0; i--)
            {
                Push(new InstructionOperand(
                    literalThrowValue.Value[i],
                    InstructionOperandType.Immediate16
                ));
            }
            AddInstruction(Opcode.Crash, Register.StackPointer);
        }
        else
        {
            GenerateCodeForStatement(throwValue);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(throwType.GetBitWidth(this, Diagnostics, throwValue)));
                AddInstruction(Opcode.Crash, reg.Get(throwType.GetBitWidth(this, Diagnostics, throwValue)));
            }
        }
    }
    void GenerateCodeForStatement(CompiledBreak keywordCall)
    {
        if (BreakInstructions.Count == 0)
        {
            Diagnostics.Add(Diagnostic.Critical($"The keyword \"{StatementKeywords.Break}\" does not available in the current context", keywordCall));
            return;
        }

        BreakInstructions.Last.Offsets.Add(GeneratedCode.Count);
        ReturnInstructions.Last.IsSkipping = true;
        AddInstruction(Opcode.Jump, 0);
    }
    void GenerateCodeForStatement(CompiledDelete compiledDelete)
    {
        GenerateCodeForStatement(compiledDelete.Value);
        GenerateDestructor(compiledDelete.Cleanup);
        Pop(compiledDelete.Value.Type.GetSize(this, Diagnostics, compiledDelete));
    }
    void GenerateCodeForStatement(CompiledGoto keywordCall)
    {
        GenerateCodeForStatement(keywordCall.Value);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            AddInstruction(Opcode.MathSub, reg.Get(PointerBitWidth), GeneratedCode.Count + 1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.Jump, reg.Get(PointerBitWidth));
        }
    }
    Stack<CompiledCleanup> GenerateCodeForArguments(IReadOnlyList<CompiledPassedArgument> arguments, ICompiledFunctionDefinition compiledFunction, int alreadyPassed = 0)
    {
        Stack<CompiledCleanup> argumentCleanup = new();

        for (int i = 0; i < arguments.Count; i++)
        {
            CompiledPassedArgument argument = arguments[i];
            GeneralType argumentType = argument.Value.Type;
            ParameterDefinition parameter = compiledFunction.Parameters[i + alreadyPassed];
            GeneralType parameterType = compiledFunction.ParameterTypes[i + alreadyPassed];

            if (argumentType.GetSize(this, Diagnostics, argument.Value) != parameterType.GetSize(this, Diagnostics, parameter))
            { Diagnostics.Add(Diagnostic.Internal($"Bad argument type passed: expected \"{parameterType}\" passed \"{argumentType}\"", argument.Value)); }

            AddComment($" Pass {parameter}:");

            GenerateCodeForStatement(argument.Value, parameterType);

            argumentCleanup.Push(argument.Cleanup);
        }

        return argumentCleanup;
    }
    Stack<CompiledCleanup> GenerateCodeForParameterPassing(IReadOnlyList<CompiledPassedArgument> parameters, FunctionType function)
    {
        Stack<CompiledCleanup> parameterCleanup = new();

        for (int i = 0; i < parameters.Count; i++)
        {
            AddComment($" Param {i}:");
            GenerateCodeForStatement(parameters[i].Value, function.Parameters[i]);
            parameterCleanup.Push(parameters[i].Cleanup);
        }

        return parameterCleanup;
    }
    void GenerateCodeForParameterCleanup(Stack<CompiledCleanup> parameterCleanup)
    {
        AddComment(" Clear Params:");
        while (parameterCleanup.Count > 0)
        {
            CompiledCleanup passedParameter = parameterCleanup.Pop();
            GenerateDestructor(passedParameter);
            Pop(passedParameter.TrashType.GetSize(this, Diagnostics, passedParameter));
        }
    }
    void GenerateCodeForFunctionCall_MSIL(CompiledExternalFunctionCall caller)
    {
        AddComment($"Call \"{((ISimpleReadable)caller.Declaration).ToReadable()}\" {{");

        if (caller.Function.ReturnValueSize > 0 && caller.SaveValue)
        {
            AddComment($"Initial return value {{");
            StackAlloc(caller.Function.ReturnValueSize, false);
            AddComment($"}}");
        }

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Declaration);

        AddComment(" .:");

        AddInstruction(Opcode.CallMSIL, caller.Function.Id);
        int conditionalJumpInstruction;
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Register8L);
            AddInstruction(Opcode.Compare, reg.Register8L, 0);
            conditionalJumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JumpIfNotEqual, 0);
        }

        int jumpInstruction = Call(caller.Declaration.InstructionOffset, caller);

        if (caller.Declaration.InstructionOffset == InvalidFunctionAddress)
        {
            UndefinedFunctionOffsets.Add(new(jumpInstruction, false, caller, caller.Declaration));
        }
        AddInstruction(Opcode.HotFuncEnd, caller.Function.Id);

        GeneratedCode[conditionalJumpInstruction].Operand1 = GeneratedCode.Count - conditionalJumpInstruction;

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (caller.Function.ReturnValueSize > 0 && !caller.SaveValue)
        {
            AddComment($" Clear return value:");
            Pop(caller.Function.ReturnValueSize);
        }

        AddComment("}");
    }
    void GenerateCodeForFunctionCall_External(CompiledExternalFunctionCall caller)
    {
        AddComment($"Call \"{((ISimpleReadable)caller.Declaration).ToReadable()}\" {{");

        if (caller.Function.ReturnValueSize > 0 && caller.SaveValue)
        {
            AddComment($"Initial return value {{");
            StackAlloc(caller.Function.ReturnValueSize, false);
            AddComment($"}}");
        }

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Declaration);

        AddComment(" .:");
        AddInstruction(Opcode.CallExternal, caller.Function.Id);

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (caller.Function.ReturnValueSize > 0 && !caller.SaveValue)
        {
            AddComment($" Clear return value:");
            Pop(caller.Function.ReturnValueSize);
        }

        AddComment("}");
    }
    void GenerateCodeForFunctionCall(CompiledFunctionCall caller)
    {
        if (ILGenerator is not null)
        {
            CompiledFunction? f = Functions.FirstOrDefault(v => v.Function == caller.Function);
            if (f is not null)
            {
                ILGenerator.Diagnostics.Clear();
                if (ILGenerator.GenerateImplMarshaled(f, out ExternalFunctionScopedSyncCallback? method))
                {
                    if (ILGenerator.Diagnostics.Has(DiagnosticsLevel.Error))
                    {
                        goto anyway;
                    }

                    int returnValueSize = f.Function.ReturnSomething ? f.Function.Type.GetSize(this) : 0;
                    int parametersSize = f.Function.ParameterTypes.Aggregate(0, (a, b) => a + b.GetSize(this));
                    int id = ExternalFunctions.Concat(GeneratedUnmanagedFunctions.Select(v => (IExternalFunction)v.Function).AsEnumerable()).GenerateId();

                    ExternalFunctionScopedSync externFunc;
#if UNITY_BURST
                    UnityEngine.Debug.LogWarning($"Function {method.Method} compiled into machine code !!!");
                    IntPtr ptr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(method);
                    unsafe { externFunc = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)ptr, id, parametersSize, returnValueSize, 0, ExternalFunctionScopedSyncFlags.MSILPointerMarshal); }
#else
                    Debug.WriteLine($"Function {method.Method} compiled into machine code !!!");
                    externFunc = new(method, id, parametersSize, returnValueSize, 0, ExternalFunctionScopedSyncFlags.MSILPointerMarshal);
#endif
                    GeneratedUnmanagedFunctions.Add((externFunc, method));

                    GenerateCodeForFunctionCall_MSIL(new()
                    {
                        Declaration = f.Function,
                        Function = externFunc,

                        Arguments = caller.Arguments,
                        Location = caller.Location,
                        SaveValue = caller.SaveValue,
                        Type = caller.Type,
                    });

                    Diagnostics.Add(Diagnostic.OptimizationNotice($"Function \"{f.Function}\" compiled into MSIL", caller));

                    Diagnostics.AddRange(ILGenerator.Diagnostics);

                    ILGenerator.Diagnostics.Clear();
                    return;
                anyway:;
                }
                Diagnostics.Add(Diagnostic.Warning($"Failed to generate MSIL for function {f.Function}", caller, ILGenerator.Diagnostics.Diagnostics.Where(v => v.Level == DiagnosticsLevel.Error).ToArray()));
                ILGenerator.Diagnostics.Clear();
            }
        }

        AddComment($"Call \"{((ISimpleReadable)caller.Function).ToReadable()}\" {{");

        if (caller.Function.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(caller.Function.Type.GetSize(this, Diagnostics, caller), false);
            AddComment($"}}");
        }

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Function);

        AddComment(" .:");

        int jumpInstruction = Call(caller.Function.InstructionOffset, caller);

        if (caller.Function.InstructionOffset == InvalidFunctionAddress)
        {
            UndefinedFunctionOffsets.Add(new(jumpInstruction, false, caller, caller.Function));
        }

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (caller.Function.ReturnSomething && !caller.SaveValue)
        {
            AddComment(" Clear Return Value:");
            Pop(caller.Function.Type.GetSize(this, Diagnostics, caller));
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledSizeof anyCall)
    {
        GeneralType paramType = anyCall.Of;

        if (FindSize(paramType, out int size, out _))
        {
            Push(size);
        }
        else
        {
            using RegisterUsage.Auto reg = Registers.GetFree();
            AddInstruction(Opcode.Move, reg.Get(BuiltinType.I32.GetBitWidth(this)), 0);
            if (!GenerateSize(paramType, reg.Get(BuiltinType.I32.GetBitWidth(this)), out PossibleDiagnostic? generateSizeError))
            { Diagnostics.Add(generateSizeError.ToError(anyCall)); }
            Push(reg.Get(BuiltinType.I32.GetBitWidth(this)));
        }
    }
    void GenerateCodeForStatement(CompiledRuntimeCall anyCall)
    {
        GeneralType prevType = anyCall.Function.Type;
        if (!prevType.Is(out FunctionType? functionType))
        {
            Diagnostics.Add(Diagnostic.Critical($"This isn't a function", anyCall.Function));
            return;
        }

        if (anyCall.Arguments.Length != functionType.Parameters.Length)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{functionType}\": required {functionType.Parameters.Length} passed {anyCall.Arguments.Length}", anyCall));
            return;
        }

        PossibleDiagnostic? argumentError = null;
        if (!Utils.SequenceEquals(anyCall.Arguments, functionType.Parameters, (argument, parameter) =>
        {
            GeneralType argumentType = argument.Type;

            if (argument.Equals(parameter))
            { return true; }

            if (StatementCompiler.CanCastImplicitly(argumentType, parameter, null, out argumentError))
            { return true; }

            argumentError = argumentError.TrySetLocation(argument);

            return false;
        }))
        {
            Diagnostics.Add(Diagnostic.Critical(
                $"Argument types of caller \"{anyCall}\" doesn't match with callee \"{functionType}\"",
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

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForParameterPassing(anyCall.Arguments, functionType);

        AddComment(" .:");

        CallRuntime(anyCall.Function);

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (functionType.ReturnSomething && !anyCall.SaveValue)
        {
            AddComment(" Clear Return Value:");
            Pop(functionType.ReturnType.GetSize(this, Diagnostics, anyCall));
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledBinaryOperatorCall @operator)
    {
        BitWidth leftBitWidth = @operator.Left.Type.GetBitWidth(this, Diagnostics, @operator.Left);
        BitWidth rightBitWidth = @operator.Right.Type.GetBitWidth(this, Diagnostics, @operator.Right);
        BitWidth bitWidth = StatementCompiler.MaxBitWidth(leftBitWidth, rightBitWidth);

        int jumpInstruction = InvalidFunctionAddress;

        GenerateCodeForStatement(@operator.Left);

        if (@operator.Operator == CompiledBinaryOperatorCall.LogicalAND)
        {
            PushFrom(StackTop, @operator.Left.Type.GetSize(this, Diagnostics, @operator.Left));

            using (RegisterUsage.Auto regLeft = Registers.GetFree())
            {
                PopTo(regLeft.Get(leftBitWidth));
                AddInstruction(Opcode.Compare, regLeft.Get(leftBitWidth), 0);
                jumpInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.JumpIfEqual, 0);
            }
        }
        else if (@operator.Operator == CompiledBinaryOperatorCall.LogicalOR)
        {
            PushFrom(StackTop, @operator.Left.Type.GetSize(this, Diagnostics, @operator.Left));

            using (RegisterUsage.Auto regLeft = Registers.GetFree())
            {
                PopTo(regLeft.Get(leftBitWidth));
                AddInstruction(Opcode.Compare, regLeft.Get(leftBitWidth), 0);

                jumpInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.JumpIfNotEqual, 0);
            }
        }

        GenerateCodeForStatement(@operator.Right);

        bool isFloat = @operator.Left.Type.SameAs(BasicType.F32) || @operator.Right.Type.SameAs(BasicType.F32);

        using (RegisterUsage.Auto regLeft = Registers.GetFree())
        using (RegisterUsage.Auto regRight = Registers.GetFree())
        {
            PopTo(regRight.Get(bitWidth), rightBitWidth);
            PopTo(regLeft.Get(bitWidth), leftBitWidth);

            switch (@operator.Operator)
            {
                case CompiledBinaryOperatorCall.Addition:
                    AddInstruction(isFloat ? Opcode.FMathAdd : Opcode.MathAdd, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.Subtraction:
                    AddInstruction(isFloat ? Opcode.FMathSub : Opcode.MathSub, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.Multiplication:
                    AddInstruction(isFloat ? Opcode.FMathMult : Opcode.MathMult, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.Division:
                    AddInstruction(isFloat ? Opcode.FMathDiv : Opcode.MathDiv, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.Modulo:
                    AddInstruction(isFloat ? Opcode.FMathMod : Opcode.MathMod, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.LogicalAND:
                    AddInstruction(Opcode.LogicAND, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.LogicalOR:
                    AddInstruction(Opcode.LogicOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.BitwiseAND:
                    AddInstruction(Opcode.BitsAND, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.BitwiseOR:
                    AddInstruction(Opcode.BitsOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.BitwiseXOR:
                    AddInstruction(Opcode.BitsXOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.BitshiftLeft:
                    AddInstruction(Opcode.BitsShiftLeft, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;
                case CompiledBinaryOperatorCall.BitshiftRight:
                    AddInstruction(Opcode.BitsShiftRight, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    Push(regLeft.Get(bitWidth));
                    break;

                case CompiledBinaryOperatorCall.CompEQ:
                    AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    AddInstruction(Opcode.JumpIfEqual, 3);
                    Push(false);
                    AddInstruction(Opcode.Jump, 2);
                    Push(true);
                    break;

                case CompiledBinaryOperatorCall.CompNEQ:
                    AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    AddInstruction(Opcode.JumpIfNotEqual, 3);
                    Push(false);
                    AddInstruction(Opcode.Jump, 2);
                    Push(true);
                    break;

                case CompiledBinaryOperatorCall.CompGT:
                    AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    AddInstruction(Opcode.JumpIfLessOrEqual, 3);
                    Push(true);
                    AddInstruction(Opcode.Jump, 2);
                    Push(false);
                    break;

                case CompiledBinaryOperatorCall.CompGEQ:
                    AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    AddInstruction(Opcode.JumpIfLess, 3);
                    Push(true);
                    AddInstruction(Opcode.Jump, 2);
                    Push(false);
                    break;

                case CompiledBinaryOperatorCall.CompLT:
                    AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                    AddInstruction(Opcode.JumpIfLess, 3);
                    Push(false);
                    AddInstruction(Opcode.Jump, 2);
                    Push(true);
                    break;

                case CompiledBinaryOperatorCall.CompLEQ:
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
    void GenerateCodeForStatement(CompiledUnaryOperatorCall @operator)
    {
        GeneralType leftType = @operator.Left.Type;
        BitWidth bitWidth = leftType.GetBitWidth(this, Diagnostics, @operator.Left);

        switch (@operator.Operator)
        {
            case CompiledUnaryOperatorCall.LogicalNOT:
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
            case CompiledUnaryOperatorCall.BinaryNOT:
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
            case CompiledUnaryOperatorCall.UnaryMinus:
            {
                GenerateCodeForStatement(@operator.Left);

                bool isFloat = @operator.Left.Type.SameAs(BasicType.F32);

                using (RegisterUsage.Auto left = Registers.GetFree())
                using (RegisterUsage.Auto right = Registers.GetFree())
                {
                    PopTo(right.Get(bitWidth));
                    AddInstruction(Opcode.Move, left.Get(bitWidth), new InstructionOperand(isFloat ? new CompiledValue(0f) : new CompiledValue(0)));

                    AddInstruction(isFloat ? Opcode.FMathSub : Opcode.MathSub, left.Get(bitWidth), right.Get(bitWidth));

                    Push(left.Get(bitWidth));
                }

                return;
            }
            case CompiledUnaryOperatorCall.UnaryPlus:
            {
                GenerateCodeForStatement(@operator.Left);
                return;
            }
            default:
            {
                Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator}\"", @operator));
                return;
            }
        }
    }
    void GenerateCodeForStatement(CompiledEvaluatedValue literal, GeneralType? expectedType = null)
    {
        Push(literal.Value);
    }
    void GenerateCodeForStatement(CompiledStringInstance stringInstance)
    {
        BuiltinType type = stringInstance.IsASCII ? BuiltinType.U8 : BuiltinType.Char;

        AddComment($"Create String \"{stringInstance.Value}\" {{");

        AddComment("Allocate String object {");

        GenerateCodeForStatement(stringInstance.Allocator);

        AddComment("}");

        AddComment("Set string data {");

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            // Save pointer
            AddInstruction(Opcode.Move, reg.Get(PointerBitWidth), (InstructionOperand)StackTop);

            if (stringInstance.IsASCII)
            {
                for (int i = 0; i < stringInstance.Value.Length; i++)
                {
                    AddInstruction(Opcode.Move, reg.Get(PointerBitWidth).ToPtr(i * type.GetSize(this), type.GetBitWidth(this)), (byte)stringInstance.Value[i]);
                }

                AddInstruction(Opcode.Move, reg.Get(PointerBitWidth).ToPtr(stringInstance.Value.Length * type.GetSize(this), type.GetBitWidth(this)), (byte)'\0');
            }
            else
            {
                for (int i = 0; i < stringInstance.Value.Length; i++)
                {
                    AddInstruction(Opcode.Move, reg.Get(PointerBitWidth).ToPtr(i * type.GetSize(this), type.GetBitWidth(this)), stringInstance.Value[i]);
                }

                AddInstruction(Opcode.Move, reg.Get(PointerBitWidth).ToPtr(stringInstance.Value.Length * type.GetSize(this), type.GetBitWidth(this)), '\0');
            }
        }

        AddComment("}");

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledStackStringInstance stringInstance)
    {
        if (stringInstance.IsNullTerminated)
        {
            if (stringInstance.IsASCII) Push(new CompiledValue(default(byte)));
            else Push(new CompiledValue(default(char)));
        }
        for (int i = stringInstance.Value.Length - 1; i >= 0; i--)
        {
            if (stringInstance.IsASCII) Push(new CompiledValue((byte)stringInstance.Value[i]));
            else Push(new CompiledValue(stringInstance.Value[i]));
        }
    }
    void GenerateCodeForStatement(RegisterGetter variable, GeneralType? expectedType = null, bool resolveReference = true)
    {
        AddInstruction(Opcode.Push, variable.Register);
    }
    void GenerateCodeForStatement(CompiledParameterGetter variable, GeneralType? expectedType = null, bool resolveReference = true)
    {
        Address address = GetParameterAddress(variable.Variable);

        if (variable.Variable.IsRef && resolveReference)
        { address = new AddressPointer(address); }

        PushFrom(address, variable.Variable.Type.GetSize(this, Diagnostics, variable.Variable));
    }
    void GenerateCodeForStatement(CompiledVariableGetter variable, GeneralType? expectedType = null, bool resolveReference = true)
    {
        if (variable.Variable.IsGlobal)
        {
            PushFrom(GetGlobalVariableAddress(variable.Variable), variable.Type.GetSize(this, Diagnostics, variable));
        }
        else
        {
            PushFrom(GetLocalVariableAddress(variable.Variable), variable.Type.GetSize(this, Diagnostics, variable));
        }
    }
    void GenerateCodeForStatement(FunctionAddressGetter variable, GeneralType? expectedType = null, bool resolveReference = true)
    {
        CompiledFunctionDefinition? compiledFunction = variable.Function;

        if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(GeneratedCode.Count, true, variable, compiledFunction)); }

        Push(compiledFunction.InstructionOffset);
    }
    void GenerateCodeForStatement(InstructionLabelAddressGetter variable, GeneralType? expectedType = null, bool resolveReference = true)
    {
        if (!GeneratedInstructionLabels.TryGetValue(variable.InstructionLabel, out GeneratedInstructionLabel? instructionLabel))
        {
            instructionLabel = GeneratedInstructionLabels[variable.InstructionLabel] = new()
            {
                InstructionOffset = InvalidFunctionAddress,
            };
            UndefinedInstructionLabels.Add(new UndefinedOffset(GeneratedCode.Count, true, variable, instructionLabel));
        }

        Push(instructionLabel.InstructionOffset);
    }
    void GenerateCodeForStatement(CompiledAddressGetter addressGetter)
    {
        if (!GetAddress(addressGetter.Of, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(addressGetter.Of));
            return;
        }
        GenerateAddressResolver(address);
    }
    void GenerateCodeForStatement(CompiledPointer pointer)
    {
        GenerateCodeForStatement(pointer.To);

        GeneralType addressType = pointer.To.Type;
        if (!addressType.Is(out PointerType? pointerType))
        {
            Diagnostics.Add(Diagnostic.Critical($"This isn't a pointer", pointer.To));
            return;
        }

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(pointerType.GetBitWidth(this, Diagnostics, pointer)));
            PushFrom(new AddressRegisterPointer(reg.Get(pointerType.GetBitWidth(this, Diagnostics, pointer))), pointerType.To.GetSize(this, Diagnostics, pointer.To));
        }
    }
    void GenerateCodeForStatement(CompiledWhileLoop whileLoop)
    {
        CompiledBlock block = CompiledBlock.CreateIfNot(whileLoop.Body);

        AddComment("while (...) {");

        CompiledScope scope = OnScopeEnter(block, false);

        AddComment("Condition");
        int conditionOffset = GeneratedCode.Count;

        List<int> conditionFalseAddresses = new();
        GenerateCodeForCondition(whileLoop.Condition, conditionFalseAddresses);

        BreakInstructions.Push(new ControlFlowFrame());

        GenerateCodeForStatement(block, true);

        AddComment("Jump Back");
        AddInstruction(Opcode.Jump, conditionOffset - GeneratedCode.Count);

        ReturnInstructions.Last.IsSkipping = false;

        FinishJumpInstructions(BreakInstructions.Last.Offsets);

        foreach (int v in conditionFalseAddresses)
        { GeneratedCode[v].Operand1 = GeneratedCode.Count - v; }

        OnScopeExit(block.Location.Position.After(), block.Location.File, scope);

        AddComment("}");

        BreakInstructions.Pop();
    }
    void GenerateCodeForStatement(CompiledForLoop forLoop)
    {
        AddComment("for (...) {");

        CompiledScope scope = OnScopeEnter(forLoop.Location.Position, forLoop.Location.File, Enumerable.Repeat(forLoop.VariableDeclaration, 1), false);

        AddComment("For-loop variable");
        GenerateCodeForStatement(forLoop.VariableDeclaration);

        AddComment("For-loop condition");
        int conditionOffsetFor = GeneratedCode.Count;

        List<int> conditionFalseAddresses = new();
        GenerateCodeForCondition(forLoop.Condition, conditionFalseAddresses);

        BreakInstructions.Push(new ControlFlowFrame());

        GenerateCodeForStatement(forLoop.Body);

        AddComment("For-loop expression");
        GenerateCodeForStatement(forLoop.Expression);

        AddComment("Jump back");
        AddInstruction(Opcode.Jump, conditionOffsetFor - GeneratedCode.Count);

        ReturnInstructions.Last.IsSkipping = false;

        foreach (int v in conditionFalseAddresses)
        { GeneratedCode[v].Operand1 = GeneratedCode.Count - v; }

        FinishJumpInstructions(BreakInstructions.Pop().Offsets);

        OnScopeExit(forLoop.Location.Position.After(), forLoop.Location.File, scope);

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledIf @if)
    {
        List<int> jumpOutInstructions = new();

        CompiledBranch? ifSegment = @if;

        while (true)
        {
            if (ifSegment is CompiledIf partIf)
            {
                AddComment("if (...) {");

                AddComment("If condition");

                List<int> falseJumpAddresses = new();
                GenerateCodeForCondition(partIf.Condition, falseJumpAddresses);

                GenerateCodeForStatement(partIf.Body);

                AddComment("If jump-to-end");
                jumpOutInstructions.Add(GeneratedCode.Count);
                AddInstruction(Opcode.Jump, 0);

                ReturnInstructions.Last.IsSkipping = false;
                if (BreakInstructions.Count > 0) BreakInstructions.Last.IsSkipping = false;

                AddComment("}");

                foreach (int falseJumpAddress in falseJumpAddresses)
                { GeneratedCode[falseJumpAddress].Operand1 = GeneratedCode.Count - falseJumpAddress; }

                ifSegment = partIf.Next;
            }
            else if (ifSegment is CompiledElse partElse)
            {
                AddComment("else {");

                GenerateCodeForStatement(partElse.Body);

                ReturnInstructions.Last.IsSkipping = false;
                if (BreakInstructions.Count > 0) BreakInstructions.Last.IsSkipping = false;

                AddComment("}");

                break;
            }
            else
            {
                break;
            }
        }

        foreach (int item in jumpOutInstructions)
        {
            GeneratedCode[item].Operand1 = GeneratedCode.Count - item;
        }
    }
    void GenerateCodeForStatement(CompiledStackAllocation newInstance)
    {
        AddComment($"new \"{newInstance.Type}\" {{");

        GeneralType instanceType = newInstance.Type;

        StackAlloc(instanceType.GetSize(this, Diagnostics, newInstance), true);

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledConstructorCall constructorCall)
    {
        GeneralType instanceType = constructorCall.Type;
        ImmutableArray<GeneralType> parameters = constructorCall.Arguments.Select(v => v.Type).ToImmutableArray();

        CompiledConstructorDefinition? compiledFunction = constructorCall.Function;

        AddComment($"Call \"{compiledFunction.ToReadable()}\" {{");

        GenerateCodeForStatement(constructorCall.Object);

        Stack<CompiledCleanup> parameterCleanup = new();

        AddComment(" Pass arguments:");

        if (constructorCall.Object.Type.Is<StructType>())
        {
            if (!FindSize(constructorCall.Object.Type, out int size, out PossibleDiagnostic? sizeError))
            {
                Diagnostics.Add(sizeError.ToError(constructorCall.Object));
                return;
            }

            AddInstruction(Opcode.Push, Register.StackPointer);

            parameterCleanup.Add(new CompiledCleanup()
            {
                Location = constructorCall.Object.Location,
                TrashType = new PointerType(constructorCall.Object.Type),
            });

            parameterCleanup.AddRange(GenerateCodeForArguments(constructorCall.Arguments, compiledFunction, 1));
        }
        else if (constructorCall.Object.Type.Is<PointerType>())
        {
            parameterCleanup.AddRange(GenerateCodeForArguments(constructorCall.Arguments, compiledFunction, 1));
        }
        else
        {
            Diagnostics.Add(Diagnostic.Internal($"Invalid type \"{constructorCall.Object.Type}\" used for constructor", constructorCall.Object));
            return;
        }

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset, constructorCall);

        if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(jumpInstruction, false, constructorCall, compiledFunction)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledFieldGetter field)
    {
        GeneralType prevType = field.Object.Type;

        if (prevType.Is(out PointerType? pointerType2))
        {
            GenerateCodeForStatement(field.Object);
            CheckPointerNull();
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
                CheckPointerNull();
                prevType = pointerType2.To;
            }

            if (!prevType.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{prevType}\"", field.Object));
                return;
            }

            if (!structPointerType.GetField(field.Field.Identifier.Content, this, out CompiledField? fieldDefinition, out int fieldOffset, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(field));
                return;
            }

            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(PointerBitWidth));
                PushFrom(new AddressOffset(
                    new AddressRegisterPointer(reg.Get(PointerBitWidth)),
                    fieldOffset
                    ), (GeneralType.InsertTypeParameters(fieldDefinition.Type, structPointerType.TypeArguments) ?? fieldDefinition.Type).GetSize(this, Diagnostics, fieldDefinition));
            }
            return;
        }

        if (!prevType.Is(out StructType? structType)) throw new NotImplementedException();

        GeneralType type = field.Type;

        if (!structType.GetField(field.Field.Identifier.Content, this, out _, out _, out PossibleDiagnostic? error2))
        {
            Diagnostics.Add(error2.ToError(field));
            return;
        }

        // TODO: what the hell is that

        CompiledStatementWithValue? dereference = NeedDerefernce(field);

        if (!GetAddress(field, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(field));
            return;
        }

        if (dereference is null)
        { PushFrom(address, type.GetSize(this, Diagnostics, field)); }
        else
        { PushFromChecked(address, type.GetSize(this, Diagnostics, field)); }
    }
    void GenerateCodeForStatement(CompiledIndexGetter index)
    {
        GeneralType prevType = index.Base.Type;
        GeneralType indexType = index.Index.Type;

        if (prevType.Is(out ArrayType? arrayType))
        {
            using (RegisterUsage.Auto regPtr = Registers.GetFree())
            {
                if (!GetAddress(index.Base, out Address? address, out PossibleDiagnostic? error))
                {
                    Diagnostics.Add(error.ToError(index.Base));
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
                    AddInstruction(Opcode.MathMult, regIndex.Get(indexType.GetBitWidth(this, Diagnostics, index.Index)), arrayType.Of.GetSize(this, Diagnostics, index.Base));
                    AddInstruction(Opcode.MathAdd, regPtr.Get(PointerBitWidth), regIndex.Get(indexType.GetBitWidth(this, Diagnostics, index.Index)));
                }

                PushFrom(new AddressRegisterPointer(regPtr.Get(PointerBitWidth)), arrayType.Of.GetSize(this, Diagnostics, index.Base));
            }
            return;
        }

        if (prevType.Is(out PointerType? pointerType) && pointerType.To.Is(out arrayType))
        {
            int elementSize = arrayType.Of.GetSize(this, Diagnostics, index.Base);
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
                CheckPointerNull();
                break;
            }
            case AddressRuntimeIndex runtimeIndex:
            {
                GenerateAddressResolver(runtimeIndex.Base);

                GeneralType indexType = runtimeIndex.IndexValue.Type;

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
    void GenerateCodeForStatement(CompiledFakeTypeCast typeCast)
    {
        GenerateCodeForStatement(typeCast.Value);
    }
    void GenerateCodeForStatement(CompiledTypeCast typeCast)
    {
        GeneralType statementType = typeCast.Value.Type;
        GeneralType targetType = typeCast.Type;

        // f32 -> i32
        if (statementType.SameAs(BuiltinType.F32) &&
            targetType.SameAs(BuiltinType.I32))
        {
            GenerateCodeForStatement(typeCast.Value);
            AddInstruction(Opcode.FFrom, (InstructionOperand)StackTop, (InstructionOperand)StackTop);
            return;
        }

        // i32 -> f32
        if (statementType.SameAs(BuiltinType.I32) &&
            targetType.SameAs(BuiltinType.F32))
        {
            GenerateCodeForStatement(typeCast.Value);
            AddInstruction(Opcode.FTo, (InstructionOperand)StackTop, (InstructionOperand)StackTop);
            return;
        }

        int statementSize = statementType.GetSize(this, Diagnostics, typeCast.Value);
        int targetSize = targetType.GetSize(this, Diagnostics, typeCast);

        if (statementSize != targetSize)
        {
            if (statementSize < targetSize)
            {
                AddComment($"Grow \"{statementType}\" ({statementSize} bytes) to \"{targetType}\" ({targetSize}) {{");

                AddComment("Make space");

                StackAlloc(targetSize, true);

                AddComment("Value");

                GenerateCodeForStatement(typeCast.Value);

                AddComment("Save");

                for (int i = 0; i < statementSize; i++)
                { PopTo(Register.StackPointer.ToPtr((statementSize - 1) * -ProcessorState.StackDirection, BitWidth._8), BitWidth._8); }

                AddComment("}");

                return;
            }
            else if (statementSize > targetSize)
            {
                AddComment($"Shrink \"{statementType}\" ({statementSize} bytes) to \"{targetType}\" ({targetSize}) {{");

                AddComment("Make space");

                StackAlloc(targetSize, false);

                AddComment("Value");

                GenerateCodeForStatement(typeCast.Value);

                AddComment("Save");

                for (int i = 0; i < targetSize; i++)
                { PopTo(Register.StackPointer.ToPtr((statementSize - 1) * -ProcessorState.StackDirection, BitWidth._8), BitWidth._8); }

                AddComment("Discard excess");

                int excess = statementSize - targetSize;
                Pop(excess);

                AddComment("}");

                return;
            }

            Diagnostics.Add(Diagnostic.Critical($"Can't modify the size of the value. You tried to convert from \"{statementType}\" (size of {statementSize}) to \"{targetType}\" (size of {targetSize})", typeCast));
            return;
        }

        GenerateCodeForStatement(typeCast.Value, targetType);
    }
    void GenerateCodeForStatement(CompiledBlock block, bool ignoreScope = false)
    {
        CompiledScope scope = ignoreScope ? default : OnScopeEnter(block, false);

        AddComment("Statements {");
        foreach (CompiledStatement v in block.Statements)
        {
            if (!Settings.DontOptimize &&
                ((ReturnInstructions.Count > 0 && ReturnInstructions.Last.IsSkipping) ||
                (BreakInstructions.Count > 0 && BreakInstructions.Last.IsSkipping)) &&
                !StatementCompiler.Visit(v).Any(v => v is CompiledInstructionLabelDeclaration))
            {
                continue;
            }

            GenerateCodeForStatement(v);
        }
        AddComment("}");

        if (!ignoreScope) OnScopeExit(block.Location.Position.After(), block.Location.File, scope);
    }
    void GenerateCodeForStatement(CompiledStatement statement, GeneralType? expectedType = null, bool resolveReference = true)
    {
        int startInstruction = GeneratedCode.Count;

        switch (statement)
        {
            case CompiledVariableDeclaration v: GenerateCodeForStatement(v); break;
            case CompiledSizeof v: GenerateCodeForStatement(v); break;
            case CompiledReturn v: GenerateCodeForStatement(v); break;
            case CompiledCrash v: GenerateCodeForStatement(v); break;
            case CompiledBreak v: GenerateCodeForStatement(v); break;
            case CompiledDelete v: GenerateCodeForStatement(v); break;
            case CompiledGoto v: GenerateCodeForStatement(v); break;
            case CompiledBinaryOperatorCall v: GenerateCodeForStatement(v); break;
            case CompiledUnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case CompiledEvaluatedValue v: GenerateCodeForStatement(v, expectedType); break;
            case RegisterGetter v: GenerateCodeForStatement(v, expectedType, resolveReference); break;
            case CompiledVariableGetter v: GenerateCodeForStatement(v, expectedType, resolveReference); break;
            case CompiledParameterGetter v: GenerateCodeForStatement(v, expectedType, resolveReference); break;
            case FunctionAddressGetter v: GenerateCodeForStatement(v, expectedType, resolveReference); break;
            case InstructionLabelAddressGetter v: GenerateCodeForStatement(v, expectedType, resolveReference); break;
            case CompiledFieldGetter v: GenerateCodeForStatement(v); break;
            case CompiledIndexGetter v: GenerateCodeForStatement(v); break;
            case CompiledAddressGetter v: GenerateCodeForStatement(v); break;
            case RegisterSetter v: GenerateCodeForValueSetter(v); break;
            case CompiledVariableSetter v: GenerateCodeForValueSetter(v); break;
            case CompiledParameterSetter v: GenerateCodeForValueSetter(v); break;
            case CompiledIndirectSetter v: GenerateCodeForValueSetter(v); break;
            case CompiledFieldSetter v: GenerateCodeForValueSetter(v); break;
            case CompiledIndexSetter v: GenerateCodeForValueSetter(v); break;
            case CompiledPointer v: GenerateCodeForStatement(v); break;
            case CompiledWhileLoop v: GenerateCodeForStatement(v); break;
            case CompiledForLoop v: GenerateCodeForStatement(v); break;
            case CompiledIf v: GenerateCodeForStatement(v); break;
            case CompiledStackAllocation v: GenerateCodeForStatement(v); break;
            case CompiledConstructorCall v: GenerateCodeForStatement(v); break;
            case CompiledTypeCast v: GenerateCodeForStatement(v); break;
            case CompiledFakeTypeCast v: GenerateCodeForStatement(v); break;
            case CompiledRuntimeCall v: GenerateCodeForStatement(v); break;
            case CompiledFunctionCall v: GenerateCodeForFunctionCall(v); break;
            case CompiledExternalFunctionCall v: GenerateCodeForFunctionCall_External(v); break;
            case CompiledBlock v: GenerateCodeForStatement(v); break;
            case CompiledInstructionLabelDeclaration v: GenerateCodeForStatement(v); break;
            case CompiledStatementWithValueThatActuallyDoesntHaveValue v: GenerateCodeForStatement(v.Statement); break;
            case CompiledStringInstance v: GenerateCodeForStatement(v); break;
            case CompiledStackStringInstance v: GenerateCodeForStatement(v); break;
            case EmptyStatement: break;
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

    void GenerateCodeForCondition(CompiledStatementWithValue statement, List<int> falseJumpAddresses)
    {
        switch (statement)
        {
            case CompiledBinaryOperatorCall v:
                GenerateCodeForCondition(v, falseJumpAddresses);
                break;
            case CompiledUnaryOperatorCall v:
                GenerateCodeForCondition(v, falseJumpAddresses);
                break;
            default:
                GenerateCodeForStatement(statement);
                break;
        }

        if (falseJumpAddresses.Count == 0)
        {
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(statement.Type.GetBitWidth(this, Diagnostics, statement)));
                AddInstruction(Opcode.Compare, reg.Get(statement.Type.GetBitWidth(this, Diagnostics, statement)), 0);
                falseJumpAddresses.Add(GeneratedCode.Count);
                AddInstruction(Opcode.JumpIfEqual, 0);
            }
        }
    }
    void GenerateCodeForCondition(CompiledUnaryOperatorCall @operator, List<int> falseJumpAddresses)
    {
        switch (@operator.Operator)
        {
            case CompiledUnaryOperatorCall.LogicalNOT:
            {
                List<int> subFalseJumpAddresses = new();
                GenerateCodeForCondition(@operator.Left, subFalseJumpAddresses);

                falseJumpAddresses.Add(GeneratedCode.Count);
                AddInstruction(Opcode.Jump, 0);

                foreach (int v in subFalseJumpAddresses)
                { GeneratedCode[v].Operand1 = GeneratedCode.Count - v; }

                return;
            }
            case CompiledUnaryOperatorCall.BinaryNOT:
            {
                BitWidth bitWidth = @operator.Left.Type.GetBitWidth(this, Diagnostics, @operator.Left);

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
                Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator}\"", @operator));
                return;
            }
        }
    }
    void GenerateCodeForCondition(CompiledBinaryOperatorCall @operator, List<int> falseJumpAddresses)
    {
        BitWidth leftBitWidth = @operator.Left.Type.GetBitWidth(this, Diagnostics, @operator.Left);
        BitWidth rightBitWidth = @operator.Right.Type.GetBitWidth(this, Diagnostics, @operator.Right);
        BitWidth bitWidth = StatementCompiler.MaxBitWidth(leftBitWidth, rightBitWidth);

        if (@operator.Operator == CompiledBinaryOperatorCall.LogicalAND)
        {
            GenerateCodeForCondition(@operator.Left, falseJumpAddresses);
            GenerateCodeForCondition(@operator.Right, falseJumpAddresses);
        }
        else if (@operator.Operator == CompiledBinaryOperatorCall.LogicalOR)
        {
            List<int> subFalseJumpAddresses = new();
            GenerateCodeForCondition(@operator.Left, subFalseJumpAddresses);

            int trueAddress = GeneratedCode.Count;
            AddInstruction(Opcode.Jump, 0);

            foreach (int v in subFalseJumpAddresses)
            { GeneratedCode[v].Operand1 = GeneratedCode.Count - v; }

            GenerateCodeForCondition(@operator.Right, falseJumpAddresses);

            GeneratedCode[trueAddress].Operand1 = GeneratedCode.Count - trueAddress;
        }
        else
        {
            GenerateCodeForStatement(@operator.Left);
            GenerateCodeForStatement(@operator.Right);

            bool isFloat = @operator.Left.Type.SameAs(BasicType.F32) || @operator.Right.Type.SameAs(BasicType.F32);

            using (RegisterUsage.Auto regLeft = Registers.GetFree())
            using (RegisterUsage.Auto regRight = Registers.GetFree())
            {
                PopTo(regRight.Get(bitWidth), rightBitWidth);
                PopTo(regLeft.Get(bitWidth), leftBitWidth);

                switch (@operator.Operator)
                {
                    case CompiledBinaryOperatorCall.Addition:
                        AddInstruction(isFloat ? Opcode.FMathAdd : Opcode.MathAdd, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.Subtraction:
                        AddInstruction(isFloat ? Opcode.FMathSub : Opcode.MathSub, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.Multiplication:
                        AddInstruction(isFloat ? Opcode.FMathMult : Opcode.MathMult, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.Division:
                        AddInstruction(isFloat ? Opcode.FMathDiv : Opcode.MathDiv, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.Modulo:
                        AddInstruction(isFloat ? Opcode.FMathMod : Opcode.MathMod, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.LogicalAND:
                        AddInstruction(Opcode.LogicAND, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.LogicalOR:
                        AddInstruction(Opcode.LogicOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.BitwiseAND:
                        AddInstruction(Opcode.BitsAND, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.BitwiseOR:
                        AddInstruction(Opcode.BitsOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.BitwiseXOR:
                        AddInstruction(Opcode.BitsXOR, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.BitshiftLeft:
                        AddInstruction(Opcode.BitsShiftLeft, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;
                    case CompiledBinaryOperatorCall.BitshiftRight:
                        AddInstruction(Opcode.BitsShiftRight, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        Push(regLeft.Get(bitWidth));
                        break;

                    case CompiledBinaryOperatorCall.CompEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        falseJumpAddresses.Add(GeneratedCode.Count);
                        AddInstruction(Opcode.JumpIfNotEqual, 0);
                        break;

                    case CompiledBinaryOperatorCall.CompNEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        falseJumpAddresses.Add(GeneratedCode.Count);
                        AddInstruction(Opcode.JumpIfEqual, 0);
                        break;

                    case CompiledBinaryOperatorCall.CompGT:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        falseJumpAddresses.Add(GeneratedCode.Count);
                        AddInstruction(Opcode.JumpIfLessOrEqual, 0);
                        break;

                    case CompiledBinaryOperatorCall.CompGEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        falseJumpAddresses.Add(GeneratedCode.Count);
                        AddInstruction(Opcode.JumpIfLess, 0);
                        break;

                    case CompiledBinaryOperatorCall.CompLT:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        falseJumpAddresses.Add(GeneratedCode.Count);
                        AddInstruction(Opcode.JumpIfGreaterOrEqual, 0);
                        break;

                    case CompiledBinaryOperatorCall.CompLEQ:
                        AddInstruction(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Get(bitWidth), regRight.Get(bitWidth));
                        falseJumpAddresses.Add(GeneratedCode.Count);
                        AddInstruction(Opcode.JumpIfGreater, 0);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }

    ImmutableArray<CompiledCleanup> CompileVariables(IEnumerable<CompiledVariableDeclaration> statements, bool addComments = true)
    {
        if (addComments) AddComment("Variables {");

        ImmutableArray<CompiledCleanup>.Builder result = ImmutableArray.CreateBuilder<CompiledCleanup>();

        foreach (CompiledVariableDeclaration statement in statements)
        {
            CompiledCleanup? item = GenerateCodeForLocalVariable(statement);
            if (item is null) continue;

            result.Add(item);
        }

        if (addComments) AddComment("}");

        return result.ToImmutable();
    }

    void CleanupVariables(ImmutableArray<CompiledCleanup> cleanupItems, Location location, bool justGenerateCode)
    {
        if (cleanupItems.Length == 0) return;
        AddComment("Clear Variables");
        for (int i = cleanupItems.Length - 1; i >= 0; i--)
        {
            CleanupVariables(cleanupItems[i], location, justGenerateCode);
        }
    }
    void CleanupVariables(CompiledCleanup cleanupItem, Location location, bool justGenerateCode)
    {
        GenerateDestructor(cleanupItem);
        Pop(cleanupItem.TrashType.GetSize(this, Diagnostics, cleanupItem));

        if (!justGenerateCode) CompiledLocalVariables.Pop();
    }

    void GenerateCodeForValueSetter(RegisterSetter registerSetter)
    {
        GenerateCodeForStatement(registerSetter.Value);
        PopTo(registerSetter.Register);
    }
    void GenerateCodeForValueSetter(CompiledVariableSetter localVariableSetter)
    {
        if (localVariableSetter.Variable.IsGlobal)
        {
            GenerateCodeForStatement(localVariableSetter.Value, localVariableSetter.Variable.Type);
            PopTo(GetGlobalVariableAddress(localVariableSetter.Variable), localVariableSetter.Variable.Type.GetSize(this, Diagnostics, localVariableSetter.Variable));
            // localVariableSetter.Variable.Variable.IsInitialized = true;
            return;
        }
        else
        {
            GenerateCodeForStatement(localVariableSetter.Value, localVariableSetter.Variable.Type);
            PopTo(GetLocalVariableAddress(localVariableSetter.Variable), localVariableSetter.Variable.Type.GetSize(this, Diagnostics, localVariableSetter));
            // localVariableSetter.Variable.Variable.IsInitialized = true;
            return;
        }
    }
    void GenerateCodeForValueSetter(CompiledParameterSetter parameterSetter)
    {
        GenerateCodeForStatement(parameterSetter.Value, parameterSetter.Variable.Type);

        Address address = GetParameterAddress(parameterSetter.Variable);

        if (parameterSetter.Variable.IsRef)
        { address = new AddressPointer(address); }

        PopTo(address, parameterSetter.Variable.Type.GetSize(this, Diagnostics, parameterSetter));
        return;
    }
    void GenerateCodeForValueSetter(CompiledFieldSetter fieldSetter)
    {
        GeneralType type = fieldSetter.Type;
        GeneralType valueType = fieldSetter.Value.Type;

        CompiledStatementWithValue? dereference = NeedDerefernce(fieldSetter.ToGetter());

        if (fieldSetter.Value is CompiledStackStringInstance stackString)
        {
            if (dereference is null)
            {
                if (!GetAddress(fieldSetter.ToGetter(), out Address? address, out PossibleDiagnostic? error2))
                {
                    Diagnostics.Add(error2.ToError(fieldSetter));
                    return;
                }
                for (int i = 0; i < stackString.Value.Length; i++)
                {
                    Push(new CompiledValue(stackString.Value[i]));
                    PopTo(new AddressOffset(address, i * 2), 2);
                }

                if (stackString.IsNullTerminated)
                {
                    Push(new CompiledValue('\0'));
                    PopTo(new AddressOffset(address, stackString.Value.Length * 2), 2);
                }
            }
            else
            {
                if (!GetAddress(fieldSetter.ToGetter(), out Address? address, out PossibleDiagnostic? error))
                {
                    Diagnostics.Add(error.ToError(fieldSetter));
                    return;
                }
                for (int i = 0; i < stackString.Value.Length; i++)
                {
                    Push(new CompiledValue(stackString.Value[i]));
                    PopToChecked(new AddressOffset(address, i * 2), 2);
                }

                if (stackString.IsNullTerminated)
                {
                    Push(new CompiledValue('\0'));
                    PopToChecked(new AddressOffset(address, stackString.Value.Length * 2), 2);
                }
            }
        }
        else
        {
            GenerateCodeForStatement(fieldSetter.Value, type);

            if (dereference is null)
            {
                if (!GetAddress(fieldSetter.ToGetter(), out Address? address, out PossibleDiagnostic? error2))
                {
                    Diagnostics.Add(error2.ToError(fieldSetter));
                    return;
                }
                PopTo(address, valueType.GetSize(this, Diagnostics, fieldSetter.Value));
            }
            else
            {
                if (!GetAddress(fieldSetter.ToGetter(), out Address? address, out PossibleDiagnostic? error))
                {
                    Diagnostics.Add(error.ToError(fieldSetter));
                    return;
                }
                PopToChecked(address, valueType.GetSize(this, Diagnostics, fieldSetter.Value));
            }
        }
    }
    void GenerateCodeForValueSetter(CompiledIndexSetter indexSetter)
    {
        if (!GetAddress(indexSetter.ToGetter(), out Address? _address, out PossibleDiagnostic? _error))
        {
            Diagnostics.Add(_error.ToError(indexSetter));
            return;
        }
        GenerateCodeForStatement(indexSetter.Value);
        PopTo(_address, indexSetter.Value.Type.GetSize(this, Diagnostics, indexSetter.Value));
    }
    void GenerateCodeForValueSetter(CompiledIndirectSetter statementToSet)
    {
        if (statementToSet.AddressValue.Type.GetBitWidth(this, Diagnostics, statementToSet.AddressValue) != PointerBitWidth)
        {
            Diagnostics.Add(Diagnostic.Critical($"Type \"{statementToSet.AddressValue.Type}\" cant be a pointer", statementToSet.AddressValue));
            return;
        }

        GenerateCodeForStatement(statementToSet.Value);

        GenerateCodeForStatement(statementToSet.AddressValue);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), statementToSet.Value.Type.GetSize(this, Diagnostics, statementToSet.Value));
        }
    }

    #endregion

    CompiledScope OnScopeEnter(CompiledBlock block, bool isFunction) => OnScopeEnter(block.Location.Position, block.Location.File, block.Statements.OfType<CompiledVariableDeclaration>(), isFunction);
    CompiledScope OnScopeEnter(Position position, Uri file, IEnumerable<CompiledVariableDeclaration> variables, bool isFunction)
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

        CompiledScope scope = new(
            CompileVariables(variables, CurrentContext is null),
            isFunction
        );
        CleanupStack2.Push(scope);
        return scope;
    }

    void OnScopeExit(Position position, Uri file, CompiledScope scope)
    {
        AddComment("Scope exit");
        if (!scope.Equals(CleanupStack2.Pop()))
        { Diagnostics.Add(Diagnostic.Internal($"There was something went wrong in a scope.", position, file)); }

        CleanupVariables(scope.Variables, new Location(position, file), false);

        ScopeInformation scopeDebug = CurrentScopeDebug.Pop();
        scopeDebug.Location.Instructions.End = GeneratedCode.Count - 1;
        DebugInfo?.ScopeInformation.Add(scopeDebug);
    }

    #region GenerateCodeForLocalVariable

    CompiledCleanup? GenerateCodeForLocalVariable(CompiledVariableDeclaration newVariable)
    {
        // if (newVariable.Getters.Count == 0 &&
        //     newVariable.Setters.Count == 0)
        // { return null; }

        GeneralType type = newVariable.Type;

        int offset = (VariablesSize + type.GetSize(this, Diagnostics, newVariable)) * ProcessorState.StackDirection;
        GeneratedVariable generatedVariable = GeneratedVariables[newVariable] = new GeneratedVariable()
        {
            MemoryAddress = offset,
        };

        StackElementInformation debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Identifier = newVariable.Identifier,
            Address = offset,
            BasePointerRelative = true,
            Size = type.GetSize(this, Diagnostics, newVariable),
            Type = type
        };

        if (CurrentScopeDebug.Count > 0) CurrentScopeDebug.Last.Stack.Add(debugInfo);

        CompiledLocalVariables.Add(newVariable);

        int size;

        if (newVariable.InitialValue is CompiledEvaluatedValue evaluatedInitialValue)
        {
            AddComment($"Initial value {{");

            size = evaluatedInitialValue.Type.GetSize(this, Diagnostics, newVariable.InitialValue);

            Push(evaluatedInitialValue.Value);
            generatedVariable.IsInitialized = true;

            AddComment("}");
        }
        else if (newVariable.Type.Is(out ArrayType? arrayType) &&
            arrayType.Of.SameAs(BasicType.U16) &&
            newVariable.InitialValue is CompiledStringInstance literalStatement &&
            arrayType.ComputedLength.HasValue &&
            literalStatement.Value.Length == arrayType.ComputedLength.Value)
        {
            size = arrayType.GetSize(this, Diagnostics, newVariable);
            generatedVariable.IsInitialized = true;

            for (int i = 0; i < literalStatement.Value.Length; i++)
            {
                Push(new CompiledValue(literalStatement.Value[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = newVariable.Type.GetSize(this, Diagnostics, newVariable);
            StackAlloc(size, false);

            if (size <= 0)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable has a size of {size}", newVariable));
                return default;
            }

            AddComment("}");
        }

        if (size != newVariable.Cleanup.TrashType.GetSize(this, Diagnostics, newVariable))
        { Diagnostics.Add(Diagnostic.Internal($"Variable size ({newVariable.Cleanup.TrashType.GetSize(this, Diagnostics, newVariable)}) and initial value size ({size}) mismatch", newVariable.InitialValue!)); }

        return newVariable.Cleanup;
    }

    #endregion

    #region GenerateCodeFor...

    int VariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariableDeclaration variable in CompiledLocalVariables)
            { sum += variable.Type.GetSize(this, Diagnostics, variable); }
            return sum;
        }
    }

    int GlobalVariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariableDeclaration variable in CompiledGlobalVariables)
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

    void GenerateCodeForFunction(ICompiledFunctionDefinition function, CompiledBlock body)
    {
        if (!GeneratedFunctions.Add(function)) return;

        function.InstructionOffset = GeneratedCode.Count;
        for (int i = UndefinedFunctionOffsets.Count - 1; i >= 0; i--)
        {
            UndefinedOffset item = UndefinedFunctionOffsets[i];
            if (item.Called != function) continue;
            item.Apply(GeneratedCode);
            UndefinedFunctionOffsets.RemoveAt(i);
        }

        CurrentContext = function as IDefinition;
        InFunction = true;

        CompiledParameters.Clear();
        CompiledLocalVariables.Clear();
        ReturnInstructions.Clear();
        ScopeSizes.Push(0);
        int savedInstructionLabelCount = CompiledInstructionLabels.Count;

        CompiledParameters.AddRange(function.Parameters.Select((v, i) => new CompiledParameter(i, function.ParameterTypes[i], v)));

        int instructionStart = GeneratedCode.Count;

        if (function is IHaveCompiledType functionWithType)
        { CurrentReturnType = functionWithType.Type; }
        else
        { CurrentReturnType = BuiltinType.Void; }

        if (body is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{((FunctionThingDefinition)function).ToReadable()}\" does not have a body", (FunctionThingDefinition)function));
            return;
        }

        CompiledScope scope = OnScopeEnter(body, true);

        if (function is IHaveCompiledType returnType && !returnType.Type.SameAs(BasicType.Void))
        {
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = GetReturnValueAddress(returnType.Type).Offset,
                BasePointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = returnType.Type.GetSize(this, Diagnostics, (FunctionThingDefinition)function),
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

        ReturnInstructions.Push(new ControlFlowFrame());

        AddComment("Statements {");
        GenerateCodeForStatement(body, true);
        AddComment("}");

        CurrentReturnType = null;

        OnScopeExit(body.Location.Position.After(), body.Location.File, scope);

        FinishJumpInstructions(ReturnInstructions.Last.Offsets);
        ReturnInstructions.Pop();

        AddComment("Return");
        Return(body.Location.After());

        if (body != null) AddComment("}");

        DebugInfo?.FunctionInformation.Add(new FunctionInformation()
        {
            IsValid = true,
            Function = (FunctionThingDefinition)function,
            TypeArguments = TypeArguments.ToImmutableDictionary(),
            Instructions = (instructionStart, GeneratedCode.Count),
        });

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
    }

    void GenerateCodeForTopLevelStatements(ImmutableArray<CompiledStatement> statements)
    {
        if (statements.IsDefaultOrEmpty) return;

        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                Location = statements.Select(v => v.Location).Aggregate((a, b) => a.Union(b)),
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
        ReturnInstructions.Push(new ControlFlowFrame());

        CurrentReturnType = ExitCodeType;

        AddComment("Statements {");
        foreach (CompiledStatement statement in statements)
        { GenerateCodeForStatement(statement); }
        AddComment("}");

        FinishJumpInstructions(ReturnInstructions.Pop().Offsets);

        CurrentReturnType = null;

        AddComment("Pop stack frame");
        PopTo(Register.BasePointer);
        if (ScopeSizes.Pop() != 0) { }

        AddComment("}");

        ScopeInformation scope = CurrentScopeDebug.Pop();
        scope.Location.Instructions.End = GeneratedCode.Count - 1;
        DebugInfo?.ScopeInformation.Add(scope);
    }

    #endregion

    BBLangGeneratorResult GenerateCode(CompilerResult compilerResult, MainGeneratorSettings settings)
    {
        ScopeSizes.Push(0);

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
                Address = (ExitCodeType.GetSize(this) - 1) * ProcessorState.StackDirection,
                BasePointerRelative = false,
                Kind = StackElementKind.Internal,
                Size = ExitCodeType.GetSize(this),
                Identifier = "Exit Code",
                Type = ExitCodeType,
            });
        }

        IEnumerable<CompiledVariableDeclaration> globalVariableDeclarations = compilerResult.Statements
            .OfType<CompiledVariableDeclaration>();

        Stack<CompiledCleanup> globalVariablesCleanup = new();
        foreach (CompiledVariableDeclaration variableDeclaration in globalVariableDeclarations)
        {
            // if (variableDeclaration.Getters.Count == 0 &&
            //     variableDeclaration.Setters.Count == 0)
            // { continue; }

            GeneralType type = variableDeclaration.Type;

            int size = type.GetSize(this, Diagnostics, variableDeclaration);
            int currentOffset = GlobalVariablesSize;

            GeneratedVariables[variableDeclaration] = new GeneratedVariable()
            {
                MemoryAddress = currentOffset,
                IsInitialized = false,
            };

            CompiledGlobalVariables.Add(variableDeclaration);
            globalVariablesCleanup.Insert(0, variableDeclaration.Cleanup);
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
                Push(Register.StackPointer);
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

        GenerateCodeForTopLevelStatements(compilerResult.Statements);

        AddComment("Pop abs global address");
        Pop(AbsGlobalAddressType.GetSize(this)); // Pop abs global offset

        if (Settings.CleanupGlobalVaraibles)
        {
            AddComment("Cleanup global variables {");
            CleanupVariables(globalVariablesCleanup.ToImmutableArray(), default, true);
            AddComment("}");
        }

        AddInstruction(Opcode.Exit);

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

        foreach ((ICompiledFunctionDefinition function, CompiledBlock body) in compilerResult.Functions)
        {
            if (function is IExposeable exposeable && exposeable.ExposedFunctionName is not null)
            { GenerateCodeForFunction(function, body); }
        }

        while (UndefinedFunctionOffsets.Count > 0)
        {
            foreach ((ICompiledFunctionDefinition function, CompiledBlock body) in compilerResult.Functions)
            {
                if (UndefinedFunctionOffsets.Any(v => v.Called == function))
                { GenerateCodeForFunction(function, body); }
            }
        }

        SetUndefinedFunctionOffsets(UndefinedInstructionLabels, true);

        // {
        //     ScopeInformation scope = CurrentScopeDebug.Pop();
        //     scope.Location.Instructions.End = GeneratedCode.Count - 1;
        //     DebugInfo?.ScopeInformation.Add(scope);
        // }

        Dictionary<string, ExposedFunction> exposedFunctions = new();
        foreach (CompiledFunctionDefinition f in compilerResult.FunctionDefinitions)
        {
            if (f.ExposedFunctionName is null) continue;
            if (f.InstructionOffset == InvalidFunctionAddress)
            {
                Diagnostics.Add(Diagnostic.Internal($"Exposed function \"{f.ToReadable()}\" was not compiled", f.Identifier, f.File));
                continue;
            }

            int returnValueSize = f.ReturnSomething ? f.Type.GetSize(this, Diagnostics, f.TypeToken) : 0;
            int argumentsSize = 0;
            foreach (GeneralType p in f.ParameterTypes)
            { argumentsSize += p.GetSize(this, Diagnostics, ((FunctionDefinition)f).Type); }

            exposedFunctions[f.ExposedFunctionName] = new(f.ExposedFunctionName, returnValueSize, f.InstructionOffset, argumentsSize);
        }

        return new BBLangGeneratorResult()
        {
            Code = GeneratedCode.Select(v => new Instruction(v)).ToImmutableArray(),
            DebugInfo = DebugInfo,
            CompiledFunctions = compilerResult.FunctionDefinitions,
            CompiledOperators = compilerResult.OperatorDefinitions,
            CompiledGeneralFunctions = compilerResult.GeneralFunctionDefinitions,
            CompiledConstructors = compilerResult.ConstructorDefinitions,
            ExposedFunctions = exposedFunctions.ToFrozenDictionary(),
            GeneratedUnmanagedFunctions = GeneratedUnmanagedFunctions.Select(v => v.Function).ToImmutableArray(),
            GeneratedUnmanagedFunctionReferences = GeneratedUnmanagedFunctions.Select(v => v.Reference).ToImmutableArray(),
        };
    }
}
