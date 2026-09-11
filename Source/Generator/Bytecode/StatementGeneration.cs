using System.Reflection.Emit;
using LanguageCore.Compiler;
using LanguageCore.IR;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    GeneratorStatistics _statistics;
    readonly List<(TemplateInstance<IHaveInstructionOffset> Definition, InstructionLabel Label)> DefinitionLabels = new();

    InstructionLabel LabelForDefinition(TemplateInstance<IHaveInstructionOffset> definition)
    {
        if (DefinitionLabels.TryGetValue(v => Utils.ReferenceEquals(v.Template, definition.Template) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, definition.TypeArguments), out InstructionLabel? label))
        {
            return label;
        }
        label = Code.DefineLabel();
        DefinitionLabels.Add((definition, label));
        return label;
    }

    InstructionLabel LabelForDefinition<T>(TemplateInstance<T> definition) where T : IHaveInstructionOffset
        => LabelForDefinition(TemplateInstance.New<IHaveInstructionOffset>(definition.Template, definition.TypeArguments));

    void GenerateDeallocator(CompiledCleanup cleanup)
    {
        if (cleanup.Deallocator is null)
        {
            return;
        }
        CompiledFunction? f = Functions.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, cleanup.Deallocator.Template) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, cleanup.Deallocator.TypeArguments));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function \"{cleanup.Deallocator.Template.ToReadable()}\" wasn't compiled", cleanup));
            return;
        }

        if (cleanup.Deallocator.Template.ExternalFunctionName is not null)
        {
            throw new NotImplementedException();
        }

        AddComment($"Call \"{cleanup.Deallocator.Template.ToReadable()}\" {{");

        if (cleanup.Deallocator.Template.ReturnSomething)
        { throw new NotImplementedException(); }

        AddComment(" .:");

        InstructionLabel label = LabelForDefinition(cleanup.Deallocator);
        Call(label, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables), false);

        if (!label.IsMarked)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(cleanup.Location, cleanup.Deallocator.UnsafeTo<IHaveInstructionOffset>())); }

        if (cleanup.Deallocator.Template.ReturnSomething)
        {
            AddComment($" Clear return value:");

            // todo: wtf?
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

        CompiledFunction? f = Functions.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, cleanup.Destructor.Template) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, cleanup.Destructor.TypeArguments));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function \"{cleanup.Destructor.Template.ToReadable()}\" wasn't compiled", cleanup));
            return;
        }

        AddComment(" Param0 should be already there");

        AddComment(" .:");

        InstructionLabel label = LabelForDefinition(cleanup.Destructor);
        Call(label, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables), false);

        if (!label.IsMarked)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(cleanup.Location, cleanup.Destructor.UnsafeTo<IHaveInstructionOffset>())); }

        if (StatementCompiler.AllowDeallocate(cleanup.TrashType))
        {
            GenerateDeallocator(cleanup);
        }

        AddComment("}");
    }

    #region Generate Size

    bool GenerateSize(GeneralType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        PointerType v => GenerateSize(v, result, out error),
        ReferenceType v => GenerateSize(v, result, out error),
        ArrayType v => GenerateSize(v, result, out error),
        FunctionType v => GenerateSize(v, result, out error),
        StructType v => GenerateSize(v, result, out error),
        GenericType v => GenerateSize(v, result, out error),
        BuiltinType v => GenerateSize(v, result, out error),
        AliasType v => GenerateSize(v, result, out error),
        EnumType v => GenerateSize(v, result, out error),
        _ => throw new NotImplementedException(),
    };
    bool GenerateSize(ReferenceType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(Settings.PointerSize, result.BitWidth()));
        return true;
    }
    bool GenerateSize(PointerType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(Settings.PointerSize, result.BitWidth()));
        return true;
    }
    bool GenerateSize(ArrayType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (FindSize(type, out int size, out _))
        {
            Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(size, result.BitWidth()));
            return true;
        }

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error))
        {
            return false;
        }

        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(type.Length.Value * elementSize, result.BitWidth()));
        return true;
    }
    bool GenerateSize(FunctionType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(Settings.PointerSize, result.BitWidth()));
        return true;
    }
    bool GenerateSize(StructType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(size, result.BitWidth()));
        return true;
    }
    bool GenerateSize(GenericType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new InvalidOperationException($"Generic type doesn't have a size");
    bool GenerateSize(BuiltinType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(size, result.BitWidth()));
        return true;
    }
    bool GenerateSize(AliasType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Value, result, out error);
    bool GenerateSize(EnumType type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Definition.Type, result, out error);

    bool GenerateSize(CompiledTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        CompiledPointerTypeExpression v => GenerateSize(v, result, out error),
        CompiledReferenceTypeExpression v => GenerateSize(v, result, out error),
        CompiledArrayTypeExpression v => GenerateSize(v, result, out error),
        CompiledFunctionTypeExpression v => GenerateSize(v, result, out error),
        CompiledStructTypeExpression v => GenerateSize(v, result, out error),
        CompiledGenericTypeExpression v => GenerateSize(v, result, out error),
        CompiledBuiltinTypeExpression v => GenerateSize(v, result, out error),
        CompiledAliasTypeExpression v => GenerateSize(v, result, out error),
        CompiledEnumTypeExpression v => GenerateSize(v, result, out error),
        _ => throw new NotImplementedException(),
    };
    bool GenerateSize(CompiledPointerTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(Settings.PointerSize, result.BitWidth()));
        return true;
    }
    bool GenerateSize(CompiledReferenceTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(Settings.PointerSize, result.BitWidth()));
        return true;
    }
    bool GenerateSize(CompiledArrayTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (FindSize(type, out int size, out _))
        {
            Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(size, result.BitWidth()));
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

        if (FindBitWidth(lengthType, type.Length) != BitWidth._32)
        {
            error = new PossibleDiagnostic($"Array length must be a 32 bit integer and not \"{lengthType}\"", type.Length);
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error))
        {
            return false;
        }

        GenerateCodeForStatement(type.Length);
        using (RegisterUsage.Auto lengthRegister = Registers.GetFree(FindBitWidth(lengthType, type.Length)))
        {
            PopTo(lengthRegister.Register);
            Code.Emit(Opcode.MathMultU, lengthRegister.Register, InstructionOperand.Immediate(elementSize, lengthRegister.Register.BitWidth()));
            Code.Emit(Opcode.MathAdd, result, lengthRegister.Register);
        }
        return true;
    }
    bool GenerateSize(CompiledFunctionTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(Settings.PointerSize, result.BitWidth()));
        return true;
    }
    bool GenerateSize(CompiledStructTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(size, result.BitWidth()));
        return true;
    }
    bool GenerateSize(CompiledGenericTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new InvalidOperationException($"Generic type doesn't have a size");
    bool GenerateSize(CompiledBuiltinTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(new BuiltinType(type.Type), out int size, out error))
        { return false; }
        Code.Emit(Opcode.MathAdd, result, InstructionOperand.Immediate(size, result.BitWidth()));
        return true;
    }
    bool GenerateSize(CompiledAliasTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Value, result, out error);
    bool GenerateSize(CompiledEnumTypeExpression type, Register result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Definition.Type, result, out error);

    #endregion

    #region GenerateCodeForStatement

    void GenerateCodeForStatement(CompiledVariableDefinition newVariable)
    {
        // if (newVariable.Getters.Count == 0 &&
        //     newVariable.Setters.Count == 0)
        // {
        //     if (newVariable.InitialValue is not null)
        //     {
        //         GenerateCodeForStatement(newVariable.InitialValue);
        //         Pop(FindSize(newVariable.InitialValue.Type, newVariable.InitialValue));
        //     }
        //     return;
        // }

        if (newVariable.InitialValue is null) return;

        AddComment($"New Variable \"{newVariable.Identifier}\" {{");

        if (newVariable.InitialValue is CompiledList literalList)
        {
            for (int i = 0; i < literalList.Values.Length; i++)
            {
                CompiledExpression value = literalList.Values[i];
                GenerateCodeForValueSetter(new CompiledSetter()
                {
                    Target = new CompiledElementAccess()
                    {
                        Base = new CompiledVariableAccess()
                        {
                            Variable = newVariable,
                            Location = newVariable.Location,
                            SaveValue = true,
                            Type = newVariable.Type,
                        },
                        Index = new CompiledConstantValue()
                        {
                            Value = i,
                            Location = value.Location,
                            SaveValue = true,
                            Type = BuiltinType.I32,
                        },
                        Location = value.Location,
                        SaveValue = true,
                        Type = value.Type,
                    },
                    IsCompoundAssignment = false,
                    Location = value.Location,
                    Value = value,
                });
            }
            AddComment("}");
            return;
        }

        GenerateCodeForValueSetter(new CompiledVariableAccess()
        {
            Variable = newVariable,
            Type = newVariable.Type,
            Location = newVariable.Location,
            SaveValue = true,
        }, newVariable.InitialValue);
        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledLabelDeclaration instructionLabel)
    {
        foreach (ControlFlowFrame v in ReturnInstructions) v.IsSkipping = false;
        foreach (ControlFlowFrame v in BreakInstructions) v.IsSkipping = false;

        if (!GeneratedInstructionLabels.TryGetValue(instructionLabel, out GeneratedInstructionLabel? generatedInstructionLabel))
        {
            generatedInstructionLabel = GeneratedInstructionLabels[instructionLabel] = new();
        }
        Code.MarkLabel(LabelForDefinition(TemplateInstance.New(generatedInstructionLabel, null)));
    }
    void GenerateCodeForStatement(CompiledReturn keywordCall)
    {
        AddComment($"Return {{");

        if (!CanReturn)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Can't return for some reason", keywordCall.Location));
            return;
        }

        if (keywordCall.Value is not null)
        {
            AddComment(" Param 0:");

            GenerateCodeForStatement(keywordCall.Value);

            if (InFunction)
            {
                AddComment(" Set return value:");
                PopTo(ReturnValueAddress, FindSize(keywordCall.Value.Type, keywordCall.Value));
            }
            else
            {
                AddComment(" Set exit code:");
                PopTo(ExitCodeAddress, FindSize(keywordCall.Value.Type, keywordCall.Value));
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
    void GenerateCodeForStatement(CompiledCrash crashStatement)
    {
        if (crashStatement.Value is CompiledString literalThrowValue && Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.CrashStringOnStack))
        {
            _statistics.Optimizations++;
            Diagnostics.Add(DiagnosticAt.OptimizationNotice("String allocated on stack", crashStatement.Value));

            GenerateCodeForStatement(literalThrowValue);
            Code.Emit(Opcode.Crash, Register.StackPointer);
        }
        else if (crashStatement.Value is CompiledStackString stackString)
        {
            GenerateCodeForStatement(stackString);
            Code.Emit(Opcode.Crash, Register.StackPointer);
        }
        else
        {
            GenerateCodeForStatement(crashStatement.Value);
            using (RegisterUsage.Auto reg = Registers.GetFree(FindBitWidth(crashStatement.Value.Type, crashStatement.Value)))
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
            Diagnostics.Add(DiagnosticAt.Error($"The keyword \"{StatementKeywords.Break}\" does not available in the current context", keywordCall));
            return;
        }

        ReturnInstructions.Last.IsSkipping = true;
        Code.Emit(Opcode.Jump, BreakInstructions.Last.Label.Relative());
    }
    void GenerateCodeForStatement(CompiledDelete compiledDelete)
    {
        GenerateCodeForStatement(compiledDelete.Value);
        GenerateDestructor(compiledDelete.Cleanup);
        Pop(FindSize(compiledDelete.Value.Type, compiledDelete.Value));
    }
    void GenerateCodeForStatement(CompiledGoto keywordCall)
    {
        GenerateCodeForStatement(keywordCall.Value);

        using (RegisterUsage.Auto reg = Registers.GetFree(Settings.PointerBitWidth))
        {
            PopTo(reg.Register);
            InstructionLabel offsetLabel = Code.DefineLabel();
            Code.Emit(Opcode.MathSub, reg.Register, offsetLabel.Absolute());
            Code.MarkLabel(offsetLabel);
            Code.Emit(Opcode.Jump, reg.Register);
        }
    }
    Stack<CompiledCleanup> GenerateCodeForArguments(IReadOnlyList<CompiledArgument> arguments, ICompiledFunctionDefinition compiledFunction, ImmutableDictionary<string, GeneralType>? typeArguments, int alreadyPassed = 0)
    {
        Stack<CompiledCleanup> argumentCleanup = new();

        for (int i = 0; i < arguments.Count; i++)
        {
            CompiledArgument argument = arguments[i];
            GeneralType argumentType = argument.Value.Type;
            CompiledParameter parameter = compiledFunction.Parameters[i + alreadyPassed];
            GeneralType parameterType = GeneralType.TryInsertTypeParameters(parameter.Type, typeArguments);

            if (FindSize(argumentType, argument) != FindSize(parameterType, parameter.Definition))
            { Diagnostics.Add(DiagnosticAt.Internal($"Bad argument type passed: expected \"{parameterType}\" passed \"{argumentType}\"", argument.Value)); }

            AddComment($" Pass {parameter}:");

            GenerateCodeForStatement(argument.Value);

            argumentCleanup.Push(argument.Cleanup);
        }

        return argumentCleanup;
    }
    void GenerateCodeForParameterPassing(IReadOnlyList<CompiledArgument> parameters, FunctionType function, Stack<CompiledCleanup> parameterCleanup)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            AddComment($" Param {i}:");
            GenerateCodeForStatement(parameters[i].Value);
            parameterCleanup.Push(parameters[i].Cleanup);
        }
    }
    void GenerateCodeForParameterCleanup(Stack<CompiledCleanup> parameterCleanup)
    {
        if (parameterCleanup.Count == 0) return;

        AddComment(" Clear Params:");
        while (parameterCleanup.Count > 0)
        {
            CompiledCleanup passedParameter = parameterCleanup.Pop();
            GenerateDestructor(passedParameter);
            Pop(FindSize(passedParameter.TrashType, passedParameter));
        }
    }
    void GenerateCodeForFunctionCall_MSIL(CompiledExternalFunctionCall caller, bool isTailCall)
    {
        CompiledFunction? f = Functions.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, caller.Declaration) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, null));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function \"{caller.Declaration.ToReadable()}\" wasn't compiled", caller));
            return;
        }

        AddComment($"Call \"{caller.Declaration.ToReadable()}\" {{");

        if (caller.Function.ReturnValueSize > 0 && caller.SaveValue)
        {
            AddComment($"Initial return value {{");
            StackAlloc(caller.Function.ReturnValueSize, false);
            AddComment($"}}");
        }

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Declaration, null);

        AddComment(" .:");

        Code.Emit(Opcode.CallMSIL, InstructionOperand.Immediate(caller.Function.Id));
        InstructionLabel skipLabel = Code.DefineLabel();
        using (RegisterUsage.Auto reg = Registers.GetFree(BitWidth._8))
        {
            PopTo(reg.Register);
            Code.Emit(Opcode.Compare, reg.Register, InstructionOperand.Immediate(0, reg.Register.BitWidth()));
            Code.Emit(Opcode.JumpIfNotEqual, skipLabel.Relative());
        }

        InstructionLabel label = LabelForDefinition(TemplateInstance.New(caller.Declaration, null));
        Call(label, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables), isTailCall);

        if (!label.IsMarked)
        { UndefinedFunctionOffsets.Add(new(caller, TemplateInstance.New<IHaveInstructionOffset>(caller.Declaration, null))); }
        Code.Emit(Opcode.HotFuncEnd, InstructionOperand.Immediate(caller.Function.Id));

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

        Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Declaration, null);

        AddComment(" .:");
        Code.Emit(Opcode.CallExternal, InstructionOperand.Immediate(caller.Function.Id));

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
        CompiledFunction? f = Functions.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, caller.Function.Template) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, caller.Function.TypeArguments));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function \"{caller.Function.Template.ToReadable()}\" wasn't compiled", caller));
            return;
        }

        bool isTailCall = false;

        if ((caller.IsAtTail || caller.IsAtTailReturn) && Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.TailCall))
        {
            isTailCall = true;
            DiagnosticAt error = DiagnosticAt.FailedOptimization($"Failed to generate tail-call for {caller}", caller);

            if (isTailCall && HasCapturedGlobalVariables != f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables))
            {
                isTailCall = false;
                Diagnostics.Add(error.WithSuberrors(DiagnosticAt.FailedOptimization($"This is not supported yet", caller)));
            }

            if (isTailCall && caller.Arguments.Length != CompiledParameters.Count)
            {
                isTailCall = false;
                Diagnostics.Add(error.WithSuberrors(DiagnosticAt.FailedOptimization($"Callee and the current function has different number of arguments ({caller.Arguments.Length} != {CompiledParameters.Count}) which is not supported yet", caller)));
            }

            if (isTailCall)
            {
                for (int i = 0; i < caller.Arguments.Length; i++)
                {
                    if (caller.Arguments[i].Value is CompiledParameterAccess parameterAccess && Utils.ReferenceEquals(parameterAccess.Parameter, CompiledParameters[i]))
                    {
                        continue;
                    }

                    isTailCall = false;
                    Diagnostics.Add(error.WithSuberrors(DiagnosticAt.FailedOptimization($"Argument {i + 1} is referencing a parameter which is not supported yet", caller)));
                    break;
                }
            }

            if (caller.IsAtTailReturn)
            {
                if (isTailCall && caller.Function.Template.ReturnSomething)
                {
                    if (CurrentReturnType is null || !CurrentReturnType.SameAs(caller.Type))
                    {
                        isTailCall = false;
                        Diagnostics.Add(error.WithSuberrors(DiagnosticAt.FailedOptimization($"Callee's return type and the current function's return type are not the same which is not supported yet", caller)));
                    }
                }

                if (isTailCall && caller.Function.Template.ReturnSomething && !caller.SaveValue)
                {
                    throw new UnreachableException();
                }
            }
            else
            {
                if (isTailCall && caller.Function.Template.ReturnSomething && caller.SaveValue)
                {
                    throw new UnreachableException();
                }
            }
        }

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
                    int returnValueSize = f.Function.ReturnSomething ? FindSize(f.Function.Type, f.Function) : 0;
                    int parametersSize = f.Function.Parameters.Aggregate(0, (a, b) => a + FindSize(b.Type, b.Definition));
                    int id = ExternalFunctions.Concat(GeneratedUnmanagedFunctions.Select(v => v.Function as IExternalFunction).AsEnumerable()).GenerateId();

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
                }, isTailCall);

                Diagnostics.Add(DiagnosticAt.OptimizationNotice($"Function {f.Function.ToReadable()} compiled into MSIL", caller));

                Diagnostics.AddRange(ILGenerator.Diagnostics);

                ILGenerator.Diagnostics.Clear();
                return;
            anyway:;
            }
            //if (!ILGenerator.Diagnostics.Has(DiagnosticsLevel.Error))
            //{
            //    ILGenerator.GenerateImplMarshaled(f, out _);
            //}
            Diagnostics.Add(DiagnosticAt.FailedOptimization($"Failed to generate MSIL for function {f.Function}", caller).WithSuberrors(ILGenerator.Diagnostics.Diagnostics.Where(v => v.Level == DiagnosticsLevel.Error)));
            ILGenerator.Diagnostics.Clear();
        }

        if (isTailCall)
        {
            AddComment($"Tailcall {caller.Function.Template.ToReadable()} {{");

            AddComment("Cleanup function scopes {");
            for (int i = CleanupStack2.Count - 1; i >= 0; i--)
            {
                CleanupVariables(CleanupStack2[i].Variables, caller.Location.Before(), true);
                if (CleanupStack2[i].IsFunction) break;
            }
            AddComment("}");

            PopTo(Register.BasePointer);

            AddComment(" .:");

            InstructionLabel label = LabelForDefinition(caller.Function);
            Call(label, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables), true);

            if (!label.IsMarked)
            { UndefinedFunctionOffsets.Add(new(caller, caller.Function.UnsafeTo<IHaveInstructionOffset>())); }

            AddComment("}");

            Diagnostics.Add(DiagnosticAt.OptimizationNotice($"Tail-call generated", caller));
        }
        else
        {
            AddComment($"Call {caller.Function.Template.ToReadable()} {{");

            if (caller.Function.Template.ReturnSomething)
            {
                AddComment($"Initial return value {{");
                StackAlloc(FindSize(GeneralType.TryInsertTypeParameters(caller.Function.Template.Type, caller.Function.TypeArguments), caller), false);
                AddComment($"}}");
            }

            Stack<CompiledCleanup> parameterCleanup = GenerateCodeForArguments(caller.Arguments, caller.Function.Template, caller.Function.TypeArguments);

            AddComment(" .:");

            InstructionLabel label = LabelForDefinition(caller.Function);
            Call(label, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables), false);

            if (!label.IsMarked)
            { UndefinedFunctionOffsets.Add(new(caller, caller.Function.UnsafeTo<IHaveInstructionOffset>())); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (caller.Function.Template.ReturnSomething && !caller.SaveValue)
            {
                AddComment(" Clear Return Value:");
                Pop(FindSize(GeneralType.TryInsertTypeParameters(caller.Function.Template.Type, caller.Function.TypeArguments), caller));
            }

            AddComment("}");
        }
    }
    void GenerateCodeForStatement(CompiledSizeof anyCall)
    {
        if (FindSize(anyCall.Of, out int size, out _))
        {
            Push(size);
        }
        else
        {
            using RegisterUsage.Auto reg = Registers.GetFree(FindBitWidth(BuiltinType.I32, anyCall.Of));
            Code.Emit(Opcode.Move, reg.Register, InstructionOperand.Immediate(0, reg.Register.BitWidth()));
            if (!GenerateSize(anyCall.Of, reg.Register, out PossibleDiagnostic? generateSizeError))
            { Diagnostics.Add(generateSizeError.ToError(anyCall)); }
            Push(reg.Register);
        }
    }
    void GenerateCodeForStatement(CompiledRuntimeCall anyCall)
    {
        GeneralType prevType = anyCall.Function.Type;
        if (!prevType.Is(out FunctionType? functionType))
        {
            Diagnostics.Add(DiagnosticAt.Error($"This isn't a function", anyCall.Function));
            return;
        }

        if (anyCall.Arguments.Length != functionType.Parameters.Length)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Wrong number of arguments passed to function \"{functionType}\": required {functionType.Parameters.Length} passed {anyCall.Arguments.Length}", anyCall));
            return;
        }

        PossibleDiagnostic? argumentError = null;
        if (!Utils.SequenceEquals(anyCall.Arguments, functionType.Parameters, (argument, parameter) =>
        {
            if (argument.Type.SameAs(parameter))
            { return true; }

            if (StatementCompiler.CanCastImplicitly(argument.Type, parameter, out argumentError, out _))
            { return true; }

            argumentError = argumentError.TrySetLocation(argument);

            return false;
        }))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Argument types of caller \"{anyCall}\" doesn't match with callee \"{functionType}\"", anyCall).WithSuberrors(argumentError?.ToError(anyCall)));
            return;
        }

        AddComment($"Call (runtime) \"{functionType}\" {{");

        if (functionType.ReturnSomething)
        {
            AddComment($"Initial return value {{");
            StackAlloc(FindSize(functionType.ReturnType, anyCall), false);
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
            Pop(FindSize(functionType.ReturnType, anyCall));
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledBinaryOperatorCall @operator)
    {
        CompiledExpression Left = @operator.Left;
        CompiledExpression Right = @operator.Right;

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
            if (Left is CompiledConstantValue && Right is not CompiledConstantValue)
            {
                (Left, Right) = (Right, Left);
            }
        }

        BitWidth leftBitWidth = FindBitWidth(Left.Type, Left);
        BitWidth rightBitWidth = FindBitWidth(Right.Type, Right);
        BitWidth bitWidth = StatementCompiler.MaxBitWidth(leftBitWidth, rightBitWidth);

        InstructionLabel endLabel = Code.DefineLabel();

        GenerateCodeForStatement(Left);

        if (@operator.Operator == CompiledBinaryOperatorCall.LogicalAND)
        {
            PushFrom(StackTop, FindSize(Left.Type, Left));

            using (RegisterUsage.Auto regLeft = Registers.GetFree(leftBitWidth))
            {
                PopTo(regLeft.Register);
                Code.Emit(Opcode.Compare, regLeft.Register, InstructionOperand.Immediate(0, regLeft.Register.BitWidth()));
                Code.Emit(Opcode.JumpIfEqual, endLabel.Relative());
            }
        }
        else if (@operator.Operator == CompiledBinaryOperatorCall.LogicalOR)
        {
            PushFrom(StackTop, FindSize(Left.Type, Left));

            using (RegisterUsage.Auto regLeft = Registers.GetFree(leftBitWidth))
            {
                PopTo(regLeft.Register);
                Code.Emit(Opcode.Compare, regLeft.Register, InstructionOperand.Immediate(0, leftBitWidth));
                Code.Emit(Opcode.JumpIfNotEqual, endLabel.Relative());
            }
        }

        InstructionOperand rightOperand;
        RegisterUsage.Auto? regRight;

        if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.BinaryOperatorFetchSkip)
            && Right is CompiledConstantValue rightValue)
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
        GeneralType leftType = @operator.Expression.Type;
        BitWidth bitWidth = FindBitWidth(leftType, @operator.Expression);

        switch (@operator.Operator)
        {
            case CompiledUnaryOperatorCall.LogicalNOT:
            {
                GenerateCodeForStatement(@operator.Expression);

                using (RegisterUsage.Auto reg = Registers.GetFree(bitWidth))
                {
                    InstructionLabel labelSkipFalse = Code.DefineLabel();
                    InstructionLabel labelSkipTrue = Code.DefineLabel();
                    PopTo(reg.Register);
                    Code.Emit(Opcode.Compare, reg.Register, InstructionOperand.Immediate(0, reg.Register.BitWidth()));
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
                GenerateCodeForStatement(@operator.Expression);

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
                GenerateCodeForStatement(@operator.Expression);

                bool isFloat = @operator.Expression.Type.SameAs(BasicType.F32);

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
                GenerateCodeForStatement(@operator.Expression);
                return;
            }
            default:
            {
                Diagnostics.Add(DiagnosticAt.Error($"Unknown operator \"{@operator.Operator}\"", @operator));
                return;
            }
        }
    }
    void GenerateCodeForStatement(CompiledConstantValue literal)
    {
        Push(literal.Value);
    }
    void GenerateCodeForStatement(CompiledString stringInstance)
    {
        //Code.Emit(Opcode.Push, (InstructionOperand)GenerateString(stringInstance.Value, stringInstance.IsASCII));
        //return;

        AddComment($"Create String \"{stringInstance.Value}\" {{");

        AddComment("Allocate String object {");

        GenerateCodeForStatement(stringInstance.Allocator);

        AddComment("}");

        AddComment("Set string data {");

        using (RegisterUsage.Auto reg = Registers.GetFree(Settings.PointerBitWidth))
        {
            // Save pointer
            Code.Emit(Opcode.Move, reg.Register, (InstructionOperand)StackTop);

            if (stringInstance.IsUTF8)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(stringInstance.Value);

                for (int i = 0; i < bytes.Length; i++)
                {
                    Code.Emit(Opcode.Move, reg.Register.ToPtr(i, BitWidth._8), InstructionOperand.Immediate(bytes[i], BitWidth._8));
                }

                Code.Emit(Opcode.Move, reg.Register.ToPtr(bytes.Length, BitWidth._8), InstructionOperand.Immediate(0, BitWidth._8));
            }
            else
            {
                BuiltinType charType = BuiltinType.Char;
                int charSize = FindSize(charType, stringInstance);
                BitWidth charBw = (BitWidth)charSize;

                for (int i = 0; i < stringInstance.Value.Length; i++)
                {
                    Code.Emit(Opcode.Move, reg.Register.ToPtr(i * charSize, charBw), InstructionOperand.Immediate(stringInstance.Value[i], BitWidth._16));
                }

                Code.Emit(Opcode.Move, reg.Register.ToPtr(stringInstance.Value.Length * charSize, charBw), InstructionOperand.Immediate('\0', BitWidth._16));
            }
        }

        AddComment("}");

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledStackString stackString)
    {
        if (stackString.IsUTF8)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(stackString.Value);
            if (stackString.IsNullTerminated) Push(new CompiledValue(default(byte)));
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                Push(new CompiledValue(bytes[i]));
            }
        }
        else
        {
            if (stackString.IsNullTerminated) Push(new CompiledValue(default(char)));
            for (int i = stackString.Value.Length - 1; i >= 0; i--)
            {
                Push(new CompiledValue(stackString.Value[i]));
            }
        }
    }
    void GenerateCodeForStatement(CompiledLambda compiledLambda)
    {
        InstructionLabel label = LabelForDefinition(TemplateInstance.New(compiledLambda, null));

        if (!label.IsMarked)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(compiledLambda, TemplateInstance.New<IHaveInstructionOffset>(compiledLambda, null))); }

        if (compiledLambda.CapturedLocals.IsDefaultOrEmpty)
        {
            Push(label.Absolute());
        }
        else
        {
            if (compiledLambda.Allocator is null) throw new UnreachableException();
            GenerateCodeForStatement(compiledLambda.Allocator);

            using (RegisterUsage.Auto reg = Registers.GetFree(Settings.PointerBitWidth))
            {
                Code.Emit(Opcode.Move, reg.Register, (InstructionOperand)StackTop);

                AddComment("Save function pointer:");
                Code.Emit(Opcode.Move, reg.Register.ToPtr(0, Settings.PointerBitWidth), label.Absolute());

                int offset = Settings.PointerSize;
                foreach (CapturedLocal capturedLocal in compiledLambda.CapturedLocals)
                {
                    if (capturedLocal.Variable is not null)
                    {
                        int size = FindSize(capturedLocal.Variable.Type, capturedLocal.Variable.TypeExpression);
                        AddComment($"Capture variable `{capturedLocal.Variable.Identifier}`:");
                        GenerateCodeForStatement(new CompiledVariableAccess()
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
                        int size = FindSize(capturedLocal.Parameter.Type, capturedLocal.Parameter.Definition);
                        AddComment($"Capture variable `{capturedLocal.Parameter.Identifier}`:");
                        GenerateCodeForStatement(new CompiledParameterAccess()
                        {
                            Parameter = capturedLocal.Parameter,
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
    void GenerateCodeForStatement(CompiledEnumMemberAccess enumMemberAccess)
    {
        GenerateCodeForStatement(enumMemberAccess.EnumMember.Value);
    }
    void GenerateCodeForStatement(CompiledRegisterAccess register)
    {
        Code.Emit(Opcode.Push, register.Register);
    }
    void GenerateCodeForStatement(CompiledParameterAccess parameterRef)
    {
        if (!GetParameterAddress(parameterRef.Parameter, 0, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(parameterRef));
            return;
        }

        PushFrom(address, FindSize(parameterRef.Type, parameterRef.Parameter.Definition));
    }
    void GenerateCodeForStatement(CompiledVariableAccess variableRef)
    {
        if (!GetVariableAddress(variableRef.Variable, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(variableRef));
            return;
        }

        PushFrom(address, FindSize(variableRef.Type, variableRef));
    }
    void GenerateCodeForStatement(CompiledExpressionVariableAccess expressionVariableRef)
    {
        PushFrom(new AddressAbsolute(expressionVariableRef.Variable.Address), FindSize(expressionVariableRef.Type, expressionVariableRef));
    }
    void GenerateCodeForStatement(CompiledFunctionReference functionRef)
    {
        InstructionLabel label = LabelForDefinition(functionRef.Function);

        if (!label.IsMarked)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(functionRef, functionRef.Function.UnsafeTo<IHaveInstructionOffset>())); }

        Push(label.Absolute());
    }
    void GenerateCodeForStatement(CompiledLabelReference labelRef)
    {
        InstructionLabel label;

        if (!GeneratedInstructionLabels.TryGetValue(labelRef.InstructionLabel, out GeneratedInstructionLabel? instructionLabel))
        {
            instructionLabel = GeneratedInstructionLabels[labelRef.InstructionLabel] = new();
            label = LabelForDefinition(TemplateInstance.New(instructionLabel, null));
        }
        else
        {
            label = LabelForDefinition(TemplateInstance.New(instructionLabel, null));
        }

        Push(label.Absolute());
    }
    void GenerateCodeForStatement(CompiledGetReference addressGetter)
    {
        if (!GetAddress(addressGetter.Of, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(addressGetter.Of));
            return;
        }
        GenerateAddressResolver(address);
    }
    void GenerateCodeForStatement(CompiledDereference pointer)
    {
        GenerateCodeForStatement(pointer.Address);

        GeneralType addressType = pointer.Address.Type;
        if (addressType.Is(out IReferenceType? referenceType))
        {
            using (RegisterUsage.Auto reg = Registers.GetFree(FindBitWidth(addressType, pointer)))
            {
                PopTo(reg.Register);
                PushFrom(new AddressRegisterPointer(reg.Register), FindSize(referenceType.To, pointer.Address));
            }
        }
        else
        {
            Diagnostics.Add(DiagnosticAt.Error($"This isn't a pointer", pointer.Address));
            return;
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
        if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.ConditionTrimming)
            && whileLoop.Condition is CompiledConstantValue constantCondition
            && constantCondition.Value == true)
        {
            _statistics.Optimizations++;
        }
        else
        {
            GenerateCodeForCondition(whileLoop.Condition, endLabel);
        }

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

        CompiledScope scope = OnScopeEnter(forLoop.Location.Position, forLoop.Location.File, forLoop.Initialization is CompiledVariableDefinition wah ? Enumerable.Repeat(wah, 1) : Enumerable.Empty<CompiledVariableDefinition>(), false);

        if (forLoop.Initialization is not null)
        {
            AddComment("For-loop variable");
            GenerateCodeForStatement(forLoop.Initialization);
        }

        InstructionLabel conditionOffsetFor = Code.MarkLabel();

        InstructionLabel endLabel = Code.DefineLabel();

        if (forLoop.Condition is not null)
        {
            AddComment("For-loop condition");
            GenerateCodeForCondition(forLoop.Condition, endLabel);
        }

        BreakInstructions.Push(new ControlFlowFrame(endLabel));
        GenerateCodeForStatement(forLoop.Body);

        if (forLoop.Step is not null)
        {
            AddComment("For-loop expression");
            GenerateCodeForStatement(forLoop.Step);
        }

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

        StackAlloc(FindSize(newInstance.Type, newInstance), true);

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledConstructorCall constructorCall)
    {
        CompiledFunction? f = Functions.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, constructorCall.Function.Template) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, constructorCall.Function.TypeArguments));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function \"{constructorCall.Function.Template.ToReadable()}\" wasn't compiled", constructorCall));
            return;
        }

        AddComment($"Call \"{constructorCall.Function.Template.ToReadable()}\" {{");

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

            parameterCleanup.AddRange(GenerateCodeForArguments(constructorCall.Arguments, constructorCall.Function.Template, constructorCall.Function.TypeArguments, 1));
        }
        else if (constructorCall.Object.Type.Is<PointerType>())
        {
            parameterCleanup.AddRange(GenerateCodeForArguments(constructorCall.Arguments, constructorCall.Function.Template, constructorCall.Function.TypeArguments, 1));
        }
        else
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Invalid type \"{constructorCall.Object.Type}\" used for constructor", constructorCall.Object));
            return;
        }

        AddComment(" .:");

        InstructionLabel label = LabelForDefinition(constructorCall.Function);
        Call(label, f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables), false);

        if (!label.IsMarked)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset(constructorCall, constructorCall.Function.UnsafeTo<IHaveInstructionOffset>())); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }
    void GenerateCodeForStatement(CompiledFieldAccess field)
    {
        GeneralType prevType = field.Object.Type;

        if (prevType.Is(out IReferenceType? referenceType))
        {
            GenerateCodeForStatement(field.Object);
            CheckPointerNull();

            prevType = referenceType.To;

            while (prevType.Is(out referenceType))
            {
                prevType = referenceType.To;

                using (RegisterUsage.Auto reg = Registers.GetFree(Settings.PointerBitWidth))
                {
                    PopTo(reg.Register);
                    PushFrom(new AddressRegisterPointer(
                        reg.Register),
                        Settings.PointerSize
                    );
                }
                CheckPointerNull();
            }

            if (!prevType.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Could not get the field offsets of type \"{prevType}\"", field.Object));
                return;
            }

            if (!GetFieldOffset(structPointerType, field.Field.Identifier, out CompiledField? fieldDefinition, out int fieldOffset, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(field));
                return;
            }

            using (RegisterUsage.Auto reg = Registers.GetFree(Settings.PointerBitWidth))
            {
                PopTo(reg.Register);
                PushFrom(new AddressOffset(
                    new AddressRegisterPointer(reg.Register),
                    fieldOffset
                    ), FindSize(GeneralType.TryInsertTypeParameters(fieldDefinition.Type, structPointerType.TypeArguments), fieldDefinition.Definition));
            }
            return;
        }

        if (!prevType.Is(out StructType? structType)) throw new NotImplementedException();

        GeneralType type = field.Type;

        if (!GetFieldOffset(structType, field.Field.Identifier, out _, out _, out PossibleDiagnostic? error2))
        {
            Diagnostics.Add(error2.ToError(field));
            return;
        }

        // todo: what the hell is that

        CompiledExpression? dereference = FindFirstReference(field);

        if (!GetAddress(field, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(field));
            return;
        }

        if (dereference is null)
        { PushFrom(address, FindSize(type, field)); }
        else
        { PushFromChecked(address, FindSize(type, field)); }
    }
    void GenerateCodeForStatement(CompiledElementAccess index)
    {
        GeneralType prevType = index.Base.Type;
        GeneralType indexType = index.Index.Type;

        if (prevType.Is(out ArrayType? arrayType))
        {
            if (!GetAddress(index.Base, out Address? address, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(index.Base));
                return;
            }

            if (!indexType.Is<BuiltinType>())
            {
                Diagnostics.Add(DiagnosticAt.Error($"Index must be a builtin type (i.e. int) and not \"{indexType}\"", index.Index));
                return;
            }

            if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.IndexerFetchSkip)
                && index.Index is CompiledConstantValue evaluatedIndex)
            {
                if (arrayType.Length.HasValue && (evaluatedIndex.Value < 0 || evaluatedIndex.Value >= arrayType.Length.Value))
                {
                    Diagnostics.Add(DiagnosticAt.Warning($"Index out of range", index.Index));
                }
                address = new AddressOffset(address, (int)evaluatedIndex.Value * FindSize(arrayType.Of, index.Base));

                PushFrom(address, FindSize(arrayType.Of, index.Base));
            }
            else
            {
                GenerateAddressResolver(address);

                using (RegisterUsage.Auto regPtr = Registers.GetFree(Settings.PointerBitWidth))
                {
                    PopTo(regPtr.Register);

                    GenerateCodeForStatement(index.Index);
                    using (RegisterUsage.Auto regIndex = Registers.GetFree(FindBitWidth(indexType, index.Index)))
                    {
                        PopTo(regIndex.Register);
                        Code.Emit(Opcode.MathMultU, regIndex.Register, InstructionOperand.Immediate(FindSize(arrayType.Of, index.Base), regIndex.Register.BitWidth()));
                        Code.Emit(Opcode.MathAdd, regPtr.Register, regIndex.Register);
                    }

                    PushFrom(new AddressRegisterPointer(regPtr.Register), FindSize(arrayType.Of, index.Base));
                }
            }
            return;
        }

        if (prevType.Is(out PointerType? pointerType) && pointerType.To.Is(out arrayType))
        {
            int elementSize = FindSize(arrayType.Of, index.Base);
            if (!GetAddress(index, out Address? _address, out PossibleDiagnostic? _error))
            {
                Diagnostics.Add(_error.ToError(index));
                return;
            }
            PushFrom(_address, elementSize);
            return;
        }

        Diagnostics.Add(DiagnosticAt.Error($"Index getter for type \"{prevType}\" not found", index));
    }
    void GenerateAddressResolver(Address address)
    {
        switch (address.Simplify())
        {
            case AddressPointer runtimePointer:
            {
                PushFrom(runtimePointer.PointerAddress, Settings.PointerSize);
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
                using (RegisterUsage.Auto reg = Registers.GetFree(Settings.PointerBitWidth))
                {
                    PopTo(reg.Register);
                    Code.Emit(Opcode.MathAdd, reg.Register, InstructionOperand.Immediate(addressOffset.Offset, reg.Register.BitWidth()));
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
                    Diagnostics.Add(DiagnosticAt.Error($"Index type must be builtin (ie. \"int\") and not \"{indexType}\"", runtimeIndex.IndexValue));
                    return;
                }

                if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.IndexerFetchSkip)
                    && runtimeIndex.IndexValue is CompiledConstantValue evaluatedIndex)
                {
                    int indexValue = (int)evaluatedIndex.Value;
                    using (RegisterUsage.Auto regBase = Registers.GetFree(Settings.PointerBitWidth))
                    {
                        PopTo(regBase.Register);
                        Code.Emit(Opcode.MathAdd, regBase.Register, InstructionOperand.Immediate(indexValue * runtimeIndex.ElementSize, regBase.Register.BitWidth()));
                        Push(regBase.Register);
                    }
                }
                else
                {
                    GenerateCodeForStatement(runtimeIndex.IndexValue);

                    using (RegisterUsage.Auto regIndex = Registers.GetFree(FindBitWidth(indexType, runtimeIndex.IndexValue)))
                    {
                        PopTo(regIndex.Register);
                        Code.Emit(Opcode.MathMultU, regIndex.Register, InstructionOperand.Immediate(runtimeIndex.ElementSize, regIndex.Register.BitWidth()));
                        using (RegisterUsage.Auto regBase = Registers.GetFree(Settings.PointerBitWidth))
                        {
                            PopTo(regBase.Register);
                            Code.Emit(Opcode.MathAdd, regBase.Register, regIndex.Register);
                            Push(regBase.Register);
                        }
                    }
                }

                break;
            }
            case AddressAbsolute absolute:
            {
                Push(absolute.Value);
                break;
            }
            default: throw new NotImplementedException();
        }
    }
    void GenerateCodeForStatement(CompiledReinterpretation typeCast)
    {
        GenerateCodeForStatement(typeCast.Value);
    }
    void GenerateCodeForStatement(CompiledCast typeCast)
    {
        GeneralType statementType = typeCast.Value.Type;
        GeneralType targetType = typeCast.Type;

        if (statementType.SameAs(targetType))
        {
            GenerateCodeForStatement(typeCast.Value);
            return;
        }

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

        // TODO
        /*
        // u32 -> i32
        if (statementType.SameAs(BuiltinType.U32) &&
            targetType.SameAs(BuiltinType.I32))
        {
            GenerateCodeForStatement(typeCast.Value);
            using (RegisterUsage.Auto reg = Registers.GetFree(BitWidth._32))
            {
                Code.Emit(Opcode.Move, reg.Register, (InstructionOperand)StackTop);
                Code.Emit(Opcode.BitsAND, new InstructionOperand(0b_10000000_00000000_00000000_00000000));
                Code.Emit(Opcode.Compare, reg.Register, new InstructionOperand(0));
                InstructionLabel l = Code.DefineLabel();
                Code.Emit(Opcode.JumpIfEqual, new PreparationInstructionOperand(l, false));

                GenerateCodeForStatement(new CompiledCrash()
                {
                    Value = new CompiledStackString()
                    {
                        Value = "mew",
                        IsNullTerminated = true,
                        IsASCII = false,
                        Location = typeCast.Location,
                        SaveValue = true,
                        Type = new ArrayType(BuiltinType.Char, "mew".Length + 1),
                    },
                    Location = typeCast.Location,
                });

                Code.MarkLabel(l);
                PopTo(reg.Register);
                Code.Emit(Opcode.BitsAND, new InstructionOperand(0b_01111111_11111111_11111111_11111111));
            }
            return;
        }
        */

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
                PushFrom(StackTop, Settings.PointerSize);
                using (RegisterUsage.Auto reg = Registers.GetFree(Settings.PointerBitWidth))
                {
                    PopTo(reg.Register);
                    CheckPointerNull(reg.Register);
                    GenerateCodeForStatement(typeCast.Value);
                    PopTo(new AddressRegisterPointer(reg.Register), Settings.PointerSize);
                }
                return;
            }

            if (functionTypeFrom.HasClosure && !functionTypeTo.HasClosure)
            {
                throw new NotImplementedException();
            }

            throw new UnreachableException();
        }

        /*
        if (statementType.Is(out BuiltinType? statementBuiltinType)
            && targetType.Is(out BuiltinType? targetbuiltinType))
        {
            if (!statementBuiltinType.Type.IsInteger())
            {
                Diagnostics.Add(Diagnostic.Error($"Invalid integer type {statementBuiltinType} to resize from", typeCast));
                return;
            }
            if (!targetbuiltinType.Type.IsInteger())
            {
                Diagnostics.Add(Diagnostic.Error($"Invalid integer type {targetbuiltinType} to resize from", typeCast));
                return;
            }

            BitWidth statementBw = statementBuiltinType.GetBitWidth(this, Diagnostics, typeCast.Value);
            BitWidth targetBw = targetbuiltinType.GetBitWidth(this, Diagnostics, typeCast);

            if (statementBw == targetBw)
            {
                GenerateCodeForStatement(typeCast.Value);
                return;
            }

            using (RegisterUsage.Auto reg = Registers.GetFree(statementBw > targetBw ? statementBw : targetBw))
            {
                GenerateCodeForStatement(typeCast.Value);
                Code.Emit(Opcode.Move, reg.Register, InstructionOperand.Immediate(0, reg.Register.BitWidth()));
                PopTo(reg.Register.GetSized(statementBw), statementBw);
                Push(reg.Register.GetSized(targetBw));
            }
            return;
        }
        */

        if (statementType.Is(out BuiltinType? statementBuiltinType)
            && targetType.Is(out BuiltinType? targetbuiltinType))
        {
            int statementSize = FindSize(statementBuiltinType, typeCast.Value);
            int targetSize = FindSize(targetbuiltinType, typeCast);

            if (statementSize != targetSize)
            {
                if (statementSize < targetSize)
                {
                    AddComment($"Grow \"{statementBuiltinType}\" ({statementSize} bytes) to \"{targetbuiltinType}\" ({targetSize}) {{");

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
                    AddComment($"Shrink \"{statementBuiltinType}\" ({statementSize} bytes) to \"{targetbuiltinType}\" ({targetSize}) {{");

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

                Diagnostics.Add(DiagnosticAt.Error($"Can't modify the size of the value. You tried to convert from \"{statementBuiltinType}\" (size of {statementSize}) to \"{targetbuiltinType}\" (size of {targetSize})", typeCast));
                return;
            }
        }

        Diagnostics.Add(DiagnosticAt.Warning($"Ignoring invalid type cast ({statementType} -> {targetType})", typeCast));
        GenerateCodeForStatement(typeCast.Value);
    }
    void GenerateCodeForStatement(CompiledCompilerVariableAccess statement)
    {
        Code.Emit(Opcode.Push, new PreparationInstructionOperand(new VariableInstructionOperand(statement.Identifier)));
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
                !StatementCompiler.Visit(v).Any(v => v is CompiledLabelDeclaration))
            {
                continue;
            }

            GenerateCodeForStatement(v);
        }
        AddComment("}");

        if (!ignoreScope) OnScopeExit(block.Location.Position.After(), block.Location.File, scope);
    }
    void GenerateCodeForStatement(CompiledStatement statement)
    {
        Settings.CancellationToken.ThrowIfCancellationRequested();

        int startInstruction = Code.Offset;

        switch (statement)
        {
            case CompiledExpression v: GenerateCodeForStatement(v); break;
            case CompiledVariableDefinition v: GenerateCodeForStatement(v); break;
            case CompiledReturn v: GenerateCodeForStatement(v); break;
            case CompiledCrash v: GenerateCodeForStatement(v); break;
            case CompiledBreak v: GenerateCodeForStatement(v); break;
            case CompiledDelete v: GenerateCodeForStatement(v); break;
            case CompiledGoto v: GenerateCodeForStatement(v); break;
            case CompiledSetter v: GenerateCodeForValueSetter(v); break;
            case CompiledWhileLoop v: GenerateCodeForStatement(v); break;
            case CompiledForLoop v: GenerateCodeForStatement(v); break;
            case CompiledIf v: GenerateCodeForStatement(v); break;
            case CompiledBlock v: GenerateCodeForStatement(v); break;
            case CompiledLabelDeclaration v: GenerateCodeForStatement(v); break;
            case CompiledEmptyStatement: break;
            default: throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\"");
        }

        if (startInstruction != Code.Offset && statement is not CompiledExpression and not CompiledWhileLoop and not CompiledForLoop and not CompiledBranch and not CompiledBlock and not CompiledDummyExpression and not CompiledMeowExpression)
        {
            DebugInfo?.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (startInstruction, Code.Offset),
                Location = statement.Location,
            });
        }
    }
    void GenerateCodeForStatement(CompiledExpression statement)
    {
        Settings.CancellationToken.ThrowIfCancellationRequested();

        int startInstruction = Code.Offset;

        switch (statement)
        {
            case CompiledSizeof v: GenerateCodeForStatement(v); break;
            case CompiledBinaryOperatorCall v: GenerateCodeForStatement(v); break;
            case CompiledUnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case CompiledConstantValue v: GenerateCodeForStatement(v); break;
            case CompiledRegisterAccess v: GenerateCodeForStatement(v); break;
            case CompiledVariableAccess v: GenerateCodeForStatement(v); break;
            case CompiledExpressionVariableAccess v: GenerateCodeForStatement(v); break;
            case CompiledParameterAccess v: GenerateCodeForStatement(v); break;
            case CompiledFunctionReference v: GenerateCodeForStatement(v); break;
            case CompiledLabelReference v: GenerateCodeForStatement(v); break;
            case CompiledFieldAccess v: GenerateCodeForStatement(v); break;
            case CompiledElementAccess v: GenerateCodeForStatement(v); break;
            case CompiledGetReference v: GenerateCodeForStatement(v); break;
            case CompiledDereference v: GenerateCodeForStatement(v); break;
            case CompiledStackAllocation v: GenerateCodeForStatement(v); break;
            case CompiledConstructorCall v: GenerateCodeForStatement(v); break;
            case CompiledCast v: GenerateCodeForStatement(v); break;
            case CompiledReinterpretation v: GenerateCodeForStatement(v); break;
            case CompiledRuntimeCall v: GenerateCodeForStatement(v); break;
            case CompiledFunctionCall v: GenerateCodeForFunctionCall(v); break;
            case CompiledExternalFunctionCall v: GenerateCodeForFunctionCall_External(v); break;
            case CompiledDummyExpression v: GenerateCodeForStatement(v.Statement); break;
            case CompiledString v: GenerateCodeForStatement(v); break;
            case CompiledStackString v: GenerateCodeForStatement(v); break;
            case CompiledLambda v: GenerateCodeForStatement(v); break;
            case CompiledEnumMemberAccess v: GenerateCodeForStatement(v); break;
            case CompiledCompilerVariableAccess v: GenerateCodeForStatement(v); break;
            default: throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\"");
        }

        if (startInstruction != Code.Offset && statement is not CompiledDummyExpression and not CompiledMeowExpression)
        {
            DebugInfo?.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (startInstruction, Code.Offset),
                Location = statement.Location,
                IsSubtle = statement is CompiledConstantValue,
            });
        }
    }

    void GenerateCodeForCondition(CompiledExpression statement, InstructionLabel falseLabel)
    {
        bool v = false;
        GenerateCodeForCondition(statement, falseLabel, ref v);
    }
    void GenerateCodeForCondition(CompiledExpression statement, InstructionLabel falseLabel, ref bool didJump)
    {
        int startInstruction = Code.Offset;

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
            using (RegisterUsage.Auto reg = Registers.GetFree(FindBitWidth(statement.Type, statement)))
            {
                PopTo(reg.Register);
                Code.Emit(Opcode.Compare, reg.Register, InstructionOperand.Immediate(0, reg.Register.BitWidth()));
                Code.Emit(Opcode.JumpIfEqual, falseLabel.Relative());
                didJump = true;
            }
        }

        if (startInstruction != Code.Offset && statement is not CompiledDummyExpression)
        {
            DebugInfo?.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (startInstruction, Code.Offset),
                Location = statement.Location,
            });
        }
    }
    void GenerateCodeForCondition(CompiledUnaryOperatorCall @operator, InstructionLabel falseLabel, ref bool didJump)
    {
        switch (@operator.Operator)
        {
            case CompiledUnaryOperatorCall.LogicalNOT:
            {
                InstructionLabel subFalseLabel = Code.DefineLabel();
                GenerateCodeForCondition(@operator.Expression, subFalseLabel);

                Code.Emit(Opcode.Jump, falseLabel.Relative());
                didJump = true;

                Code.MarkLabel(subFalseLabel);
                return;
            }
            case CompiledUnaryOperatorCall.BinaryNOT:
            {
                BitWidth bitWidth = FindBitWidth(@operator.Expression.Type, @operator.Expression);

                GenerateCodeForStatement(@operator.Expression);

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
                Diagnostics.Add(DiagnosticAt.Error($"Unknown operator \"{@operator.Operator}\"", @operator));
                return;
            }
        }
    }
    void GenerateCodeForCondition(CompiledBinaryOperatorCall @operator, InstructionLabel falseLabel, ref bool didJump)
    {
        BitWidth leftBitWidth = FindBitWidth(@operator.Left.Type, @operator.Left);
        BitWidth rightBitWidth = FindBitWidth(@operator.Right.Type, @operator.Right);
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
            CompiledExpression Left = @operator.Left;
            CompiledExpression Right = @operator.Right;

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
                if (Left is CompiledConstantValue && Right is not CompiledConstantValue)
                {
                    (Left, Right) = (Right, Left);
                }
            }

            GenerateCodeForStatement(Left);

            InstructionOperand rightOperand;
            RegisterUsage.Auto? regRight;

            if (Settings.Optimizations.HasFlag(GeneratorOptimizationSettings.BinaryOperatorFetchSkip)
                && Right is CompiledConstantValue rightValue)
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

    ImmutableArray<CompiledCleanup> CompileVariables(IEnumerable<CompiledVariableDefinition> statements, bool addComments = true)
    {
        if (addComments) AddComment("Variables {");

        ImmutableArray<CompiledCleanup>.Builder result = ImmutableArray.CreateBuilder<CompiledCleanup>();

        foreach (CompiledVariableDefinition statement in statements)
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
            CleanupVariable(cleanupItems[i], location, justGenerateCode);
        }
    }
    void CleanupVariable(CompiledCleanup cleanupItem, Location location, bool justGenerateCode)
    {
        GenerateDestructor(cleanupItem);
        Pop(FindSize(cleanupItem.TrashType, cleanupItem));

        if (!justGenerateCode) CompiledLocalVariables.Pop();
    }

    void GenerateCodeForValueSetter(CompiledSetter setter)
    {
        switch (setter.Target)
        {
            case CompiledRegisterAccess v: GenerateCodeForValueSetter(v, setter.Value); break;
            case CompiledVariableAccess v: GenerateCodeForValueSetter(v, setter.Value); break;
            case CompiledExpressionVariableAccess v: GenerateCodeForValueSetter(v, setter.Value); break;
            case CompiledParameterAccess v: GenerateCodeForValueSetter(v, setter.Value); break;
            case CompiledFieldAccess v: GenerateCodeForValueSetter(v, setter.Value); break;
            case CompiledElementAccess v: GenerateCodeForValueSetter(v, setter.Value); break;
            case CompiledDereference v: GenerateCodeForValueSetter(v, setter.Value); break;
            default: throw new UnreachableException(setter.Target.GetType().Name);
        }
    }
    void GenerateCodeForValueSetter(CompiledRegisterAccess registerSetter, CompiledExpression value)
    {
        GenerateCodeForStatement(value);
        PopTo(registerSetter.Register);
    }
    void GenerateCodeForValueSetter(CompiledVariableAccess localVariableSetter, CompiledExpression value)
    {
        GenerateCodeForStatement(value);

        if (!GetVariableAddress(localVariableSetter.Variable, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(localVariableSetter));
            return;
        }

        PopTo(address, FindSize(localVariableSetter.Variable.Type, localVariableSetter.Variable));
        // localVariableSetter.Variable.Variable.IsInitialized = true;
    }
    void GenerateCodeForValueSetter(CompiledExpressionVariableAccess localVariableSetter, CompiledExpression value)
    {
        GenerateCodeForStatement(value);
        PopTo(new AddressAbsolute(localVariableSetter.Variable.Address), FindSize(localVariableSetter.Variable.Type, value));
    }
    void GenerateCodeForValueSetter(CompiledParameterAccess parameterSetter, CompiledExpression value)
    {
        GenerateCodeForStatement(value);

        if (!GetParameterAddress(parameterSetter.Parameter, 0, out Address? address, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(parameterSetter));
            return;
        }

        PopTo(address, FindSize(parameterSetter.Parameter.Type, parameterSetter));
    }
    void GenerateCodeForValueSetter(CompiledFieldAccess fieldSetter, CompiledExpression value)
    {
        GeneralType valueType = value.Type;

        CompiledExpression? dereference = FindFirstReference(fieldSetter);

        if (value is CompiledStackString stackString)
        {
            if (dereference is null)
            {
                if (!GetAddress(fieldSetter, out Address? address, out PossibleDiagnostic? error2))
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
                if (!GetAddress(fieldSetter, out Address? address, out PossibleDiagnostic? error))
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
            GenerateCodeForStatement(value);

            if (dereference is null)
            {
                if (!GetAddress(fieldSetter, out Address? address, out PossibleDiagnostic? error2))
                {
                    Diagnostics.Add(error2.ToError(fieldSetter));
                    return;
                }
                PopTo(address, FindSize(valueType, value));
            }
            else
            {
                if (!GetAddress(fieldSetter, out Address? address, out PossibleDiagnostic? error))
                {
                    Diagnostics.Add(error.ToError(fieldSetter));
                    return;
                }
                PopToChecked(address, FindSize(valueType, value));
            }
        }
    }
    void GenerateCodeForValueSetter(CompiledElementAccess indexSetter, CompiledExpression value)
    {
        if (!GetAddress(indexSetter, out Address? _address, out PossibleDiagnostic? _error))
        {
            Diagnostics.Add(_error.ToError(indexSetter));
            return;
        }
        GenerateCodeForStatement(value);
        PopTo(_address, FindSize(value.Type, value));
    }
    void GenerateCodeForValueSetter(CompiledDereference statementToSet, CompiledExpression value)
    {
        if (FindBitWidth(statementToSet.Address.Type, statementToSet.Address) != Settings.PointerBitWidth)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Type \"{statementToSet.Address.Type}\" cant be a pointer", statementToSet.Address));
            return;
        }

        GenerateCodeForStatement(value);

        GenerateCodeForStatement(statementToSet.Address);

        using (RegisterUsage.Auto reg = Registers.GetFree(Settings.PointerBitWidth))
        {
            PopTo(reg.Register);
            PopTo(new AddressRegisterPointer(reg.Register), FindSize(value.Type, value));
        }
    }

    #endregion

    CompiledScope OnScopeEnter(CompiledBlock block, bool isFunction) => OnScopeEnter(block.Location.Position, block.Location.File, block.Statements.OfType<CompiledVariableDefinition>(), isFunction);
    CompiledScope OnScopeEnter(Position position, Uri file, IEnumerable<CompiledVariableDefinition> variables, bool isFunction)
    {
        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (Code.Offset, Code.Offset + 1),
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
        { Diagnostics.Add(DiagnosticAt.Internal($"There was something went wrong in a scope.", position, file)); }

        CleanupVariables(scope.Variables, new Location(position, file), false);

        ScopeInformation scopeDebug = CurrentScopeDebug.Pop();
        scopeDebug.Location.Instructions.End = Code.Offset;
        DebugInfo?.ScopeInformation.Add(scopeDebug);
    }

    #region GenerateCodeForLocalVariable

    CompiledCleanup? GenerateCodeForLocalVariable(CompiledVariableDefinition newVariable)
    {
        // if (newVariable.Getters.Count == 0 &&
        //     newVariable.Setters.Count == 0)
        // { return null; }

        GeneralType type = newVariable.Type;

        int offset = (VariablesSize + FindSize(type, newVariable)) * ProcessorState.StackDirection;
        GeneratedVariables[newVariable] = new GeneratedVariable()
        {
            MemoryAddress = offset,
        };

        StackElementInformation debugInfo = new()
        {
            Kind = StackElementKind.Variable,
            Identifier = newVariable.Identifier,
            Address = offset,
            BasePointerRelative = true,
            Size = FindSize(type, newVariable),
            Type = type
        };

        if (CurrentScopeDebug.Count > 0) CurrentScopeDebug.Last.Stack.Add(debugInfo);

        CompiledLocalVariables.Add(newVariable);

        int size;

        if (newVariable.InitialValue is CompiledConstantValue evaluatedInitialValue)
        {
            AddComment($"Initial value {{");

            size = FindSize(evaluatedInitialValue.Type, newVariable.InitialValue);

            Push(evaluatedInitialValue.Value);

            AddComment("}");
        }
        else if (newVariable.Type.Is(out ArrayType? arrayType1) &&
            arrayType1.Of.SameAs(BasicType.U16) &&
            newVariable.InitialValue is CompiledString literalStatement1 &&
            arrayType1.Length.HasValue &&
            literalStatement1.Value.Length == arrayType1.Length.Value)
        {
            size = FindSize(arrayType1, newVariable);

            for (int i = 0; i < literalStatement1.Value.Length; i++)
            {
                Push(new CompiledValue(literalStatement1.Value[i]));
            }
        }
        else if (newVariable.Type.Is(out ArrayType? arrayType2) &&
            arrayType2.Of.SameAs(BasicType.U8) &&
            newVariable.InitialValue is CompiledString literalStatement2 &&
            arrayType2.Length.HasValue &&
            Encoding.UTF8.GetByteCount(literalStatement2.Value) == arrayType2.Length.Value)
        {
            size = FindSize(arrayType2, newVariable);
            byte[] bytes = Encoding.UTF8.GetBytes(literalStatement2.Value);

            for (int i = 0; i < bytes.Length; i++)
            {
                Push(new CompiledValue(bytes[i]));
            }
        }
        else
        {
            AddComment($"Initial value {{");

            size = FindSize(newVariable.Type, newVariable);
            StackAlloc(size, false);

            if (size <= 0)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Variable has a size of {size}", newVariable));
                return default;
            }

            AddComment("}");
        }

        if (size != FindSize(newVariable.Cleanup.TrashType, newVariable))
        { Diagnostics.Add(DiagnosticAt.Internal($"Variable size ({FindSize(newVariable.Cleanup.TrashType, newVariable)} bytes) and initial value size ({size} bytes) mismatch", newVariable.InitialValue!)); }

        return newVariable.Cleanup;
    }

    #endregion

    #region GenerateCodeFor...

    int VariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariableDefinition variable in CompiledLocalVariables)
            { sum += FindSize(variable.Type, variable); }
            return sum;
        }
    }

    int GlobalVariablesSize
    {
        get
        {
            int sum = 0;
            foreach (CompiledVariableDefinition variable in CompiledGlobalVariables)
            { sum += FindSize(variable.Type, variable); }
            return sum;
        }
    }

    void GenerateCodeForFunction(ICompiledFunctionDefinition function, ImmutableDictionary<string, GeneralType>? typeArguments, CompiledBlock body)
    {
        if (GeneratedFunctions.Any(v => Utils.ReferenceEquals(v.Template, function) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, typeArguments))) return;
        GeneratedFunctions.Add(TemplateInstance.New(function, typeArguments));

        InstructionLabel label = LabelForDefinition(TemplateInstance.New(function, typeArguments));
        Code.MarkLabel(label);

        if (function is IExposeable exposeable && exposeable.ExposedFunctionName is not null)
        {
            label.Keep = true;
        }

        CurrentContext = function;
        InFunction = true;
        FunctionFlags functionFlags;
        if (function is CompiledLambda l)
        {
            functionFlags = l.Flags;
        }
        else
        {
            CompiledFunction? f = Functions.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, function) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, typeArguments));
            if (f is null)
            {
                Diagnostics.Add(DiagnosticAt.Internal($"function {function.ToReadable()} not compiled", function));
                functionFlags = FunctionFlags.None;
            }
            else
            {
                functionFlags = f.Flags;
            }
        }

        TypeArguments.Clear();
        CompiledParameters.Clear();
        CompiledLocalVariables.Clear();
        ReturnInstructions.Clear();
        ScopeSizes.Push(0);
        HasCapturedGlobalVariables = functionFlags.HasFlag(FunctionFlags.CapturesGlobalVariables);
        int savedInstructionLabelCount = CompiledInstructionLabels.Count;

        if (typeArguments is not null) TypeArguments.AddRange(typeArguments);
        CompiledParameters.AddRange(function.Parameters);

        if (function is IHaveCompiledType functionWithType)
        { CurrentReturnType = functionWithType.Type; }
        else
        { CurrentReturnType = BuiltinType.Void; }

        if (body is null)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function \"{function.ToReadable()}\" does not have a body", function));
            return;
        }

        int instructionsStart = Code.Offset;

        AddComment("Create stack frame");
        Push(Register.BasePointer);
        Code.Emit(Opcode.Move, Register.BasePointer, Register.StackPointer);

        int frameInstructionsStart = Code.Offset;

        CompiledScope scope = OnScopeEnter(body, true);

        if (function is IHaveCompiledType returnType && !returnType.Type.SameAs(BasicType.Void))
        {
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = ReturnValueAddress.Offset,
                BasePointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = FindSize(GeneralType.TryInsertTypeParameters(returnType.Type, typeArguments), function),
                Identifier = "Return Value",
                Type = GeneralType.TryInsertTypeParameters(returnType.Type, typeArguments),
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
            GeneralType pType = GeneralType.TryInsertTypeParameters(p.Type, typeArguments);

            if (!GetParameterAddress(p, 0, out Address? address, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToWarning(p));
                continue;
            }

            if (address is not AddressOffset addressOffset) continue;

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = addressOffset.Offset,
                Kind = StackElementKind.Parameter,
                BasePointerRelative = true,
                Size = FindSize(pType, p.Definition),
                Identifier = p.Identifier,
                Type = pType,
            });
        }

        InstructionLabel returnLabel = Code.DefineLabel();
        ReturnInstructions.Push(new ControlFlowFrame(returnLabel));

        for (int i = 0; i < body.Statements.Length; i++)
        {
            if (i == body.Statements.Length - 1
                && body.Statements[i] is CompiledFunctionCall functionCallExpression)
            {
                functionCallExpression.IsAtTail = true;
            }
            else if (i == body.Statements.Length - 1
                && body.Statements[i] is CompiledReturn compiledReturn
                && compiledReturn.Value is CompiledFunctionCall compiledFunctionCall)
            {
                compiledFunctionCall.IsAtTailReturn = true;
            }
        }

        GenerateCodeForStatement(body, true);

        CurrentReturnType = null;

        OnScopeExit(body.Location.Position.After(), body.Location.File, scope);

        Code.MarkLabel(returnLabel);
        ReturnInstructions.Pop();

        int frameInstructionsEnd = Code.Offset;

        AddComment("Return");
        Return();

        if (body is not null) AddComment("}");

        DebugInfo?.FunctionInformation.Add(new FunctionInformation()
        {
            IsValid = true,
            Function = function,
            TypeArguments = TypeArguments.ToImmutableDictionary(),
            Instructions = (instructionsStart, Code.Offset),
            FrameInstructions = (frameInstructionsStart, frameInstructionsEnd),
        });

        while (CompiledInstructionLabels.Count > savedInstructionLabelCount)
        {
            CompiledInstructionLabels.Pop();
        }

        TypeArguments.Clear();
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
                Instructions = (Code.Offset, Code.Offset + 1),
                Location = statements.Select(v => v.Location).Aggregate((a, b) => a.Union(b)),
            },
            Stack = new List<StackElementInformation>(),
        });

        ScopeSizes.Push(0);

        AddComment("TopLevelStatements {");

        int instructionsStart = Code.Offset;

        AddComment("Create stack frame");
        Push(Register.BasePointer);
        Code.Emit(Opcode.Move, Register.BasePointer, Register.StackPointer);

        int frameInstructionsStart = Code.Offset;

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

        int frameInstructionsEnd = Code.Offset;

        if (!Settings.IsExpression)
        {
            AddComment("Pop stack frame");
            PopTo(Register.BasePointer);
        }

        if (ScopeSizes.Pop() != 0) { }

        AddComment("}");

        DebugInfo?.FunctionInformation.Add(new FunctionInformation()
        {
            IsValid = true,
            Function = null,
            IsTopLevelStub = true,
            TypeArguments = TypeArguments.ToImmutableDictionary(),
            Instructions = (instructionsStart, Code.Offset),
            FrameInstructions = (frameInstructionsStart, frameInstructionsEnd),
        });

        ScopeInformation scope = CurrentScopeDebug.Pop();
        scope.Location.Instructions.End = frameInstructionsEnd;
        DebugInfo?.ScopeInformation.Add(scope);
    }

    #endregion

    BBLangGeneratorResult GenerateCode(CompilerResult compilerResult, MainGeneratorSettings settings)
    {
        ScopeSizes.Push(0);

        /*
        if (false)
        {
            AddComment("Create stack frame");
            Push(Register.BasePointer);
            Code.Emit(Opcode.Move, Register.BasePointer, Register.StackPointer);

            EmitIRAll(IRGenerator.Generate(compilerResult));

            Code.Emit(Opcode.Exit);

            while (UndefinedFunctionOffsets.Count > 0)
            {
                foreach (UndefinedOffset undefinedOffset in UndefinedFunctionOffsets.ToArray())
                {
                    if (undefinedOffset.Called.Template is CompiledLambda compiledLambda)
                    {
                        GenerateCodeForFunction(compiledLambda, null, compiledLambda.Block);
                        goto ok;
                    }

                    foreach (CompiledFunction function in compilerResult.Functions)
                    {
                        if (!Utils.ReferenceEquals(undefinedOffset.Called.Template, function.Function)
                            || !StatementCompiler.TypeArgumentsEquals(undefinedOffset.Called.TypeArguments, function.TypeArguments))
                        { continue; }
                        GenerateCodeForFunction(function.Function, function.TypeArguments, function.Body);
                        goto ok;
                    }

                    if (!Diagnostics.HasErrors) Diagnostics.Add(DiagnosticAt.Error($"Function {undefinedOffset.Called} wasn't compiled for some reason", undefinedOffset.CallerLocation));
                    goto failed2;
                ok:;
                }

                for (int i = 0; i < UndefinedFunctionOffsets.Count; i++)
                {
                    if (LabelForDefinition(UndefinedFunctionOffsets[i].Called).IsMarked)
                    {
                        UndefinedFunctionOffsets.RemoveAt(i--);
                    }
                }
            }
        failed2:

            return new BBLangGeneratorResult()
            {
                Code = Code.Compile(new Dictionary<string, int>()
                {
                    { "heap_start", Strings.Sum(v => v.Value.Length) }
                }),
                CodeEmitter = Code,
                DebugInfo = DebugInfo,

                CompiledFunctions = compilerResult.FunctionDefinitions,
                CompiledOperators = compilerResult.OperatorDefinitions,
                CompiledGeneralFunctions = compilerResult.GeneralFunctionDefinitions,
                CompiledConstructors = compilerResult.ConstructorDefinitions,

                ExposedFunctions = FrozenDictionary<string, ExposedFunction>.Empty,
                GeneratedUnmanagedFunctions = GeneratedUnmanagedFunctions.ToImmutableArray(v => v.Function),
                GeneratedUnmanagedFunctionReferences = GeneratedUnmanagedFunctions.ToImmutableArray(v => v.Reference),
                ILGeneratorBuilders = ILGenerator?.Builders?.ToImmutableArray() ?? ImmutableArray<string>.Empty,
            };
        }
        */

        CurrentScopeDebug.Push(new ScopeInformation()
        {
            Location = new SourceCodeLocation()
            {
                Instructions = (Code.Offset, Code.Offset + 1),
                Location = new Location(Position.UnknownPosition, compilerResult.File),
            },
            Stack = new List<StackElementInformation>(),
        });

        if (!Settings.IsExpression)
        {
            // Exit code

            AddComment("Push exit code");
            Push(new CompiledValue(0));

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformation()
            {
                Address = (FindSize(ExitCodeType) - 1) * ProcessorState.StackDirection,
                BasePointerRelative = false,
                Kind = StackElementKind.Internal,
                Size = FindSize(ExitCodeType),
                Identifier = "Exit Code",
                Type = ExitCodeType,
            });
        }

        IEnumerable<CompiledVariableDefinition> globalVariableDeclarations = compilerResult.Statements
            .OfType<CompiledVariableDefinition>();

        Stack<CompiledCleanup> globalVariablesCleanup = new();
        foreach (CompiledVariableDefinition variableDeclaration in globalVariableDeclarations)
        {
            // if (variableDeclaration.Getters.Count == 0 &&
            //     variableDeclaration.Setters.Count == 0)
            // { continue; }

            GeneralType type = variableDeclaration.Type;

            int size = FindSize(type, variableDeclaration);
            int currentOffset = GlobalVariablesSize;

            GeneratedVariables[variableDeclaration] = new GeneratedVariable()
            {
                MemoryAddress = currentOffset,
            };

            CurrentScopeDebug.LastRef.Stack.Add(new StackElementInformation()
            {
                Address = currentOffset,
                BasePointerRelative = false,
                Kind = StackElementKind.GlobalVariable,
                Size = size,
                Identifier = variableDeclaration.Identifier,
                Type = variableDeclaration.Type,
            });

            CompiledGlobalVariables.Add(variableDeclaration);
            globalVariablesCleanup.Insert(0, variableDeclaration.Cleanup);
        }

        if (GlobalVariablesSize > 0)
        {
            AddComment("Allocate global variables {");
            StackAlloc(GlobalVariablesSize, false);
            AddComment("}");

            HasCapturedGlobalVariables = true;

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

        if (HasCapturedGlobalVariables)
        {
            AddComment("Pop abs global address");
            Pop(FindSize(AbsGlobalAddressType)); // Pop abs global offset
        }

        if (Settings.CleanupGlobalVaraibles && globalVariablesCleanup.Count > 0)
        {
            if (!HasCapturedGlobalVariables) throw new UnreachableException();
            AddComment("Cleanup global variables {");
            CleanupVariables(globalVariablesCleanup.ToImmutableArray(), default, true);
            AddComment("}");
        }

        Code.Emit(Opcode.Exit);

        HasCapturedGlobalVariables = false;

        // 4 -> exit code
        if (ScopeSizes.Pop() != 4) { } // throw new InternalException("Bruh");

        ScopeInformation topLevelScope = CurrentScopeDebug.Pop();
        topLevelScope.Location.Instructions.End = Code.Offset;

        foreach (CompiledFunction function in compilerResult.Functions)
        {
            if (function.Function is IExposeable exposeable && exposeable.ExposedFunctionName is not null)
            { GenerateCodeForFunction(function.Function, function.TypeArguments, function.Body); }
        }

        while (UndefinedFunctionOffsets.Count > 0)
        {
            foreach (UndefinedOffset undefinedOffset in UndefinedFunctionOffsets.ToArray())
            {
                if (undefinedOffset.Called.Template is CompiledLambda compiledLambda)
                {
                    GenerateCodeForFunction(compiledLambda, null, compiledLambda.Block);
                    goto ok;
                }

                foreach (CompiledFunction function in compilerResult.Functions)
                {
                    if (!Utils.ReferenceEquals(undefinedOffset.Called.Template, function.Function)
                        || !StatementCompiler.TypeArgumentsEquals(undefinedOffset.Called.TypeArguments, function.TypeArguments))
                    { continue; }
                    GenerateCodeForFunction(function.Function, function.TypeArguments, function.Body);
                    goto ok;
                }

                if (!Diagnostics.HasErrors) Diagnostics.Add(DiagnosticAt.Error($"Function {undefinedOffset.Called} wasn't compiled for some reason", undefinedOffset.CallerLocation));
                goto failed;
            ok:;
            }

            for (int i = 0; i < UndefinedFunctionOffsets.Count; i++)
            {
                if (LabelForDefinition(UndefinedFunctionOffsets[i].Called).IsMarked)
                {
                    UndefinedFunctionOffsets.RemoveAt(i--);
                }
            }
        }
    failed:

        List<PreparationInstruction> stringInitializationInstructions = new();

        foreach (GeneratedString _string in Strings)
        {
            for (int i = 0; i < _string.Value.Length; i++)
            {
                stringInitializationInstructions.Add(new PreparationInstruction(Opcode.Move, new InstructionOperand(i + _string.Offset, InstructionOperandType.Pointer8), new InstructionOperand(_string.Value[i], InstructionOperandType.Immediate8)));
            }
        }

        Code.Insert(0, stringInitializationInstructions);
        topLevelScope.Location.Instructions.End += stringInitializationInstructions.Count;

        DebugInfo?.ScopeInformation.Add(topLevelScope);

        ImmutableArray<Instruction> code = Code.Compile(new Dictionary<string, int>()
        {
            { "heap_start", Strings.Sum(v => v.Value.Length) }
        });

        Dictionary<string, ExposedFunction> exposedFunctions = new();
        if (!compilerResult.IsExpression)
        {
            foreach (CompiledFunctionDefinition f in compilerResult.FunctionDefinitions)
            {
                if (f.ExposedFunctionName is null) continue;
                InstructionLabel label = LabelForDefinition(TemplateInstance.New(f, null));
                if (!label.IsMarked)
                {
                    Diagnostics.Add(DiagnosticAt.Internal($"Exposed function \"{f.ToReadable()}\" was not compiled", f.Definition.Identifier, f.File));
                    continue;
                }

                CompiledFunction? e = Functions.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, f) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, null));
                if (e is null)
                {
                    Diagnostics.Add(DiagnosticAt.Internal($"Function \"{f.ToReadable()}\" wasn't compiled", f));
                    continue;
                }

                int returnValueSize = f.ReturnSomething ? FindSize(f.Type, f.Definition.Type) : 0;
                int argumentsSize = 0;
                foreach (CompiledParameter p in f.Parameters)
                { argumentsSize += FindSize(p.Type, f.Definition.Type); }

                exposedFunctions[f.ExposedFunctionName] = new(f.ExposedFunctionName, returnValueSize, label.Address, argumentsSize, e.Flags);
            }
        }

        return new BBLangGeneratorResult()
        {
            Code = code,
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
