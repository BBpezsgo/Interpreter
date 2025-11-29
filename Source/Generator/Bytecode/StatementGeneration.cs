using System.Reflection.Emit;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    GeneratorStatistics _statistics;
    readonly Dictionary<IHaveInstructionOffset, InstructionLabel> DefinitionLabels = new();

    InstructionLabel LabelForDefinition(IHaveInstructionOffset definition)
    {
        if (DefinitionLabels.TryGetValue(definition, out InstructionLabel label)) return label;
        return DefinitionLabels[definition] = Code.DefineLabel();
    }

    void GenerateDeallocator(CompiledCleanup cleanup)
    {
        if (cleanup.Deallocator is null)
        {
            return;
        }
        CompiledFunctionDefinition? deallocator = cleanup.Deallocator;
        CompiledFunction f = Functions.First(v => v.Function == deallocator);

        if (deallocator.ExternalFunctionName is not null)
        {
            throw new NotImplementedException();
        }

        AddComment($"Call \"{deallocator.ToReadable()}\" {{");

        if (deallocator.ReturnSomething)
        { throw new NotImplementedException(); }

        AddComment(" .:");

        InstructionLabel label = LabelForDefinition(deallocator);
        Call(label, cleanup.Location, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables));

        if (deallocator.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(label, cleanup.Location, deallocator)); }

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
        if (StatementCompiler.AllowDeallocate(cleanup.TrashType))
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

        CompiledFunction f = Functions.First(v => v.Function == cleanup.Destructor);

        AddComment(" Param0 should be already there");

        AddComment(" .:");

        InstructionLabel label = LabelForDefinition(cleanup.Destructor);
        Call(label, cleanup.Location, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables));

        if (cleanup.Destructor.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(label, cleanup.Location, cleanup.Destructor)); }

        if (StatementCompiler.AllowDeallocate(cleanup.TrashType))
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
        Code.Emit(Opcode.MathAdd, result, PointerSize);
        return true;
    }
    bool GenerateSize(ArrayType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (FindSize(type, out int size, out _))
        {
            Code.Emit(Opcode.MathAdd, result, size);
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
        using (RegisterUsage.Auto lengthRegister = Registers.GetFree(lengthType.GetBitWidth(this)))
        {
            PopTo(lengthRegister.Register);
            Code.Emit(Opcode.MathMultU, lengthRegister.Register, elementSize);
            Code.Emit(Opcode.MathAdd, result, lengthRegister.Register);
        }
        return true;
    }
    bool GenerateSize(FunctionType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.Emit(Opcode.MathAdd, result, PointerSize);
        return true;
    }
    bool GenerateSize(StructType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        Code.Emit(Opcode.MathAdd, result, size);
        return true;
    }
    bool GenerateSize(GenericType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new InvalidOperationException($"Generic type doesn't have a size");
    bool GenerateSize(BuiltinType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        Code.Emit(Opcode.MathAdd, result, size);
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
                InstructionOffset = Code.Offset,
            };
        }
        generatedInstructionLabel.InstructionOffset = Code.Offset;
        Code.MarkLabel(LabelForDefinition(generatedInstructionLabel));
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

        if (CanReturn && CleanupStack2.Count > 0)
        {
            AddComment("Cleanup function scopes {");
            for (int i = CleanupStack2.Count - 1; i >= 0; i--)
            {
                CleanupVariables(CleanupStack2[i].Variables, keywordCall.Location, true);
                if (CleanupStack2[i].IsFunction) break;
            }
            AddComment("}");
        }

        ReturnInstructions.Last.IsSkipping = true;
        Code.Emit(Opcode.Jump, ReturnInstructions.Last.Label.Relative());

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
            Code.Emit(Opcode.Crash, Register.StackPointer);
        }
        else
        {
            GenerateCodeForStatement(throwValue);
            using (RegisterUsage.Auto reg = Registers.GetFree(throwType.GetBitWidth(this, Diagnostics, throwValue)))
            {
                PopTo(reg.Register);
                Code.Emit(Opcode.Crash, reg.Register);
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

        ReturnInstructions.Last.IsSkipping = true;
        Code.Emit(Opcode.Jump, BreakInstructions.Last.Label.Relative());
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

        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            InstructionLabel offsetLabel = Code.DefineLabel();
            Code.Emit(Opcode.MathSub, reg.Register, offsetLabel.Absolute());
            Code.MarkLabel(offsetLabel);
            Code.Emit(Opcode.Jump, reg.Register);
        }
    }
    Stack<CompiledCleanup> GenerateCodeForArguments(IReadOnlyList<CompiledPassedArgument> arguments, ICompiledFunctionDefinition compiledFunction, int alreadyPassed = 0)
    {
        Stack<CompiledCleanup> argumentCleanup = new();

        for (int i = 0; i < arguments.Count; i++)
        {
            CompiledPassedArgument argument = arguments[i];
            GeneralType argumentType = argument.Value.Type;
            CompiledParameter parameter = compiledFunction.Parameters[i + alreadyPassed];

            if (argumentType.GetSize(this, Diagnostics, argument.Value) != parameter.Type.GetSize(this, Diagnostics, parameter))
            { Diagnostics.Add(Diagnostic.Internal($"Bad argument type passed: expected \"{parameter.Type}\" passed \"{argumentType}\"", argument.Value)); }

            AddComment($" Pass {parameter}:");

            GenerateCodeForStatement(argument.Value, parameter.Type);

            argumentCleanup.Push(argument.Cleanup);
        }

        return argumentCleanup;
    }
    void GenerateCodeForParameterPassing(IReadOnlyList<CompiledPassedArgument> parameters, FunctionType function, Stack<CompiledCleanup> parameterCleanup)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            AddComment($" Param {i}:");
            GenerateCodeForStatement(parameters[i].Value, function.Parameters[i]);
            parameterCleanup.Push(parameters[i].Cleanup);
        }
    }
    Stack<CompiledCleanup> GenerateCodeForParameterPassing(IReadOnlyList<CompiledPassedArgument> parameters, FunctionType function)
    {
        Stack<CompiledCleanup> parameterCleanup = new();
        GenerateCodeForParameterPassing(parameters, function, parameterCleanup);
        return parameterCleanup;
    }
    void GenerateCodeForParameterCleanup(Stack<CompiledCleanup> parameterCleanup)
    {
        if (parameterCleanup.Count == 0) return;

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
        CompiledFunction f = Functions.First(v => v.Function == caller.Declaration);

        AddComment($"Call \"{caller.Declaration.ToReadable()}\" {{");

        if (caller.Function.ReturnValueSize > 0 && caller.SaveValue)
        {
            AddComment($"Initial return value {{");
            StackAlloc(caller.Function.ReturnValueSize, false);
            AddComment($"}}");
        }

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Declaration);

        AddComment(" .:");

        Code.Emit(Opcode.CallMSIL, caller.Function.Id);
        InstructionLabel skipLabel = Code.DefineLabel();
        using (RegisterUsage.Auto reg = Registers.GetFree(BitWidth._8))
        {
            PopTo(reg.Register);
            Code.Emit(Opcode.Compare, reg.Register, 0);
            Code.Emit(Opcode.JumpIfNotEqual, skipLabel.Relative());
        }

        InstructionLabel label = LabelForDefinition(caller.Declaration);
        Call(label, caller, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables));

        if (caller.Declaration.InstructionOffset == InvalidFunctionAddress)
        {
            UndefinedFunctionOffsets.Add(new(label, caller, caller.Declaration));
        }
        Code.Emit(Opcode.HotFuncEnd, caller.Function.Id);

        Code.MarkLabel(skipLabel);

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
        AddComment($"Call \"{caller.Declaration.ToReadable()}\" {{");

        if (caller.Function.ReturnValueSize > 0 && caller.SaveValue)
        {
            AddComment($"Initial return value {{");
            StackAlloc(caller.Function.ReturnValueSize, false);
            AddComment($"}}");
        }

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Declaration);

        AddComment(" .:");
        Code.Emit(Opcode.CallExternal, caller.Function.Id);

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
        CompiledFunction f = Functions.First(v => v.Function == caller.Function);
        if (ILGenerator is not null)
        {
            ILGenerator.Diagnostics.Clear();
            if (ILGenerator.GenerateImplMarshaled(f, out ExternalFunctionScopedSyncCallback? method, out DynamicMethod? raw))
            {
                if (ILGenerator.Diagnostics.Has(DiagnosticsLevel.Error))
                {
                    ILGenerator.Diagnostics.Throw();
                    goto anyway;
                }

                int existing = GeneratedUnmanagedFunctions.FindIndex(v => v.Reference == method);
                ExternalFunctionScopedSync externFunc;

                if (existing == -1)
                {
                    int returnValueSize = f.Function.ReturnSomething ? f.Function.Type.GetSize(this) : 0;
                    int parametersSize = f.Function.Parameters.Aggregate(0, (a, b) => a + b.Type.GetSize(this));
                    int id = ExternalFunctions.Concat(GeneratedUnmanagedFunctions.Select(v => (IExternalFunction)v.Function).AsEnumerable()).GenerateId();

#if UNITY_BURST
                        IntPtr ptr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(method);
                        unsafe { externFunc = new((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)ptr, id, parametersSize, returnValueSize, 0, ExternalFunctionScopedSyncFlags.MSILPointerMarshal); }
                        //UnityEngine.Debug.LogWarning($"MSIL {f.ToReadable()} --> {raw ?? method.Method} ({externFunc})");
#else
                    externFunc = new(method, id, parametersSize, returnValueSize, 0, ExternalFunctionScopedSyncFlags.MSILPointerMarshal);
                    Debug.WriteLine($"MSIL {f.ToReadable()} --> {raw ?? method.Method} ({externFunc})");
#endif
                    GeneratedUnmanagedFunctions.Add((externFunc, method));
                }
                else
                {
                    externFunc = GeneratedUnmanagedFunctions[existing].Function;
                }

                GenerateCodeForFunctionCall_MSIL(new()
                {
                    Declaration = f.Function,
                    Function = externFunc,

                    Arguments = caller.Arguments,
                    Location = caller.Location,
                    SaveValue = caller.SaveValue,
                    Type = caller.Type,
                });

                Diagnostics.Add(Diagnostic.OptimizationNotice($"Function {f.Function.ToReadable()} compiled into MSIL", caller));

                Diagnostics.AddRange(ILGenerator.Diagnostics);

                ILGenerator.Diagnostics.Clear();
                return;
            anyway:;
            }
            //if (!ILGenerator.Diagnostics.Has(DiagnosticsLevel.Error))
            //{
            //    ILGenerator.GenerateImplMarshaled(f, out _);
            //}
            Diagnostics.Add(Diagnostic.FailedOptimization($"Failed to generate MSIL for function {f.Function}", caller).WithSuberrors(ILGenerator.Diagnostics.Diagnostics.Where(v => v.Level == DiagnosticsLevel.Error)));
            ILGenerator.Diagnostics.Clear();
        }

        AddComment($"Call {caller.Function.ToReadable()} {{");

        if (caller.Function.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(caller.Function.Type.GetSize(this, Diagnostics, caller), false);
            AddComment($"}}");
        }

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Function);

        AddComment(" .:");

        InstructionLabel label = LabelForDefinition(caller.Function);
        Call(label, caller, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables));

        if (caller.Function.InstructionOffset == InvalidFunctionAddress)
        {
            UndefinedFunctionOffsets.Add(new(label, caller, caller.Function));
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
            using RegisterUsage.Auto reg = Registers.GetFree(BuiltinType.I32.GetBitWidth(this));
            Code.Emit(Opcode.Move, reg.Register, 0);
            if (!GenerateSize(paramType, reg.Register, out PossibleDiagnostic? generateSizeError))
            { Diagnostics.Add(generateSizeError.ToError(anyCall)); }
            Push(reg.Register);
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
            Diagnostics.Add(Diagnostic.Critical($"Argument types of caller \"{anyCall}\" doesn't match with callee \"{functionType}\"", anyCall).WithSuberrors(argumentError?.ToError(anyCall)));
            return;
        }

        AddComment($"Call (runtime) \"{functionType}\" {{");

        if (functionType.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(functionType.ReturnType.GetSize(this, Diagnostics, anyCall), false);
            AddComment($"}}");
        }

        Stack<CompiledCleanup> parameterCleanup = new();
        if (functionType.HasClosure)
        {
            GenerateCodeForStatement(anyCall.Function);
            parameterCleanup.Push(new CompiledCleanup()
            {
                Location = anyCall.Function.Location,
                TrashType = anyCall.Function.Type,
            });
        }
        GenerateCodeForParameterPassing(anyCall.Arguments, functionType, parameterCleanup);

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
        CompiledStatementWithValue Left = @operator.Left;
        CompiledStatementWithValue Right = @operator.Right;

        if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.BinaryOperatorFetchSkip)
            && @operator.Operator
            is CompiledBinaryOperatorCall.LogicalAND
            or CompiledBinaryOperatorCall.LogicalOR
            or CompiledBinaryOperatorCall.BitwiseAND
            or CompiledBinaryOperatorCall.BitwiseOR
            or CompiledBinaryOperatorCall.BitwiseXOR
            or CompiledBinaryOperatorCall.Multiplication
            or CompiledBinaryOperatorCall.Addition
            or CompiledBinaryOperatorCall.CompEQ
            or CompiledBinaryOperatorCall.CompNEQ)
        {
            if (Left is CompiledEvaluatedValue && Right is not CompiledEvaluatedValue)
            {
                (Left, Right) = (Right, Left);
            }
        }

        BitWidth leftBitWidth = Left.Type.GetBitWidth(this, Diagnostics, Left);
        BitWidth rightBitWidth = Right.Type.GetBitWidth(this, Diagnostics, Right);
        BitWidth bitWidth = StatementCompiler.MaxBitWidth(leftBitWidth, rightBitWidth);

        InstructionLabel endLabel = Code.DefineLabel();

        GenerateCodeForStatement(Left);

        if (@operator.Operator == CompiledBinaryOperatorCall.LogicalAND)
        {
            PushFrom(StackTop, Left.Type.GetSize(this, Diagnostics, Left));

            using (RegisterUsage.Auto regLeft = Registers.GetFree(leftBitWidth))
            {
                PopTo(regLeft.Register);
                Code.Emit(Opcode.Compare, regLeft.Register, 0);
                Code.Emit(Opcode.JumpIfEqual, endLabel.Relative());
            }
        }
        else if (@operator.Operator == CompiledBinaryOperatorCall.LogicalOR)
        {
            PushFrom(StackTop, Left.Type.GetSize(this, Diagnostics, Left));

            using (RegisterUsage.Auto regLeft = Registers.GetFree(leftBitWidth))
            {
                PopTo(regLeft.Register);
                Code.Emit(Opcode.Compare, regLeft.Register, 0);
                Code.Emit(Opcode.JumpIfNotEqual, endLabel.Relative());
            }
        }

        InstructionOperand rightOperand;
        RegisterUsage.Auto? regRight;

        if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.BinaryOperatorFetchSkip)
            && Right is CompiledEvaluatedValue rightValue)
        {
            rightOperand = new InstructionOperand(rightValue.Value);
            regRight = null;
        }
        else
        {
            GenerateCodeForStatement(Right);
            regRight = Registers.GetFree(bitWidth);
            PopTo(regRight.Value.Register, rightBitWidth);
            rightOperand = regRight.Value.Register;
        }

        bool isFloat = Left.Type.SameAs(BasicType.F32)
            || Right.Type.SameAs(BasicType.F32);
        bool isSigned = Left.Type.SameAs(BasicType.F32)
            || Left.Type.SameAs(BasicType.I8)
            || Left.Type.SameAs(BasicType.I16)
            || Left.Type.SameAs(BasicType.I32);

        using (RegisterUsage.Auto regLeft = Registers.GetFree(bitWidth))
        using (regRight)
        {
            PopTo(regLeft.Register, leftBitWidth);

            switch (@operator.Operator)
            {
                case CompiledBinaryOperatorCall.Addition:
                    Code.Emit(isFloat ? Opcode.FMathAdd : Opcode.MathAdd, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.Subtraction:
                    Code.Emit(isFloat ? Opcode.FMathSub : Opcode.MathSub, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.Multiplication:
                    Code.Emit(isFloat ? Opcode.FMathMult : isSigned ? Opcode.MathMultS : Opcode.MathMultU, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.Division:
                    Code.Emit(isFloat ? Opcode.FMathDiv : isSigned ? Opcode.MathDivS : Opcode.MathDivU, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.Modulo:
                    Code.Emit(isFloat ? Opcode.FMathMod : isSigned ? Opcode.MathModS : Opcode.MathModU, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.LogicalAND:
                    Code.Emit(Opcode.LogicAND, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.LogicalOR:
                    Code.Emit(Opcode.LogicOR, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.BitwiseAND:
                    Code.Emit(Opcode.BitsAND, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.BitwiseOR:
                    Code.Emit(Opcode.BitsOR, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.BitwiseXOR:
                    Code.Emit(Opcode.BitsXOR, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.BitshiftLeft:
                    Code.Emit(Opcode.BitsShiftLeft, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;
                case CompiledBinaryOperatorCall.BitshiftRight:
                    Code.Emit(Opcode.BitsShiftRight, regLeft.Register, rightOperand);
                    Push(regLeft.Register);
                    break;

                case CompiledBinaryOperatorCall.CompEQ:
                {
                    InstructionLabel labelSkipFalse = Code.DefineLabel();
                    InstructionLabel labelSkipTrue = Code.DefineLabel();
                    Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                    Code.Emit(Opcode.JumpIfEqual, labelSkipFalse.Relative());
                    Push(false);
                    Code.Emit(Opcode.Jump, labelSkipTrue.Relative());
                    Code.MarkLabel(labelSkipFalse);
                    Push(true);
                    Code.MarkLabel(labelSkipTrue);
                    break;
                }

                case CompiledBinaryOperatorCall.CompNEQ:
                {
                    InstructionLabel labelSkipFalse = Code.DefineLabel();
                    InstructionLabel labelSkipTrue = Code.DefineLabel();
                    Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                    Code.Emit(Opcode.JumpIfNotEqual, labelSkipFalse.Relative());
                    Push(false);
                    Code.Emit(Opcode.Jump, labelSkipTrue.Relative());
                    Code.MarkLabel(labelSkipFalse);
                    Push(true);
                    Code.MarkLabel(labelSkipTrue);
                    break;
                }

                case CompiledBinaryOperatorCall.CompGT:
                {
                    InstructionLabel labelSkipFalse = Code.DefineLabel();
                    InstructionLabel labelSkipTrue = Code.DefineLabel();
                    Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                    Code.Emit(isSigned ? Opcode.JumpIfLessOrEqualS : Opcode.JumpIfLessOrEqualU, labelSkipFalse.Relative());
                    Push(true);
                    Code.Emit(Opcode.Jump, labelSkipTrue.Relative());
                    Code.MarkLabel(labelSkipFalse);
                    Push(false);
                    Code.MarkLabel(labelSkipTrue);
                    break;
                }

                case CompiledBinaryOperatorCall.CompGEQ:
                {
                    InstructionLabel labelSkipFalse = Code.DefineLabel();
                    InstructionLabel labelSkipTrue = Code.DefineLabel();
                    Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                    Code.Emit(isSigned ? Opcode.JumpIfLessS : Opcode.JumpIfLessU, labelSkipFalse.Relative());
                    Push(true);
                    Code.Emit(Opcode.Jump, labelSkipTrue.Relative());
                    Code.MarkLabel(labelSkipFalse);
                    Push(false);
                    Code.MarkLabel(labelSkipTrue);
                    break;
                }

                case CompiledBinaryOperatorCall.CompLT:
                {
                    InstructionLabel labelSkipFalse = Code.DefineLabel();
                    InstructionLabel labelSkipTrue = Code.DefineLabel();
                    Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                    Code.Emit(isSigned ? Opcode.JumpIfLessS : Opcode.JumpIfLessU, labelSkipFalse.Relative());
                    Push(false);
                    Code.Emit(Opcode.Jump, labelSkipTrue.Relative());
                    Code.MarkLabel(labelSkipFalse);
                    Push(true);
                    Code.MarkLabel(labelSkipTrue);
                    break;
                }

                case CompiledBinaryOperatorCall.CompLEQ:
                {
                    InstructionLabel labelSkipFalse = Code.DefineLabel();
                    InstructionLabel labelSkipTrue = Code.DefineLabel();
                    Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                    Code.Emit(isSigned ? Opcode.JumpIfLessOrEqualS : Opcode.JumpIfLessOrEqualU, labelSkipFalse.Relative());
                    Push(false);
                    Code.Emit(Opcode.Jump, labelSkipTrue.Relative());
                    Code.MarkLabel(labelSkipFalse);
                    Push(true);
                    Code.MarkLabel(labelSkipTrue);
                    break;
                }

                default:
                    throw new NotImplementedException();
            }
        }

        Code.MarkLabel(endLabel);
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

                using (RegisterUsage.Auto reg = Registers.GetFree(bitWidth))
                {
                    InstructionLabel labelSkipFalse = Code.DefineLabel();
                    InstructionLabel labelSkipTrue = Code.DefineLabel();
                    PopTo(reg.Register);
                    Code.Emit(Opcode.Compare, reg.Register, 0);
                    Code.Emit(Opcode.JumpIfEqual, labelSkipFalse.Relative());
                    Push(false);
                    Code.Emit(Opcode.Jump, labelSkipTrue.Relative());
                    Code.MarkLabel(labelSkipFalse);
                    Push(true);
                    Code.MarkLabel(labelSkipTrue);
                }

                return;
            }
            case CompiledUnaryOperatorCall.BinaryNOT:
            {
                GenerateCodeForStatement(@operator.Left);

                using (RegisterUsage.Auto reg = Registers.GetFree(bitWidth))
                {
                    PopTo(reg.Register);
                    Code.Emit(Opcode.BitsNOT, reg.Register);
                    Push(reg.Register);
                }

                return;
            }
            case CompiledUnaryOperatorCall.UnaryMinus:
            {
                GenerateCodeForStatement(@operator.Left);

                bool isFloat = @operator.Left.Type.SameAs(BasicType.F32);

                using (RegisterUsage.Auto left = Registers.GetFree(bitWidth))
                using (RegisterUsage.Auto right = Registers.GetFree(bitWidth))
                {
                    PopTo(right.Register);
                    Code.Emit(Opcode.Move, left.Register, new InstructionOperand(isFloat ? new CompiledValue(0f) : new CompiledValue(0)));

                    Code.Emit(isFloat ? Opcode.FMathSub : Opcode.MathSub, left.Register, right.Register);

                    Push(left.Register);
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

        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            // Save pointer
            Code.Emit(Opcode.Move, reg.Register, (InstructionOperand)StackTop);

            if (stringInstance.IsASCII)
            {
                for (int i = 0; i < stringInstance.Value.Length; i++)
                {
                    Code.Emit(Opcode.Move, reg.Register.ToPtr(i * type.GetSize(this), type.GetBitWidth(this)), (byte)stringInstance.Value[i]);
                }

                Code.Emit(Opcode.Move, reg.Register.ToPtr(stringInstance.Value.Length * type.GetSize(this), type.GetBitWidth(this)), (byte)'\0');
            }
            else
            {
                for (int i = 0; i < stringInstance.Value.Length; i++)
                {
                    Code.Emit(Opcode.Move, reg.Register.ToPtr(i * type.GetSize(this), type.GetBitWidth(this)), stringInstance.Value[i]);
                }

                Code.Emit(Opcode.Move, reg.Register.ToPtr(stringInstance.Value.Length * type.GetSize(this), type.GetBitWidth(this)), '\0');
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
    void GenerateCodeForStatement(CompiledLambda compiledLambda)
    {
        InstructionLabel label = LabelForDefinition(compiledLambda);

        if (compiledLambda.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(label, compiledLambda, compiledLambda)); }

        if (compiledLambda.CapturedLocals.IsDefaultOrEmpty)
        {
            Push(label.Absolute());
        }
        else
        {
            if (compiledLambda.Allocator is null) throw new UnreachableException();
            GenerateCodeForStatement(compiledLambda.Allocator);

            using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
            {
                Code.Emit(Opcode.Move, reg.Register, (InstructionOperand)StackTop);

                AddComment("Save function pointer:");
                Code.Emit(Opcode.Move, reg.Register.ToPtr(0, PointerBitWidth), label.Absolute());

                int offset = PointerSize;
                foreach (CapturedLocal capturedLocal in compiledLambda.CapturedLocals)
                {
                    if (capturedLocal.Variable is not null)
                    {
                        int size = capturedLocal.Variable.Type.GetSize(this);
                        AddComment($"Capture variable `{capturedLocal.Variable.Identifier}`:");
                        GenerateCodeForStatement(new CompiledVariableGetter()
                        {
                            Variable = capturedLocal.Variable,
                            Location = compiledLambda.Location,
                            SaveValue = true,
                            Type = capturedLocal.Variable.Type,
                        });
                        PopTo(new AddressOffset(new AddressRegisterPointer(reg.Register), offset), size);
                        offset += size;
                    }
                    else if (capturedLocal.Parameter is not null)
                    {
                        int size = capturedLocal.Parameter.Type.GetSize(this);
                        AddComment($"Capture variable `{capturedLocal.Parameter.Identifier}`:");
                        GenerateCodeForStatement(new CompiledParameterGetter()
                        {
                            Variable = capturedLocal.Parameter,
                            Location = compiledLambda.Location,
                            SaveValue = true,
                            Type = capturedLocal.Parameter.Type,
                        });
                        PopTo(new AddressOffset(new AddressRegisterPointer(reg.Register), offset), size);
                        offset += size;
                    }
                    else
                    {
                        throw new UnreachableException();
                    }
                }
            }
        }
    }
    void GenerateCodeForStatement(CompilerVariableGetter compilerVariableGetter)
    {
        Push(false);
    }
    void GenerateCodeForStatement(RegisterGetter variable, GeneralType? expectedType = null, bool resolveReference = true)
    {
        Code.Emit(Opcode.Push, variable.Register);
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
        PushFrom(GetVariableAddress(variable.Variable), variable.Type.GetSize(this, Diagnostics, variable));
    }
    void GenerateCodeForStatement(FunctionAddressGetter variable, GeneralType? expectedType = null, bool resolveReference = true)
    {
        IHaveInstructionOffset compiledFunction = variable.Function;
        InstructionLabel label = LabelForDefinition(compiledFunction);

        if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(label, variable, compiledFunction)); }

        Push(label.Absolute());
    }
    void GenerateCodeForStatement(InstructionLabelAddressGetter variable, GeneralType? expectedType = null, bool resolveReference = true)
    {
        InstructionLabel label;

        if (!GeneratedInstructionLabels.TryGetValue(variable.InstructionLabel, out GeneratedInstructionLabel? instructionLabel))
        {
            instructionLabel = GeneratedInstructionLabels[variable.InstructionLabel] = new()
            {
                InstructionOffset = InvalidFunctionAddress,
            };
            label = LabelForDefinition(instructionLabel);
            UndefinedInstructionLabels.Add(new UndefinedOffset(label, variable, instructionLabel));
        }
        else
        {
            label = LabelForDefinition(instructionLabel);
        }

        Push(label.Absolute());
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

        using (RegisterUsage.Auto reg = Registers.GetFree(pointerType.GetBitWidth(this, Diagnostics, pointer)))
        {
            PopTo(reg.Register);
            PushFrom(new AddressRegisterPointer(reg.Register), pointerType.To.GetSize(this, Diagnostics, pointer.To));
        }
    }
    void GenerateCodeForStatement(CompiledWhileLoop whileLoop)
    {
        CompiledBlock block = CompiledBlock.CreateIfNot(whileLoop.Body);

        AddComment("while (...) {");

        CompiledScope scope = OnScopeEnter(block, false);

        AddComment("Condition");
        InstructionLabel conditionOffset = Code.MarkLabel();

        InstructionLabel endLabel = Code.DefineLabel();
        GenerateCodeForCondition(whileLoop.Condition, endLabel);

        BreakInstructions.Push(new ControlFlowFrame(endLabel));

        GenerateCodeForStatement(block, true);

        AddComment("Jump Back");
        Code.Emit(Opcode.Jump, conditionOffset.Relative());

        ReturnInstructions.Last.IsSkipping = false;

        Code.MarkLabel(endLabel);

        OnScopeExit(block.Location.Position.After(), block.Location.File, scope);

        AddComment("}");

        BreakInstructions.Pop();
    }
    void GenerateCodeForStatement(CompiledForLoop forLoop)
    {
        AddComment("for (...) {");

        CompiledScope scope = OnScopeEnter(forLoop.Location.Position, forLoop.Location.File, forLoop.Initialization is CompiledVariableDeclaration wah ? Enumerable.Repeat(wah, 1) : Enumerable.Empty<CompiledVariableDeclaration>(), false);

        if (forLoop.Initialization is not null)
        {
            AddComment("For-loop variable");
            GenerateCodeForStatement(forLoop.Initialization);
        }

        AddComment("For-loop condition");
        InstructionLabel conditionOffsetFor = Code.MarkLabel();

        InstructionLabel endLabel = Code.DefineLabel();

        GenerateCodeForCondition(forLoop.Condition, endLabel);

        BreakInstructions.Push(new ControlFlowFrame(endLabel));
        GenerateCodeForStatement(forLoop.Body);

        AddComment("For-loop expression");
        GenerateCodeForStatement(forLoop.Step);

        AddComment("Jump back");
        Code.Emit(Opcode.Jump, conditionOffsetFor.Relative());

        ReturnInstructions.Last.IsSkipping = false;

        Code.MarkLabel(endLabel);

        OnScopeExit(forLoop.Location.Position.After(), forLoop.Location.File, scope);

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledIf @if)
    {
        InstructionLabel endLabel = Code.DefineLabel();

        CompiledBranch? ifSegment = @if;

        while (true)
        {
            if (ifSegment is CompiledIf partIf)
            {
                AddComment("if (...) {");

                AddComment("If condition");

                InstructionLabel falseLabel = Code.DefineLabel();
                GenerateCodeForCondition(partIf.Condition, falseLabel);

                GenerateCodeForStatement(partIf.Body);

                AddComment("If jump-to-end");
                Code.Emit(Opcode.Jump, endLabel.Relative());

                ReturnInstructions.Last.IsSkipping = false;
                if (BreakInstructions.Count > 0) BreakInstructions.Last.IsSkipping = false;

                AddComment("}");

                Code.MarkLabel(falseLabel);

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

        Code.MarkLabel(endLabel);
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
        CompiledConstructorDefinition compiledFunction = constructorCall.Function;
        CompiledFunction f = Functions.First(v => v.Function == compiledFunction);

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

            Code.Emit(Opcode.Push, Register.StackPointer);

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

        InstructionLabel label = LabelForDefinition(compiledFunction);
        Call(label, constructorCall, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables));

        if (compiledFunction.InstructionOffset == InvalidFunctionAddress)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(label, constructorCall, compiledFunction)); }

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
                using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
                {
                    PopTo(reg.Register);
                    PushFrom(new AddressRegisterPointer(
                        reg.Register),
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

            using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
            {
                PopTo(reg.Register);
                PushFrom(new AddressOffset(
                    new AddressRegisterPointer(reg.Register),
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
            using (RegisterUsage.Auto regPtr = Registers.GetFree(PointerBitWidth))
            {
                if (!GetAddress(index.Base, out Address? address, out PossibleDiagnostic? error))
                {
                    Diagnostics.Add(error.ToError(index.Base));
                    return;
                }
                GenerateAddressResolver(address);
                PopTo(regPtr.Register);

                if (!indexType.Is<BuiltinType>())
                {
                    Diagnostics.Add(Diagnostic.Critical($"Index must be a builtin type (i.e. int) and not \"{indexType}\"", index.Index));
                    return;
                }

                if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.IndexerFetchSkip)
                    && index.Index is CompiledEvaluatedValue evaluatedIndex)
                {
                    Code.Emit(Opcode.MathAdd, regPtr.Register, (int)evaluatedIndex.Value * arrayType.Of.GetSize(this, Diagnostics, index.Base));
                }
                else
                {
                    GenerateCodeForStatement(index.Index);
                    using (RegisterUsage.Auto regIndex = Registers.GetFree(indexType.GetBitWidth(this, Diagnostics, index.Index)))
                    {
                        PopTo(regIndex.Register);
                        Code.Emit(Opcode.MathMultU, regIndex.Register, arrayType.Of.GetSize(this, Diagnostics, index.Base));
                        Code.Emit(Opcode.MathAdd, regPtr.Register, regIndex.Register);
                    }
                }

                PushFrom(new AddressRegisterPointer(regPtr.Register), arrayType.Of.GetSize(this, Diagnostics, index.Base));
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
                using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
                {
                    PopTo(reg.Register);
                    Code.Emit(Opcode.MathAdd, reg.Register, addressOffset.Offset);
                    Push(reg.Register);
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

                if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.IndexerFetchSkip)
                    && runtimeIndex.IndexValue is CompiledEvaluatedValue evaluatedIndex)
                {
                    int indexValue = (int)evaluatedIndex.Value;
                    using (RegisterUsage.Auto regBase = Registers.GetFree(PointerBitWidth))
                    {
                        PopTo(regBase.Register);
                        Code.Emit(Opcode.MathAdd, regBase.Register, indexValue * runtimeIndex.ElementSize);
                        Push(regBase.Register);
                    }
                }
                else
                {
                    GenerateCodeForStatement(runtimeIndex.IndexValue);

                    using (RegisterUsage.Auto regIndex = Registers.GetFree(indexType.GetBitWidth(this, Diagnostics, runtimeIndex.IndexValue)))
                    {
                        PopTo(regIndex.Register);
                        Code.Emit(Opcode.MathMultU, regIndex.Register, runtimeIndex.ElementSize);
                        using (RegisterUsage.Auto regBase = Registers.GetFree(PointerBitWidth))
                        {
                            PopTo(regBase.Register);
                            Code.Emit(Opcode.MathAdd, regBase.Register, regIndex.Register);
                            Push(regBase.Register);
                        }
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
            Code.Emit(Opcode.FFrom, (InstructionOperand)StackTop, (InstructionOperand)StackTop);
            return;
        }

        // i32 -> f32
        if (statementType.SameAs(BuiltinType.I32) &&
            targetType.SameAs(BuiltinType.F32))
        {
            GenerateCodeForStatement(typeCast.Value);
            Code.Emit(Opcode.FTo, (InstructionOperand)StackTop, (InstructionOperand)StackTop);
            return;
        }

        if (statementType.Is(out FunctionType? functionTypeFrom)
            && targetType.Is(out FunctionType? functionTypeTo))
        {
            if (functionTypeFrom.HasClosure == functionTypeTo.HasClosure)
            {
                GenerateCodeForStatement(typeCast.Value);
                return;
            }

            if (!functionTypeFrom.HasClosure && functionTypeTo.HasClosure)
            {
                if (typeCast.Allocator is null) throw new NotImplementedException();
                GenerateCodeForStatement(typeCast.Allocator);
                PushFrom(StackTop, PointerSize);
                using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
                {
                    PopTo(reg.Register);
                    CheckPointerNull(reg.Register);
                    GenerateCodeForStatement(typeCast.Value);
                    PopTo(new AddressRegisterPointer(reg.Register), PointerSize);
                }
                return;
            }

            if (functionTypeFrom.HasClosure && !functionTypeTo.HasClosure)
            {
                throw new NotImplementedException();
            }
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
        if (block.Statements.Length == 0) return;

        CompiledScope scope = ignoreScope ? default : OnScopeEnter(block, false);

        AddComment("Statements {");
        foreach (CompiledStatement v in block.Statements)
        {
            if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.TrimReturnBreak) &&
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
        int startInstruction = Code.Offset;

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
            case CompiledLambda v: GenerateCodeForStatement(v); break;
            case EmptyStatement: break;
            case CompilerVariableGetter v: GenerateCodeForStatement(v); break;
            default: throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\"");
        }

        if (startInstruction != Code.Offset)
        {
            DebugInfo?.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (startInstruction, Code.Offset - 1),
                Location = statement.Location,
            });
        }
    }

    void GenerateCodeForCondition(CompiledStatementWithValue statement, InstructionLabel falseLabel)
    {
        bool v = false;
        GenerateCodeForCondition(statement, falseLabel, ref v);
    }
    void GenerateCodeForCondition(CompiledStatementWithValue statement, InstructionLabel falseLabel, ref bool didJump)
    {
        switch (statement)
        {
            case CompiledBinaryOperatorCall v:
                GenerateCodeForCondition(v, falseLabel, ref didJump);
                break;
            case CompiledUnaryOperatorCall v:
                GenerateCodeForCondition(v, falseLabel, ref didJump);
                break;
            default:
                GenerateCodeForStatement(statement);
                break;
        }

        if (!didJump)
        {
            using (RegisterUsage.Auto reg = Registers.GetFree(statement.Type.GetBitWidth(this, Diagnostics, statement)))
            {
                PopTo(reg.Register);
                Code.Emit(Opcode.Compare, reg.Register, 0);
                Code.Emit(Opcode.JumpIfEqual, falseLabel.Relative());
                didJump = true;
            }
        }
    }
    void GenerateCodeForCondition(CompiledUnaryOperatorCall @operator, InstructionLabel falseLabel, ref bool didJump)
    {
        switch (@operator.Operator)
        {
            case CompiledUnaryOperatorCall.LogicalNOT:
            {
                InstructionLabel subFalseLabel = Code.DefineLabel();
                GenerateCodeForCondition(@operator.Left, subFalseLabel);

                Code.Emit(Opcode.Jump, falseLabel.Relative());
                didJump = true;

                Code.MarkLabel(subFalseLabel);
                return;
            }
            case CompiledUnaryOperatorCall.BinaryNOT:
            {
                BitWidth bitWidth = @operator.Left.Type.GetBitWidth(this, Diagnostics, @operator.Left);

                GenerateCodeForStatement(@operator.Left);

                using (RegisterUsage.Auto reg = Registers.GetFree(bitWidth))
                {
                    PopTo(reg.Register);
                    Code.Emit(Opcode.BitsNOT, reg.Register);
                    Push(reg.Register);
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
    void GenerateCodeForCondition(CompiledBinaryOperatorCall @operator, InstructionLabel falseLabel, ref bool didJump)
    {
        BitWidth leftBitWidth = @operator.Left.Type.GetBitWidth(this, Diagnostics, @operator.Left);
        BitWidth rightBitWidth = @operator.Right.Type.GetBitWidth(this, Diagnostics, @operator.Right);
        BitWidth bitWidth = StatementCompiler.MaxBitWidth(leftBitWidth, rightBitWidth);

        if (@operator.Operator == CompiledBinaryOperatorCall.LogicalAND)
        {
            GenerateCodeForCondition(@operator.Left, falseLabel, ref didJump);
            GenerateCodeForCondition(@operator.Right, falseLabel, ref didJump);
        }
        else if (@operator.Operator == CompiledBinaryOperatorCall.LogicalOR)
        {
            InstructionLabel subFalseLabel = Code.DefineLabel();
            GenerateCodeForCondition(@operator.Left, subFalseLabel);

            InstructionLabel trueLabel = Code.DefineLabel();
            Code.Emit(Opcode.Jump, trueLabel.Relative());

            Code.MarkLabel(subFalseLabel);

            GenerateCodeForCondition(@operator.Right, falseLabel, ref didJump);

            Code.MarkLabel(trueLabel);
        }
        else
        {
            CompiledStatementWithValue Left = @operator.Left;
            CompiledStatementWithValue Right = @operator.Right;

            if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.BinaryOperatorFetchSkip)
                && @operator.Operator
                is CompiledBinaryOperatorCall.LogicalAND
                or CompiledBinaryOperatorCall.LogicalOR
                or CompiledBinaryOperatorCall.BitwiseAND
                or CompiledBinaryOperatorCall.BitwiseOR
                or CompiledBinaryOperatorCall.BitwiseXOR
                or CompiledBinaryOperatorCall.Multiplication
                or CompiledBinaryOperatorCall.Addition
                or CompiledBinaryOperatorCall.CompEQ
                or CompiledBinaryOperatorCall.CompNEQ)
            {
                if (Left is CompiledEvaluatedValue && Right is not CompiledEvaluatedValue)
                {
                    (Left, Right) = (Right, Left);
                }
            }

            GenerateCodeForStatement(Left);

            InstructionOperand rightOperand;
            RegisterUsage.Auto? regRight;

            if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.BinaryOperatorFetchSkip)
                && Right is CompiledEvaluatedValue rightValue)
            {
                rightOperand = new InstructionOperand(rightValue.Value);
                regRight = null;
            }
            else
            {
                GenerateCodeForStatement(Right);
                regRight = Registers.GetFree(bitWidth);
                PopTo(regRight.Value.Register, rightBitWidth);
                rightOperand = regRight.Value.Register;
            }

            bool isFloat = Left.Type.SameAs(BasicType.F32)
                || Right.Type.SameAs(BasicType.F32);
            bool isSigned = Left.Type.SameAs(BasicType.F32)
                || Left.Type.SameAs(BasicType.I8)
                || Left.Type.SameAs(BasicType.I16)
                || Left.Type.SameAs(BasicType.I32);

            using (RegisterUsage.Auto regLeft = Registers.GetFree(bitWidth))
            using (regRight)
            {
                PopTo(regLeft.Register, leftBitWidth);

                switch (@operator.Operator)
                {
                    case CompiledBinaryOperatorCall.Addition:
                        Code.Emit(isFloat ? Opcode.FMathAdd : Opcode.MathAdd, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.Subtraction:
                        Code.Emit(isFloat ? Opcode.FMathSub : Opcode.MathSub, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.Multiplication:
                        Code.Emit(isFloat ? Opcode.FMathMult : isSigned ? Opcode.MathMultS : Opcode.MathMultU, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.Division:
                        Code.Emit(isFloat ? Opcode.FMathDiv : isSigned ? Opcode.MathDivS : Opcode.MathDivU, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.Modulo:
                        Code.Emit(isFloat ? Opcode.FMathMod : isSigned ? Opcode.MathModS : Opcode.MathModU, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.LogicalAND:
                        Code.Emit(Opcode.LogicAND, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.LogicalOR:
                        Code.Emit(Opcode.LogicOR, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.BitwiseAND:
                        Code.Emit(Opcode.BitsAND, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.BitwiseOR:
                        Code.Emit(Opcode.BitsOR, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.BitwiseXOR:
                        Code.Emit(Opcode.BitsXOR, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.BitshiftLeft:
                        Code.Emit(Opcode.BitsShiftLeft, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;
                    case CompiledBinaryOperatorCall.BitshiftRight:
                        Code.Emit(Opcode.BitsShiftRight, regLeft.Register, rightOperand);
                        Push(regLeft.Register);
                        break;

                    case CompiledBinaryOperatorCall.CompEQ:
                        Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                        Code.Emit(Opcode.JumpIfNotEqual, falseLabel.Relative());
                        didJump = true;
                        break;

                    case CompiledBinaryOperatorCall.CompNEQ:
                        Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                        Code.Emit(Opcode.JumpIfEqual, falseLabel.Relative());
                        didJump = true;
                        break;

                    case CompiledBinaryOperatorCall.CompGT:
                        Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                        Code.Emit(isSigned ? Opcode.JumpIfLessOrEqualS : Opcode.JumpIfLessOrEqualU, falseLabel.Relative());
                        didJump = true;
                        break;

                    case CompiledBinaryOperatorCall.CompGEQ:
                        Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                        Code.Emit(isSigned ? Opcode.JumpIfLessS : Opcode.JumpIfLessU, falseLabel.Relative());
                        didJump = true;
                        break;

                    case CompiledBinaryOperatorCall.CompLT:
                        Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                        Code.Emit(isSigned ? Opcode.JumpIfGreaterOrEqualS : Opcode.JumpIfGreaterOrEqualU, falseLabel.Relative());
                        didJump = true;
                        break;

                    case CompiledBinaryOperatorCall.CompLEQ:
                        Code.Emit(isFloat ? Opcode.CompareF : Opcode.Compare, regLeft.Register, rightOperand);
                        Code.Emit(isSigned ? Opcode.JumpIfGreaterS : Opcode.JumpIfGreaterU, falseLabel.Relative());
                        didJump = true;
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
        GenerateCodeForStatement(localVariableSetter.Value, localVariableSetter.Variable.Type);
        PopTo(GetVariableAddress(localVariableSetter.Variable), localVariableSetter.Variable.Type.GetSize(this, Diagnostics, localVariableSetter.Variable));
        // localVariableSetter.Variable.Variable.IsInitialized = true;
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

        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            PopTo(new AddressRegisterPointer(reg.Register), statementToSet.Value.Type.GetSize(this, Diagnostics, statementToSet.Value));
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
                Instructions = (Code.Offset, Code.Offset),
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
        scopeDebug.Location.Instructions.End = Code.Offset - 1;
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

    void GenerateCodeForFunction(ICompiledFunctionDefinition function, CompiledBlock body)
    {
        if (!GeneratedFunctions.Add(function)) return;
        Code.MarkLabel(LabelForDefinition(function));

        function.InstructionOffset = Code.Offset;
        for (int i = UndefinedFunctionOffsets.Count - 1; i >= 0; i--)
        {
            UndefinedOffset item = UndefinedFunctionOffsets[i];
            if (item.Called != function) continue;
            Code.MarkLabel(item.Label);
            UndefinedFunctionOffsets.RemoveAt(i);
        }

        CurrentContext = function;
        InFunction = true;
        FunctionFlags f = function is CompiledLambda l ? l.Flags : Functions.First(v => v.Function == function).Flags;

        CompiledParameters.Clear();
        CompiledLocalVariables.Clear();
        ReturnInstructions.Clear();
        ScopeSizes.Push(0);
        HasCapturedGlobalVariables = f.HasFlag(FunctionFlags.CapturesGlobalVariables);
        int savedInstructionLabelCount = CompiledInstructionLabels.Count;

        CompiledParameters.AddRange(function.Parameters);

        int instructionStart = Code.Offset;

        if (function is IHaveCompiledType functionWithType)
        { CurrentReturnType = functionWithType.Type; }
        else
        { CurrentReturnType = BuiltinType.Void; }

        if (body is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{function.ToReadable()}\" does not have a body", (ILocated)function));
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
                Size = returnType.Type.GetSize(this, Diagnostics, (ILocated)function),
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

        InstructionLabel returnLabel = Code.DefineLabel();
        ReturnInstructions.Push(new ControlFlowFrame(returnLabel));

        GenerateCodeForStatement(body, true);

        CurrentReturnType = null;

        OnScopeExit(body.Location.Position.After(), body.Location.File, scope);

        Code.MarkLabel(returnLabel);
        ReturnInstructions.Pop();

        AddComment("Return");
        Return(body.Location.After());

        if (body != null) AddComment("}");

        if (function is FunctionThingDefinition functionDefinition)
        {
            DebugInfo?.FunctionInformation.Add(new FunctionInformation()
            {
                IsValid = true,
                Function = functionDefinition,
                TypeArguments = TypeArguments.ToImmutableDictionary(),
                Instructions = (instructionStart, Code.Offset),
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
        HasCapturedGlobalVariables = false;
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
                Instructions = (Code.Offset, Code.Offset),
                Location = statements.Select(v => v.Location).Aggregate((a, b) => a.Union(b)),
            },
            Stack = new List<StackElementInformation>(),
        });

        ScopeSizes.Push(0);

        AddComment("TopLevelStatements {");

        AddComment("Create stack frame");
        Push(Register.BasePointer);
        Code.Emit(Opcode.Move, Register.BasePointer, Register.StackPointer);

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
        InstructionLabel returnLabel = Code.DefineLabel();
        ReturnInstructions.Push(new ControlFlowFrame(returnLabel));

        CurrentReturnType = ExitCodeType;

        if (statements.Length > 0)
        {
            AddComment("Statements {");
            foreach (CompiledStatement statement in statements)
            { GenerateCodeForStatement(statement); }
            AddComment("}");
        }

        Code.MarkLabel(returnLabel);

        CurrentReturnType = null;

        AddComment("Pop stack frame");
        PopTo(Register.BasePointer);
        if (ScopeSizes.Pop() != 0) { }

        AddComment("}");

        ScopeInformation scope = CurrentScopeDebug.Pop();
        scope.Location.Instructions.End = Code.Offset - 1;
        DebugInfo?.ScopeInformation.Add(scope);
    }

    #endregion

    BBLangGeneratorResult GenerateCode(CompilerResult compilerResult, MainGeneratorSettings settings)
    {
        ScopeSizes.Push(0);
        HasCapturedGlobalVariables = true;

        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (Code.Offset, Code.Offset),
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

        if (GlobalVariablesSize > 0)
        {
            AddComment("Allocate global variables {");
            StackAlloc(GlobalVariablesSize, false);
            AddComment("}");
        }

        {
            // Absolute global offset

            AddComment("Abs global address");

            //using (RegisterUsage.Auto reg = Registers.GetFree())
            //{
            // Code.AddInstruction(Opcode.Move, reg.Register, Register.StackPointer);
            // Code.AddInstruction(Opcode.MathAdd, reg.Register, GlobalVariablesSize);
            Push(Register.StackPointer);
            //}

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

        if (Settings.CleanupGlobalVaraibles && globalVariablesCleanup.Count > 0)
        {
            AddComment("Cleanup global variables {");
            CleanupVariables(globalVariablesCleanup.ToImmutableArray(), default, true);
            AddComment("}");
        }

        Code.Emit(Opcode.Exit);

        HasCapturedGlobalVariables = false;

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
            foreach (UndefinedOffset undefinedOffset in UndefinedFunctionOffsets.ToArray())
            {
                if (undefinedOffset.Called is CompiledLambda compiledLambda)
                {
                    GenerateCodeForFunction(compiledLambda, compiledLambda.Block);
                    goto ok;
                }

                foreach ((ICompiledFunctionDefinition function, CompiledBlock body) in compilerResult.Functions)
                {
                    if (undefinedOffset.Called != function) continue;
                    GenerateCodeForFunction(function, body);
                    goto ok;
                }

                if (!Diagnostics.HasErrors) Diagnostics.Add(Diagnostic.Critical($"Function {undefinedOffset.Called} wasn't compiled for some reason", undefinedOffset.CallerLocation));
                goto failed;
            ok:;
            }
        }
    failed:

        SetUndefinedFunctionOffsets(UndefinedInstructionLabels, true);

        // {
        //     ScopeInformation scope = CurrentScopeDebug.Pop();
        //     scope.Location.Instructions.End = Code.Offset - 1;
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
            CompiledFunction e = Functions.First(v => v.Function == f);

            int returnValueSize = f.ReturnSomething ? f.Type.GetSize(this, Diagnostics, f.TypeToken) : 0;
            int argumentsSize = 0;
            foreach (CompiledParameter p in f.Parameters)
            { argumentsSize += p.Type.GetSize(this, Diagnostics, ((FunctionDefinition)f).Type); }

            exposedFunctions[f.ExposedFunctionName] = new(f.ExposedFunctionName, returnValueSize, f.InstructionOffset, argumentsSize, e.Flags);
        }

        return new BBLangGeneratorResult()
        {
            Code = Code.Compile(),
            CodeEmitter = Code,
            DebugInfo = DebugInfo,
            CompiledFunctions = compilerResult.FunctionDefinitions,
            CompiledOperators = compilerResult.OperatorDefinitions,
            CompiledGeneralFunctions = compilerResult.GeneralFunctionDefinitions,
            CompiledConstructors = compilerResult.ConstructorDefinitions,
            ExposedFunctions = exposedFunctions.ToFrozenDictionary(),
            GeneratedUnmanagedFunctions = GeneratedUnmanagedFunctions.ToImmutableArray(v => v.Function),
            GeneratedUnmanagedFunctionReferences = GeneratedUnmanagedFunctions.ToImmutableArray(v => v.Reference),
            ILGeneratorBuilders = ILGenerator?.Builders?.ToImmutableArray() ?? ImmutableArray<string>.Empty,
        };
    }
}
