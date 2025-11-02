#pragma warning disable IDE0060 // Remove unused parameter

using System.Reflection;
using System.Reflection.Emit;
using LanguageCore.Compiler;
using LanguageCore.IL.Reflection;
using LanguageCore.Parser;
using LanguageCore.Runtime;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForIL : CodeGenerator
{
    readonly Dictionary<CompiledVariableDeclaration, LocalBuilder> LocalBuilders = new();
    readonly Stack<Label> LoopLabels = new();
    readonly Dictionary<ICompiledFunctionDefinition, DynamicMethod> FunctionBuilders = new();
    readonly HashSet<ICompiledFunctionDefinition> EmittedFunctions = new();
    readonly Dictionary<CompiledInstructionLabelDeclaration, Label> EmittedLabels = new();
    readonly ModuleBuilder Module;

    public readonly List<string>? Builders;

    void GenerateDeallocator(CompiledCleanup cleanup, ILProxy il, ref bool successful)
    {
        if (cleanup.Deallocator is null) return;

        /*

        if (cleanup.Deallocator.ExternalFunctionName is not null)
        {
            Diagnostics.Add(Diagnostic.Critical($"External deallocator not supported", cleanup));
            successful = false;
            return;
        }

        if (cleanup.Deallocator.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Deallocator should not return anything", cleanup));
            successful = false;
            return;
        }

        if (!EmitFunction(cleanup.Deallocator, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to emit function \"{cleanup.Deallocator}\"", cleanup));
            successful = false;
            return;
        }

        if (function.GetParameters().Length != 1)
        {
            Diagnostics.Add(Diagnostic.Internal($"Invalid deallocator", cleanup));
            successful = false;
            return;
        }

        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Call, function);
        */
    }

    void GenerateDestructor(CompiledCleanup cleanup, ILProxy il, ref bool successful)
    {
        GeneralType deallocateableType = cleanup.TrashType;

        if (cleanup.TrashType.Is<PointerType>())
        {
            if (cleanup.Destructor is null)
            {
                GenerateDeallocator(cleanup, il, ref successful);
                return;
            }
        }
        else
        {
            if (cleanup.Destructor is null)
            { return; }
        }

        if (!EmitFunction(cleanup.Destructor, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to emit function \"{cleanup.Destructor}\"", cleanup));
            successful = false;
            return;
        }

        if (function.GetParameters().Length != 1)
        {
            Diagnostics.Add(Diagnostic.Internal($"Invalid destructor", cleanup));
            successful = false;
            return;
        }

        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Call, function);

        if (deallocateableType.Is<PointerType>())
        {
            GenerateDeallocator(cleanup, il, ref successful);
        }
    }

    void EmitStatement(CompiledEvaluatedValue statement, ILProxy il, ref bool successful)
    {
        switch (statement.Value.Type)
        {
            case RuntimeType.U8:
            case RuntimeType.I8:
            case RuntimeType.U16:
            case RuntimeType.I16:
            case RuntimeType.I32:
            case RuntimeType.U32:
                EmitValue(statement.Value.I32, il);
                return;
            case RuntimeType.F32:
                EmitValue(statement.Value.F32, il);
                return;
            case RuntimeType.Null:
                Diagnostics.Add(Diagnostic.Internal($"Value has type of null", statement));
                return;
            default:
                throw new UnreachableException();
        }
    }
    void EmitStatement(CompiledReturn statement, ILProxy il, ref bool successful)
    {
        if (statement.Value is not null)
        {
            EmitStatement(statement.Value, il, ref successful);
        }
        il.Emit(OpCodes.Ret);
    }
    void EmitStatement(CompiledBinaryOperatorCall statement, ILProxy il, ref bool successful)
    {
        switch (statement.Operator)
        {
            case "+":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Add);
                return;
            }
            case "-":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Sub);
                return;
            }
            case "*":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Mul);
                return;
            }
            case "/":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Div);
                return;
            }
            case "%":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Rem);
                return;
            }
            case "&":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.And);
                return;
            }
            case "|":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Or);
                return;
            }
            case "^":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Xor);
                return;
            }
            case "<<":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Shl);
                return;
            }
            case ">>":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Shr);
                return;
            }
            case "<":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Blt, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case ">":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Bgt, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case "<=":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Ble, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case ">=":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Bge, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case "==":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Beq, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case "!=":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelFalse = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Beq, labelFalse);

                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelFalse);
                il.Emit(OpCodes.Ldc_I4_0);

                il.MarkLabel(labelEnd);

                return;
            }
            case "&&":
            {
                Label labelFalse = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Brfalse, labelFalse);

                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Brfalse, labelFalse);

                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelFalse);
                il.Emit(OpCodes.Ldc_I4_0);

                il.MarkLabel(labelEnd);

                return;
            }
            case "||":
            {
                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Brtrue, labelTrue);

                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Brtrue, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
        }

        Diagnostics.Add(Diagnostic.Critical($"Unimplemented binary operator {statement.Operator}", statement));
        successful = false;
        return;
    }
    void EmitStatement(CompiledUnaryOperatorCall statement, ILProxy il, ref bool successful)
    {
        switch (statement.Operator)
        {
            case "!":
            {
                Label labelFalse = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Brfalse, labelFalse);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelFalse);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }

            case "-":
            {
                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Neg);

                return;
            }

            case "+":
            {
                EmitStatement(statement.Left, il, ref successful);

                return;
            }

            case "~":
            {
                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Not);

                return;
            }
        }

        Diagnostics.Add(Diagnostic.Critical($"Unimplemented unary operator {statement.Operator}", statement));
        successful = false;
        return;
    }
    void EmitStatement(CompiledVariableDeclaration statement, ILProxy il, ref bool successful)
    {
        if (statement.IsGlobal)
        {
            //Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement));
            //successful = false;

            if (!EmittedGlobalVariables.TryGetValue(statement, out FieldInfo? field))
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Identifier}\" wasn't emitted for some reason", statement, successful));
                successful = false;
                return;
            }

            if (statement.InitialValue is not null)
            {
                EmitStatement(statement.InitialValue, il, ref successful);
                il.Emit(OpCodes.Stsfld, field);
            }
        }
        else
        {
            if (LocalBuilders.ContainsKey(statement)) return;
            if (!ToType(statement.Type, out Type? type, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement));
                successful = false;
                return;
            }

            LocalBuilder local = LocalBuilders[statement] = il.DeclareLocal(type);
            if (statement.InitialValue is not null)
            {
                switch (statement.InitialValue)
                {
                    case CompiledConstructorCall constructorCall:
                        EmitStatement(constructorCall, il, ref successful, local);
                        break;
                    case CompiledStackAllocation compiledStackAllocation:
                        EmitStatement(compiledStackAllocation, il, ref successful, local);
                        break;
                    default:
                        EmitStatement(statement.InitialValue, il, ref successful);
                        StoreLocal(il, local.LocalIndex);
                        break;
                }
            }
        }
    }
    void EmitStatement(CompiledVariableGetter statement, ILProxy il, ref bool successful)
    {
        if (statement.Variable.IsGlobal)
        {
            //Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement));
            //successful = false;

            if (!EmittedGlobalVariables.TryGetValue(statement.Variable, out FieldInfo? field))
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement, successful));
                successful = false;
                return;
            }

            il.Emit(OpCodes.Ldsfld, field);
            return;
        }

        if (!LocalBuilders.TryGetValue(statement.Variable, out LocalBuilder? local))
        {
            Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement, successful));
            successful = false;
            return;
        }

        LoadLocal(il, local.LocalIndex);
    }
    void EmitStatement(CompiledParameterGetter statement, ILProxy il, ref bool successful)
    {
        LoadArgument(il, statement.Variable.Index);
    }
    void EmitStatement(CompiledFieldGetter statement, ILProxy il, ref bool successful)
    {
        CompiledStatementWithValue _object = statement.Object;

        if (!_object.Type.Is(out PointerType? objectType))
        {
            _object = new CompiledAddressGetter()
            {
                Of = _object,
                Type = objectType = new PointerType(_object.Type),
                Location = _object.Location,
                SaveValue = true,
            };
        }

        EmitStatement(_object, il, ref successful);

        while (objectType.To.Is(out PointerType? indirectPointer))
        {
            il.Emit(OpCodes.Ldind_Ref);
            objectType = indirectPointer;
        }

        if (objectType.To.Is(out StructType? structType))
        {
            if (!ToType(structType, out Type? type, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement));
                successful = false;
                return;
            }

            FieldInfo? field = type.GetField(statement.Field.Identifier.Content);
            if (field is null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Field \"{statement.Field.Identifier.Content}\" not found in type {type}", _object));
                successful = false;
                return;
            }
            il.Emit(OpCodes.Ldfld, field);
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a struct", statement.Object));
            successful = false;
            return;
        }
    }
    void EmitStatement(CompiledFunctionCall statement, ILProxy il, ref bool successful)
    {
        if (statement.Function is CompiledFunctionDefinition compiledFunction &&
            compiledFunction.BuiltinFunctionName == "free")
        {
            return;
        }

        if (!EmitFunction(statement.Function, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to emit function \"{statement.Function}\"", statement, successful));
            successful = false;
            return;
        }

        for (int i = 0; i < statement.Arguments.Length; i++)
        {
            EmitStatement(statement.Arguments[i].Value, il, ref successful);
        }

        il.Emit(OpCodes.Call, function);

        if (!statement.SaveValue && function.ReturnType != typeof(void))
        {
            il.Emit(OpCodes.Pop);
        }
    }
    void EmitStatement(CompiledExternalFunctionCall statement, ILProxy il, ref bool successful)
    {
        switch (statement.Function)
        {
            case ExternalFunctionSync f:
            {
                if (f.UnmarshaledCallback.Method.IsStatic)
                {
                    foreach (CompiledPassedArgument item in statement.Arguments)
                    {
                        EmitStatement(item.Value, il, ref successful);
                    }
                    il.Emit(OpCodes.Call, f.UnmarshaledCallback.GetMethodInfo());

                    return;
                }

                // FIXME test 70 & 71
                if (false && f.UnmarshaledCallback.Target is not null)
                {
                    int i = DelegateTargets.IndexOf(f.UnmarshaledCallback.Target);
                    if (i == -1)
                    {
                        i = DelegateTargets.Count;
                        DelegateTargets.Add(f.UnmarshaledCallback.Target);
                        GlobalContextType_Targets.SetValue(null, DelegateTargets.ToArray());
                    }

                    il.Emit(OpCodes.Ldsflda, GlobalContextType_Targets);
                    EmitValue(i, il);
                    il.Emit(OpCodes.Ldelem_Ref);

                    foreach (CompiledPassedArgument item in statement.Arguments)
                    {
                        EmitStatement(item.Value, il, ref successful);
                    }
                    il.Emit(OpCodes.Call, f.UnmarshaledCallback.GetMethodInfo());

                    return;
                }

                Diagnostics.Add(Diagnostic.Critical($"Non-static external functions not supported", statement, false));
                successful = false;
                return;
            }
            case ExternalFunctionStub:
                Diagnostics.Add(Diagnostic.Critical($"Can't call an external function stub", statement, false));
                successful = false;
                return;
            default:
                Diagnostics.Add(Diagnostic.Critical($"{statement.Function.GetType()} external functions not supported", statement));
                successful = false;
                return;
        }
    }
    void EmitStatement(CompiledVariableSetter statement, ILProxy il, ref bool successful)
    {
        if (statement.Variable.IsGlobal)
        {
            //Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement));
            //successful = false;

            if (!EmittedGlobalVariables.TryGetValue(statement.Variable, out FieldInfo? field))
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement, successful));
                successful = false;
                return;
            }

            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Stsfld, field);
            return;
        }

        if (!LocalBuilders.TryGetValue(statement.Variable, out LocalBuilder? local))
        {
            Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement, successful));
            successful = false;
            return;
        }

        switch (statement.Value)
        {
            case CompiledConstructorCall constructorCall:
                EmitStatement(constructorCall, il, ref successful, local);
                break;
            case CompiledStackAllocation stackAllocation:
                EmitStatement(stackAllocation, il, ref successful, local);
                break;
            default:
                EmitStatement(statement.Value, il, ref successful);
                StoreLocal(il, local.LocalIndex);
                break;
        }
    }
    void EmitStatement(CompiledParameterSetter statement, ILProxy il, ref bool successful)
    {
        EmitStatement(statement.Value, il, ref successful);
        il.Emit(OpCodes.Starg, statement.Variable.Index);
    }
    void EmitStatement(CompiledFieldSetter statement, ILProxy il, ref bool successful)
    {
        CompiledStatementWithValue _object = statement.Object;

        if (!_object.Type.Is(out PointerType? objectType))
        {
            _object = new CompiledAddressGetter()
            {
                Of = _object,
                Type = objectType = new PointerType(_object.Type),
                Location = _object.Location,
                SaveValue = true,
            };
        }

        EmitStatement(_object, il, ref successful);

        while (objectType.To.Is(out PointerType? indirectPointer))
        {
            il.Emit(OpCodes.Ldind_Ref);
            objectType = indirectPointer;
        }

        if (objectType.To.Is(out StructType? structType))
        {
            if (!ToType(structType, out Type? type, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement));
                successful = false;
                return;
            }

            FieldInfo? field = type.GetField(statement.Field.Identifier.Content);
            if (field is null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Field \"{statement.Field.Identifier.Content}\" not found in type {type}", _object));
                successful = false;
                return;
            }

            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Stfld, field);
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a struct", statement.Object));
            successful = false;
        }
    }
    void EmitStatement(CompiledIndirectSetter statement, ILProxy il, ref bool successful)
    {
        if (!Settings.AllowPointers)
        {
            Diagnostics.Add(Diagnostic.Critical($"Pointers are banned by the generator settings", statement, false));
            successful = false;
            return;
        }

        switch (statement.AddressValue)
        {
            case CompiledVariableGetter:
            case CompiledParameterGetter:
            case CompiledFieldGetter:
                break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unsafe!!!", statement, successful));
                successful = false;
                break;
        }

        EmitStatement(statement.AddressValue, il, ref successful);
        EmitStatement(statement.Value, il, ref successful);
        if (!statement.AddressValue.Type.Is(out PointerType? pointer))
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a pointer", statement.AddressValue));
            successful = false;
            return;
        }

        if (!StoreIndirect(pointer.To, il, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(statement));
            successful = false;
            return;
        }
    }
    void EmitStatement(CompiledPointer statement, ILProxy il, ref bool successful)
    {
        if (!Settings.AllowPointers)
        {
            Diagnostics.Add(Diagnostic.Critical($"Pointers are banned by the generator settings", statement, false));
            successful = false;
            return;
        }

        switch (statement.To)
        {
            case CompiledVariableGetter:
            case CompiledParameterGetter:
            case CompiledFieldGetter:
                break;
            default:
                Debugger.Break();
                Diagnostics.Add(Diagnostic.Critical($"Unsafe!!!", statement));
                successful = false;
                return;
        }

        EmitStatement(statement.To, il, ref successful);

        if (!statement.To.Type.Is(out PointerType? pointer))
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a pointer", statement.To));
            successful = false;
            return;
        }

        if (!LoadIndirect(pointer.To, il, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(statement.To));
            successful = false;
            return;
        }
    }
    void EmitStatement(CompiledWhileLoop statement, ILProxy il, ref bool successful)
    {
        Label loopStart = il.DefineLabel();
        Label loopEnd = il.DefineLabel();

        LoopLabels.Push(loopEnd);

        il.MarkLabel(loopStart);
        EmitStatement(statement.Condition, il, ref successful);
        il.Emit(OpCodes.Brfalse, loopEnd);

        EmitStatement(statement.Body, il, ref successful);

        il.Emit(OpCodes.Br, loopStart);
        il.MarkLabel(loopEnd);

        if (LoopLabels.Pop() != loopEnd)
        {
            Diagnostics.Add(Diagnostic.Internal($"Something went wrong ...", statement));
            successful = false;
        }
    }
    void EmitStatement(CompiledForLoop statement, ILProxy il, ref bool successful)
    {
        if (statement.VariableDeclaration is not null)
        {
            EmitStatement(statement.VariableDeclaration, il, ref successful);
        }

        Label loopStart = il.DefineLabel();
        Label loopEnd = il.DefineLabel();

        LoopLabels.Push(loopEnd);

        il.MarkLabel(loopStart);
        EmitStatement(statement.Condition, il, ref successful);
        il.Emit(OpCodes.Brfalse, loopEnd);

        EmitStatement(statement.Body, il, ref successful);
        EmitStatement(statement.Expression, il, ref successful);
        il.Emit(OpCodes.Br, loopStart);

        il.MarkLabel(loopEnd);

        if (LoopLabels.Pop() != loopEnd)
        {
            Diagnostics.Add(Diagnostic.Internal($"Something went wrong ...", statement));
            successful = false;
        }
    }
    void EmitStatement(CompiledIf statement, ILProxy il, ref bool successful)
    {
        Label labelEnd = il.DefineLabel();

        CompiledBranch? current = statement;
        while (current is not null)
        {
            switch (current)
            {
                case CompiledIf _if:
                    Label labelNext = il.DefineLabel();

                    EmitStatement(_if.Condition, il, ref successful);
                    il.Emit(OpCodes.Brfalse, labelNext);

                    EmitStatement(_if.Body, il, ref successful);
                    il.Emit(OpCodes.Br, labelEnd);

                    il.MarkLabel(labelNext);

                    current = _if.Next;
                    break;

                case CompiledElse _else:
                    EmitStatement(_else.Body, il, ref successful);

                    current = null;
                    break;

                default:
                    throw new UnreachableException();
            }
        }

        il.MarkLabel(labelEnd);
    }
    void EmitStatement(CompiledBreak statement, ILProxy il, ref bool successful)
    {
        if (LoopLabels.Count == 0)
        {
            Diagnostics.Add(Diagnostic.Critical($"You can only break in a loop", statement));
            successful = false;
            return;
        }

        il.Emit(OpCodes.Br, LoopLabels.Last);
    }
    void EmitStatement(CompiledBlock statement, ILProxy il, ref bool successful)
    {
        foreach (CompiledStatement v in statement.Statements)
        {
            EmitStatement(v, il, ref successful);
        }
    }
    void EmitStatement(CompiledAddressGetter statement, ILProxy il, ref bool successful)
    {
        if (!Settings.AllowPointers)
        {
            Diagnostics.Add(Diagnostic.Critical($"Pointers are banned by the generator settings", statement, false));
            successful = false;
            return;
        }

        switch (statement.Of)
        {
            case CompiledVariableGetter v:
                if (v.Variable.IsGlobal)
                {
                    //Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement));
                    //successful = false;

                    if (!EmittedGlobalVariables.TryGetValue(v.Variable, out FieldInfo? field))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Variable \"{v.Variable.Identifier}\" wasn't emitted for some reason", statement, successful));
                        successful = false;
                        return;
                    }

                    il.Emit(OpCodes.Ldsflda, field);
                    return;
                }

                if (!LocalBuilders.TryGetValue(v.Variable, out LocalBuilder? local))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Variable \"{v.Variable.Identifier}\" not compiled", statement, successful));
                    successful = false;
                    return;
                }
                il.Emit(OpCodes.Ldloca_S, local);
                break;
            case CompiledParameterGetter v:
                il.Emit(OpCodes.Ldarga_S, v.Variable.Index);
                break;
            case CompiledFieldGetter v:
                CompiledStatementWithValue _object = v.Object;

                if (!_object.Type.Is(out PointerType? objectType))
                {
                    _object = new CompiledAddressGetter()
                    {
                        Of = _object,
                        Type = objectType = new PointerType(_object.Type),
                        Location = _object.Location,
                        SaveValue = true,
                    };
                }

                EmitStatement(_object, il, ref successful);

                while (objectType.To.Is(out PointerType? indirectPointer))
                {
                    il.Emit(OpCodes.Ldind_Ref);
                    objectType = indirectPointer;
                }

                if (objectType.To.Is(out StructType? structType))
                {
                    if (!ToType(structType, out Type? type, out PossibleDiagnostic? typeError))
                    {
                        Diagnostics.Add(typeError.ToError(statement));
                        successful = false;
                        return;
                    }

                    FieldInfo? field = type.GetField(v.Field.Identifier.Content);
                    if (field is null)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Field \"{v.Field.Identifier.Content}\" not found in type {type}", _object));
                        successful = false;
                        return;
                    }
                    il.Emit(OpCodes.Ldflda, field);
                }
                else
                {
                    Diagnostics.Add(Diagnostic.Critical($"This should be a struct", v.Object));
                    successful = false;
                }
                break;
            case CompiledIndexGetter v:
                if (v.Base.Type.Is(out PointerType? basePointerType) &&
                    basePointerType.To.Is(out ArrayType? baseArrayType))
                {
                    if (baseArrayType.Of.SameAs(BuiltinType.Char))
                    {
                        Diagnostics.Add(Diagnostic.Internal($"Nah", statement));
                        successful = false;
                        break;
                    }

                    if (!ToType(baseArrayType.Of, out Type? elementType, out PossibleDiagnostic? typeError))
                    {
                        Diagnostics.Add(typeError.ToError(v.Base));
                        successful = false;
                        break;
                    }

                    EmitStatement(v.Base, il, ref successful);
                    EmitStatement(v.Index, il, ref successful);
                    EmitValue(SizeOf(elementType), il);
                    il.Emit(OpCodes.Mul);
                    il.Emit(OpCodes.Add);

                    if (!LoadIndirect(elementType, il, out PossibleDiagnostic? loadIndirectError))
                    {
                        Diagnostics.Add(loadIndirectError.ToError(statement));
                        successful = false;
                    }

                    break;
                }

                if (v.Base.Type.Is<ArrayType>())
                {
                    EmitInlineArrayElementRef(v.Base, v.Index, il, ref successful);
                    break;
                }

                Diagnostics.Add(Diagnostic.Critical($"This should be an array", v.Base));
                successful = false;
                break;
            default:
                Debugger.Break();
                Diagnostics.Add(Diagnostic.Critical($"Can't get the address of \"{statement.Of}\" ({statement.Of.GetType().Name})", statement.Of));
                successful = false;
                break;
        }
    }
    void EmitStatement(CompiledHeapAllocation statement, ILProxy il, ref bool successful)
    {
        if (!Settings.AllowHeap)
        {
            Diagnostics.Add(Diagnostic.Critical($"Heap is banned by the generator settings", statement, false));
            successful = false;
            return;
        }

        if (!statement.Type.Is(out PointerType? pointerType))
        {
            Diagnostics.Add(Diagnostic.Internal("What", statement));
            successful = false;
            return;
        }

        switch (pointerType.To.FinalValue)
        {
            case ArrayType arrayType:
            {
                if (!ToType(arrayType.Of, out Type? type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement));
                    successful = false;
                    return;
                }

                if (arrayType.Length is null)
                {
                    Diagnostics.Add(Diagnostic.Internal($"The array's length must be specified", statement));
                    successful = false;
                    return;
                }

                if (!arrayType.Length.Type.SameAs(BuiltinType.I32))
                {
                    Diagnostics.Add(Diagnostic.Internal($"The array's length must be an i32 and not {arrayType.Length.Type}", statement));
                    successful = false;
                    return;
                }

                EmitStatement(arrayType.Length, il, ref successful);
                il.Emit(OpCodes.Newarr, type);
                EmitValue(0, il);
                il.Emit(OpCodes.Ldelema, type);
                return;
            }

            case StructType structType:
            {
                if (!ToType(structType, out Type? type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement));
                    successful = false;
                    return;
                }

                ConstructorInfo? constructor = type.GetConstructor(Array.Empty<Type>());

                if (constructor is null)
                {
                    Diagnostics.Add(Diagnostic.Internal($"Type \"{type}\" doesn't have a parameterless constructor", statement));
                    successful = false;
                    return;
                }

                il.Emit(OpCodes.Newobj, constructor);
                il.Emit(OpCodes.Box, type);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Initobj, type);
                return;
            }

            case BuiltinType builtinType:
            {
                if (!EmitDefaultValue(builtinType, il, out PossibleDiagnostic? defaultValueError))
                {
                    Diagnostics.Add(defaultValueError.ToError(statement));
                    successful = false;
                    return;
                }

                if (!ToType(builtinType, out Type? type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement));
                    successful = false;
                    return;
                }

                il.Emit(OpCodes.Box, type);
                return;
            }

            default:
                throw new NotImplementedException();

        }
    }
    void EmitStatement(CompiledFakeTypeCast statement, ILProxy il, ref bool successful)
    {
        if (statement.Type.Is(out PointerType? resultPointerType) &&
            statement.Value is CompiledFunctionCall allocatorCaller &&
            allocatorCaller.Function is CompiledFunctionDefinition allocatorCallee &&
            allocatorCallee.BuiltinFunctionName == "alloc" &&
            allocatorCaller.Arguments.Length == 1)
        {
            if (allocatorCaller.Arguments[0].Value is CompiledSizeof sizeofArgument)
            {
                if (sizeofArgument.Of.Is(out ArrayType? sizeofArrayType))
                {
                    if (!resultPointerType.To.Is(out ArrayType? resultArrayType) ||
                        !resultArrayType.Of.SameAs(sizeofArrayType.Of))
                    {
                        Diagnostics.Add(Diagnostic.Internal($"Invalid heap allocation types", sizeofArgument));
                        successful = false;
                        return;
                    }

                    EmitStatement(new CompiledHeapAllocation()
                    {
                        Allocator = allocatorCallee,
                        Location = statement.Location,
                        SaveValue = statement.SaveValue,
                        Type = resultPointerType,
                    }, il, ref successful);
                }
                else
                {
                    if (!resultPointerType.To.SameAs(sizeofArgument.Of))
                    {
                        Diagnostics.Add(Diagnostic.Internal($"Invalid heap allocation types", sizeofArgument));
                        successful = false;
                        return;
                    }

                    EmitStatement(new CompiledHeapAllocation()
                    {
                        Allocator = allocatorCallee,
                        Location = statement.Location,
                        SaveValue = statement.SaveValue,
                        Type = resultPointerType,
                    }, il, ref successful);
                }
                return;
            }
            else if (allocatorCaller.Arguments[0].Value is CompiledEvaluatedValue valueArgument)
            {
                if (!FindSize(resultPointerType.To, out int size, out PossibleDiagnostic? sizeError))
                {
                    Diagnostics.Add(sizeError.ToError(statement));
                    successful = false;
                    return;
                }

                if (!valueArgument.Type.SameAs(BuiltinType.I32))
                {
                    Diagnostics.Add(Diagnostic.Internal($"Invalid heap allocation sizes", statement));
                    successful = false;
                    return;
                }

                if (valueArgument.Value.I32 != size)
                {
                    Diagnostics.Add(Diagnostic.Internal($"Invalid heap allocation sizes", statement));
                    successful = false;
                    return;
                }

                EmitStatement(new CompiledHeapAllocation()
                {
                    Allocator = allocatorCallee,
                    Location = statement.Location,
                    SaveValue = statement.SaveValue,
                    Type = resultPointerType,
                }, il, ref successful);
                return;
            }
            else
            {
                Diagnostics.Add(Diagnostic.Error($"Unrecognised allocation", allocatorCaller, successful));
                successful = false;
                return;
            }
        }

        if (statement.Type.SameAs(BuiltinType.I32) &&
            statement.Value.Type.SameAs(BuiltinType.F32))
        {
            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Call, ((Func<float, int>)BitConverter.SingleToInt32Bits).Method);
            if (!statement.SaveValue) il.Emit(OpCodes.Pop);
            return;
        }

#if !NETSTANDARD
        if (statement.Type.SameAs(BuiltinType.U32) &&
            statement.Value.Type.SameAs(BuiltinType.F32))
        {
            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Call, ((Func<float, uint>)BitConverter.SingleToUInt32Bits).Method);
            if (!statement.SaveValue) il.Emit(OpCodes.Pop);
            return;
        }
#endif

        if (statement.Type.SameAs(BuiltinType.F32) &&
            statement.Value.Type.SameAs(BuiltinType.I32))
        {
            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Call, ((Func<int, float>)BitConverter.Int32BitsToSingle).Method);
            if (!statement.SaveValue) il.Emit(OpCodes.Pop);
            return;
        }

        if (statement.Value.Type.Is(out PointerType? fromTypeP) &&
            statement.Type.Is(out PointerType? toTypeP))
        {
            if (ToType(fromTypeP, out Type? fromTypePT, out _) &&
                ToType(toTypeP, out Type? toTypePT, out _) &&
                IsUnmanaged(fromTypePT) &&
                IsUnmanaged(toTypePT))
            {
                EmitStatement(statement.Value, il, ref successful);
                if (!fromTypePT.Equals(toTypeP))
                {
                    Diagnostics.Add(Diagnostic.Warning($"Be careful! (casting {statement.Value.Type.FinalValue} to {statement.Type.FinalValue})", statement));
                }
                return;
            }
        }

        if (statement.Value is CompiledEvaluatedValue fromValue &&
            statement.Type.Is(out toTypeP))
        {
            if (CompiledValue.IsZero(fromValue.Value))
            {
                il.Emit(OpCodes.Ldnull);
                return;
            }

            if (ToType(toTypeP, out Type? toTypePT, out _) &&
                IsUnmanaged(toTypePT))
            {
                EmitStatement(statement.Value, il, ref successful);
                il.Emit(OpCodes.Ldnull);
                Diagnostics.Add(Diagnostic.Warning($"Be careful! (casting {statement.Value.Type} to {statement.Type})", statement));
                return;
            }
        }

        if (statement.Value.Type.SameAs(BuiltinType.I32) &&
            statement.Type.Is(out toTypeP))
        {
            if (ToType(toTypeP, out Type? toTypePT, out _) &&
                IsUnmanaged(toTypePT))
            {
                EmitStatement(statement.Value, il, ref successful);
                il.Emit(OpCodes.Conv_I);
                Diagnostics.Add(Diagnostic.Warning($"Be careful! (casting {statement.Value.Type} to {statement.Type})", statement));
                return;
            }
        }

        if (statement.Value.Type.SameAs(statement.Type))
        {
            EmitStatement(statement.Value, il, ref successful);
            return;
        }

        if (statement.Value.Type.Is(out BuiltinType? fromTypeB) &&
            statement.Type.Is(out BuiltinType? toTypeB) &&
            fromTypeB.GetBitWidth(this, out BitWidth fromTypeBW, out _) &&
            toTypeB.GetBitWidth(this, out BitWidth toTypeBW, out _) &&
            fromTypeBW == toTypeBW)
        {
            EmitStatement(statement.Value, il, ref successful);
            return;
        }

        EmitStatement(statement.Value, il, ref successful);
        Diagnostics.Add(Diagnostic.Internal($"Fake type casts are unsafe (tried to cast {statement.Value.Type} to {statement.Type})", statement, successful));
        successful = false;
    }
    void EmitStatement(CompiledTypeCast statement, ILProxy il, ref bool successful)
    {
        EmitStatement(statement.Value, il, ref successful);
        switch (statement.Type.FinalValue)
        {
            case BuiltinType v:
                switch (v.Type)
                {
                    case BasicType.U8: il.Emit(OpCodes.Conv_U1); break;
                    case BasicType.I8: il.Emit(OpCodes.Conv_I1); break;
                    case BasicType.U16: il.Emit(OpCodes.Conv_U2); break;
                    case BasicType.I16: il.Emit(OpCodes.Conv_I2); break;
                    case BasicType.U32: il.Emit(OpCodes.Conv_U4); break;
                    case BasicType.I32: il.Emit(OpCodes.Conv_I4); break;
                    case BasicType.U64: il.Emit(OpCodes.Conv_U8); break;
                    case BasicType.I64: il.Emit(OpCodes.Conv_I8); break;
                    case BasicType.F32: il.Emit(OpCodes.Conv_R4); break;
                    default:
                        successful = false;
                        Diagnostics.Add(Diagnostic.Critical($"Invalid casting type {v.Type}", statement));
                        break;
                }
                return;
            default:
                successful = false;
                Diagnostics.Add(Diagnostic.Critical($"Unimplemented casting type {statement.Type.FinalValue}", statement));
                break;
        }
    }
    void EmitStatement(CompiledCrash statement, ILProxy il, ref bool successful)
    {
        if (!Settings.AllowCrash)
        {
            Diagnostics.Add(Diagnostic.Critical($"Crashing is banned by the generator settings", statement, false));
            successful = false;
            return;
        }

        switch (statement.Value)
        {
            case CompiledStringInstance compiledStringInstance:
                il.Emit(OpCodes.Ldstr, compiledStringInstance.Value);
                il.Emit(OpCodes.Newobj, typeof(RuntimeException).GetConstructor(new Type[] { typeof(string) })!);
                il.Emit(OpCodes.Throw);
                break;
            case CompiledStackStringInstance stackStringInstance:
                il.Emit(OpCodes.Ldstr, stackStringInstance.Value);
                il.Emit(OpCodes.Newobj, typeof(RuntimeException).GetConstructor(new Type[] { typeof(string) })!);
                il.Emit(OpCodes.Throw);
                break;
            case CompiledEvaluatedValue compiledEvaluatedValue:
                il.Emit(OpCodes.Ldstr, compiledEvaluatedValue.Value.ToStringValue() ?? string.Empty);
                il.Emit(OpCodes.Newobj, typeof(RuntimeException).GetConstructor(new Type[] { typeof(string) })!);
                il.Emit(OpCodes.Throw);
                break;
            default:
                Diagnostics.Add(Diagnostic.Internal($"Unimplemented value for crash reason", statement.Value));
                successful = false;
                break;
        }
    }
    void EmitStatement(CompiledSizeof statement, ILProxy il, ref bool successful)
    {
        if (!FindSize(statement.Of, out int size, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(statement));
            successful = false;
            return;
        }

        EmitValue(size, il);
    }
    void EmitStatement(CompiledDelete statement, ILProxy il, ref bool successful)
    {
        EmitStatement(statement.Value, il, ref successful);
        GenerateDestructor(statement.Cleanup, il, ref successful);
        il.Emit(OpCodes.Pop);
    }
    void EmitStatement(CompiledConstructorCall statement, ILProxy il, ref bool successful, LocalBuilder? destination = null)
    {
        if (!EmitFunction(statement.Function, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to emit function {statement.Function}", statement));
            successful = false;
            return;
        }

        if (statement.Object is CompiledStackAllocation stackAllocation)
        {
            if (destination is null)
            {
                if (!ToType(statement.Object.Type, out Type? type, out PossibleDiagnostic? error))
                {
                    Diagnostics.Add(error.ToError(statement.Object));
                    successful = false;
                    return;
                }

                destination = il.DeclareLocal(type);
                EmitStatement(statement, il, ref successful, destination);
                LoadLocal(il, destination.LocalIndex);
                return;
            }

            EmitStatement(stackAllocation, il, ref successful, destination);

            il.Emit(OpCodes.Ldloca_S, destination.LocalIndex);
            for (int i = 0; i < statement.Arguments.Length; i++)
            {
                EmitStatement(statement.Arguments[i].Value, il, ref successful);
            }

            il.Emit(OpCodes.Call, function);

            if (!statement.SaveValue &&
                function.ReturnType != typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }
        }
        else
        {
            if (!statement.Object.Type.Is<PointerType>())
            {
                Diagnostics.Add(Diagnostic.Internal($"This should be a pointer", statement.Object));
                successful = false;
                return;
            }

            EmitStatement(statement.Object, il, ref successful);

            destination ??= il.DeclareLocal(typeof(nint));
            StoreLocal(il, destination.LocalIndex);

            LoadLocal(il, destination.LocalIndex);
            for (int i = 0; i < statement.Arguments.Length; i++)
            {
                EmitStatement(statement.Arguments[i].Value, il, ref successful);
            }

            il.Emit(OpCodes.Call, function);

            if (function.ReturnType != typeof(void))
            {
                il.Emit(OpCodes.Pop); // What???
            }

            LoadLocal(il, destination.LocalIndex);
        }
    }
    void EmitStatement(CompiledStackAllocation statement, ILProxy il, ref bool successful, LocalBuilder? destination = null)
    {
        switch (statement.Type)
        {
            case StructType v:
            {
                if (!ToType(v, out Type? type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement));
                    successful = false;
                    return;
                }

                if (destination is not null)
                {
                    il.Emit(OpCodes.Ldloca_S, destination.LocalIndex);
                    il.Emit(OpCodes.Initobj, type);
                }
                else
                {
                    LocalBuilder local = il.DeclareLocal(type);
                    il.Emit(OpCodes.Ldloca_S, local.LocalIndex);
                    il.Emit(OpCodes.Initobj, type);
                    LoadLocal(il, local.LocalIndex);
                }
                break;
            }
            default:
            {
                Diagnostics.Add(Diagnostic.Internal($"Only structs supported for stack allocations", statement));
                successful = false;
                break;
            }
        }
    }
    void EmitStatement(CompiledStringInstance statement, ILProxy il, ref bool successful)
    {
        if (!Settings.AllowHeap)
        {
            Diagnostics.Add(Diagnostic.Critical($"Heap is banned by the generator settings", statement, false));
            successful = false;
            return;
        }

        Type elementType = statement.IsASCII ? typeof(byte) : typeof(char);
        int elementSize = statement.IsASCII ? sizeof(byte) : sizeof(char);

        EmitValue(statement.Value.Length + 1, il);
        il.Emit(OpCodes.Newarr, elementType);
        EmitValue(0, il);
        il.Emit(OpCodes.Ldelema, elementType);

        for (int i = 0; i < statement.Value.Length; i++)
        {
            il.Emit(OpCodes.Dup);
            if (i != 0)
            {
                EmitValue(i * elementSize, il);
                il.Emit(OpCodes.Add);
            }
            EmitValue(statement.Value[i], il);
            if (!StoreIndirect(elementType, il, out PossibleDiagnostic? storeIndirectError))
            {
                Diagnostics.Add(storeIndirectError.ToError(statement));
                successful = false;
                return;
            }
        }
    }
    void EmitStatement(CompiledIndexGetter statement, ILProxy il, ref bool successful)
    {
        if (statement.Base.Type.Is(out PointerType? basePointerType) &&
            basePointerType.To.Is(out ArrayType? baseArrayType))
        {
            if (!ToType(baseArrayType.Of, out _, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement.Base));
                successful = false;
                return;
            }

            EmitStatement(statement.Base, il, ref successful);

            if (statement.Index is CompiledEvaluatedValue evaluatedIndex && evaluatedIndex.Value == 0)
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Index 0", statement.Base));
            }
            else
            {
                EmitStatement(statement.Index, il, ref successful);

                if (!FindSize(baseArrayType.Of, out int elementSize, out typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement.Base));
                    successful = false;
                    return;
                }

                if (elementSize != 1)
                {
                    EmitValue(elementSize, il);

                    il.Emit(OpCodes.Mul);
                    il.Emit(OpCodes.Add);
                }
                else
                {
                    Diagnostics.Add(Diagnostic.OptimizationNotice($"Element size is 1 byte 😀", statement.Base));
                    il.Emit(OpCodes.Add);
                }
            }

            if (!LoadIndirect(baseArrayType.Of, il, out PossibleDiagnostic? loadIndirectError))
            {
                Diagnostics.Add(loadIndirectError.ToError(statement));
                successful = false;
                return;
            }

            return;
        }

        if (statement.Base.Type.Is(out baseArrayType))
        {
            EmitInlineArrayElementRef(statement.Base, statement.Index, il, ref successful);

            if (!LoadIndirect(baseArrayType.Of, il, out PossibleDiagnostic? loadIndirectError))
            {
                Diagnostics.Add(loadIndirectError.ToError(statement));
                successful = false;
                return;
            }

            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"This should be an array", statement.Base));
        successful = false;
    }
    void EmitStatement(CompiledIndexSetter statement, ILProxy il, ref bool successful)
    {
        if (statement.Base.Type.Is(out PointerType? basePointerType) &&
            basePointerType.To.Is(out ArrayType? baseArrayType))
        {
            if (!ToType(baseArrayType.Of, out _, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement.Base));
                successful = false;
                return;
            }

            EmitStatement(statement.Base, il, ref successful);

            EmitStatement(statement.Index, il, ref successful);

            if (!FindSize(baseArrayType.Of, out int elementSize, out typeError))
            {
                Diagnostics.Add(typeError.ToError(statement.Base));
                successful = false;
                return;
            }
            if (elementSize != 1)
            {
                EmitValue(elementSize, il);

                il.Emit(OpCodes.Mul);
                il.Emit(OpCodes.Add);
            }
            else
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Element size is 1 byte 😀", statement.Base));
                il.Emit(OpCodes.Add);
            }

            EmitStatement(statement.Value, il, ref successful);

            if (!StoreIndirect(baseArrayType.Of, il, out PossibleDiagnostic? loadIndirectError))
            {
                Diagnostics.Add(loadIndirectError.ToError(statement));
                successful = false;
                return;
            }

            return;
        }

        if (statement.Base.Type.Is(out baseArrayType))
        {
            EmitInlineArrayElementRef(statement.Base, statement.Index, il, ref successful);

            EmitStatement(statement.Value, il, ref successful);

            if (!StoreIndirect(baseArrayType.Of, il, out PossibleDiagnostic? storeIndirectError))
            {
                Diagnostics.Add(storeIndirectError.ToError(statement));
                successful = false;
                return;
            }

            return;
        }

        Debugger.Break();
        Diagnostics.Add(Diagnostic.Critical($"Unimplemented index setter {statement.Base.Type}[{statement.Index.Type}]", statement));
        successful = false;
    }
    void EmitStatement(FunctionAddressGetter statement, ILProxy il, ref bool successful)
    {
        if (!Settings.AllowPointers)
        {
            Diagnostics.Add(Diagnostic.Critical($"Pointers are banned by the generator settings", statement, false));
            successful = false;
            return;
        }

        //var function = GetOrEmitFunctionSignature(statement.Function);
        //il.Emit(OpCodes.Ldftn, function);

        Diagnostics.Add(Diagnostic.Critical($"Function address getters not supported", statement, successful));
        successful = false;
    }
    void EmitStatement(CompiledRuntimeCall statement, ILProxy il, ref bool successful)
    {
        for (int i = 0; i < statement.Arguments.Length; i++)
        {
            EmitStatement(statement.Arguments[i].Value, il, ref successful);
        }

        EmitStatement(statement.Function, il, ref successful);

        il.Emit(OpCodes.Calli);

        if (!statement.SaveValue && !statement.Function.Type.SameAs(BuiltinType.Void))
        {
            il.Emit(OpCodes.Pop);
        }
    }
    void EmitStatement(CompiledGoto statement, ILProxy il, ref bool successful)
    {
        if (statement.Value is InstructionLabelAddressGetter instructionLabelAddressGetter)
        {
            if (!EmittedLabels.TryGetValue(instructionLabelAddressGetter.InstructionLabel, out Label label))
            {
                label = EmittedLabels[instructionLabelAddressGetter.InstructionLabel] = il.DefineLabel();
            }
            il.Emit(OpCodes.Jmp, label);
            return;
        }
        else
        {
            successful = false;
        }
    }
    void EmitStatement(CompiledInstructionLabelDeclaration statement, ILProxy il, ref bool successful)
    {
        if (!EmittedLabels.TryGetValue(statement, out Label label))
        {
            return;
        }
        il.MarkLabel(label);
    }
    void EmitStatement(RegisterGetter statement, ILProxy il, ref bool successful)
    {
        successful = false;
        Diagnostics.Add(Diagnostic.Critical($"Direct register access isn't supported in MSIL", statement, false));
    }
    void EmitStatement(RegisterSetter statement, ILProxy il, ref bool successful)
    {
        successful = false;
        Diagnostics.Add(Diagnostic.Critical($"Direct register access isn't supported in MSIL", statement, false));
    }
    void EmitStatement(InstructionLabelAddressGetter statement, ILProxy il, ref bool successful)
    {
        successful = false;
        Diagnostics.Add(Diagnostic.Critical($"This isn't supported in MSIL", statement, false));
    }
    void EmitStatement(CompiledLiteralList statement, ILProxy il, ref bool successful)
    {
        successful = false;
        Diagnostics.Add(Diagnostic.Critical($"no", statement));
    }
    void EmitStatement(CompilerVariableGetter statement, ILProxy il, ref bool successful)
    {
        if (statement.Identifier == "IL")
        {
            EmitValue(1, il);
        }
        else
        {
            EmitValue(0, il);
        }
    }
    void EmitStatement(CompiledStatement statement, ILProxy il, ref bool successful)
    {
        switch (statement)
        {
            case EmptyStatement: break;
            case CompiledReturn v: EmitStatement(v, il, ref successful); break;
            case CompiledEvaluatedValue v: EmitStatement(v, il, ref successful); break;
            case CompiledBinaryOperatorCall v: EmitStatement(v, il, ref successful); break;
            case CompiledUnaryOperatorCall v: EmitStatement(v, il, ref successful); break;
            case CompiledVariableDeclaration v: EmitStatement(v, il, ref successful); break;
            case CompiledVariableGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledParameterGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledFieldGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledPointer v: EmitStatement(v, il, ref successful); break;
            case CompiledFunctionCall v: EmitStatement(v, il, ref successful); break;
            case CompiledExternalFunctionCall v: EmitStatement(v, il, ref successful); break;
            case CompiledVariableSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledParameterSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledFieldSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledIndirectSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledWhileLoop v: EmitStatement(v, il, ref successful); break;
            case CompiledBlock v: EmitStatement(v, il, ref successful); break;
            case CompiledForLoop v: EmitStatement(v, il, ref successful); break;
            case CompiledIf v: EmitStatement(v, il, ref successful); break;
            case CompiledBreak v: EmitStatement(v, il, ref successful); break;
            case CompiledAddressGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledFakeTypeCast v: EmitStatement(v, il, ref successful); break;
            case CompiledTypeCast v: EmitStatement(v, il, ref successful); break;
            case CompiledCrash v: EmitStatement(v, il, ref successful); break;
            case CompiledStatementWithValueThatActuallyDoesntHaveValue v: EmitStatement(v.Statement, il, ref successful); break;
            case CompiledSizeof v: EmitStatement(v, il, ref successful); break;
            case CompiledDelete v: EmitStatement(v, il, ref successful); break;
            case CompiledStackAllocation v: EmitStatement(v, il, ref successful); break;
            case CompiledConstructorCall v: EmitStatement(v, il, ref successful); break;
            case CompiledStringInstance v: EmitStatement(v, il, ref successful); break;
            case CompiledIndexGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledIndexSetter v: EmitStatement(v, il, ref successful); break;
            case FunctionAddressGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledRuntimeCall v: EmitStatement(v, il, ref successful); break;
            case CompiledGoto v: EmitStatement(v, il, ref successful); break;
            case CompiledInstructionLabelDeclaration v: EmitStatement(v, il, ref successful); break;
            case RegisterGetter v: EmitStatement(v, il, ref successful); break;
            case InstructionLabelAddressGetter v: EmitStatement(v, il, ref successful); break;
            case RegisterSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledLiteralList v: EmitStatement(v, il, ref successful); break;
            case CompilerVariableGetter v: EmitStatement(v, il, ref successful); break;
            default: throw new NotImplementedException(statement.GetType().Name);
        }
    }

    void EmitInlineArrayElementRef(CompiledStatementWithValue @base, CompiledStatementWithValue index, ILProxy il, ref bool successful)
    {
        if (!@base.Type.Is(out ArrayType? baseArrayType))
        {
            successful = false;
            return;
        }

        if (!ToType(baseArrayType, out _, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(@base));
            successful = false;
            return;
        }

        EmitStatement(new CompiledAddressGetter()
        {
            Of = @base,
            Location = @base.Location,
            SaveValue = true,
            Type = new PointerType(@base.Type),
        }, il, ref successful);
        il.Emit(OpCodes.Conv_U);

        EmitStatement(index, il, ref successful);
        il.Emit(OpCodes.Conv_I);

        if (!FindSize(baseArrayType.Of, out int elementSize, out typeError))
        {
            Diagnostics.Add(typeError.ToError(@base));
            successful = false;
            return;
        }
        if (elementSize != 1)
        {
            EmitValue(elementSize, il);

            il.Emit(OpCodes.Mul);
            il.Emit(OpCodes.Add);
        }
        else
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Element size is 1 byte 😀", @base));
            il.Emit(OpCodes.Add);
        }
    }

    bool ToType(ImmutableArray<GeneralType> types, [NotNullWhen(true)] out Type[]? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        result = new Type[types.Length];
        error = null;

        for (int i = 0; i < types.Length; i++)
        {
            if (!ToType(types[i], out result[i]!, out error)) return false;
        }

        return true;
    }
    bool ToType(GeneralType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        AliasType v => ToType(v.Value, out result, out error),
        BuiltinType v => ToType(v, out result, out error),
        StructType v => ToType(v, out result, out error),
        PointerType v => ToType(v, out result, out error),
        ArrayType v => ToType(v, out result, out error),
        FunctionType v => ToType(v, out result, out error),
        _ => throw new UnreachableException(),
    };

    bool ToType(PointerType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        if (false && type.To.Is(out ArrayType? arrayType) && ToType(arrayType.Of, out Type? arrayOf, out _))
        {
            result = arrayOf.MakeArrayType();
            return true;
        }
        else
        {
            result = typeof(nint);
            return true;
        }
    }
    static bool ToType(BuiltinType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        result = type.Type switch
        {
            BasicType.Void => typeof(void),
            BasicType.U8 => typeof(byte),
            BasicType.I8 => typeof(sbyte),
            BasicType.U16 => typeof(ushort),
            BasicType.I16 => typeof(short),
            BasicType.U32 => typeof(uint),
            BasicType.I32 => typeof(int),
            BasicType.F32 => typeof(float),
            BasicType.U64 => typeof(ulong),
            BasicType.I64 => typeof(long),
            BasicType.Any => throw new NotImplementedException(),
            _ => throw new UnreachableException(),
        };
        return true;
    }
    static bool ToType(FunctionType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        result = typeof(nint);
        return true;
    }

    static bool IsUnmanaged(Type t)
    {
        if (t.IsPrimitive || t.IsPointer || t.IsEnum) return true;
        if (t.IsGenericType || !t.IsValueType) return false;
        return t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .All(x => IsUnmanaged(x.FieldType));
    }

    readonly List<(StructType Type, Type Value)> GeneratedStructTypes = new();
    readonly List<(ArrayType Type, Type Value)> GeneratedInlineArrayTypes = new();

    string GenerateTypeId(GeneralType type)
    {
        return MakeUnique(GenerateTypeId_(type));
    }

    string GenerateTypeId_(GeneralType type) => type.FinalValue switch
    {
        ArrayType v => $"{GenerateTypeId_(v.Of)}[{v.ComputedLength?.ToString() ?? ""}]",
        BuiltinType v => v.Type.ToString(),
        FunctionType => $"fnc",
        PointerType => $"ptr",
        StructType v => MakeUnique(v.Struct.Identifier.Content),
        _ => throw new UnreachableException(),
    };

    string GenerateTypeId(StructType type)
    {
        return MakeUnique(type.Struct.Identifier.Content);
    }

    string MakeUnique(string id) => Utils.MakeUnique(id, v => Module.GetType(v) is null);

    bool ToType(StructType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        result = GeneratedStructTypes.FirstOrDefault(v => v.Type.Equals(type) && Utils.SequenceEquals(v.Type.TypeArguments, type.TypeArguments, (a, b) => a.Equals(b))).Value;
        if (result is not null) return true;

        if (!type.AllGenericsDefined())
        {
            error = new PossibleDiagnostic($"Invalid template type", false);
            return false;
        }

        string id = GenerateTypeId(type);

        if (!type.GetSize(this, out int size, out error))
        {
            return false;
        }

        if (!type.GetFields(this, out ImmutableDictionary<CompiledField, int>? fields, out error))
        {
            return false;
        }

        TypeBuilder builder = Module.DefineType(id, TypeAttributes.Public | TypeAttributes.ExplicitLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, typeof(ValueType), PackingSize.Size1, size);

        foreach ((CompiledField field, int offset) in fields)
        {
            GeneralType fieldType = type.ReplaceType(field.Type, out error);
            if (error is not null) return false;
            if (!ToType(fieldType, out Type? fieldType1, out error)) return false;

            FieldBuilder fieldBuilder = builder.DefineField(field.Identifier.Content, fieldType1, FieldAttributes.Public);
            fieldBuilder.SetOffset(offset);
        }

        ConstructorBuilder constructor = builder.DefineConstructor(
            MethodAttributes.Public |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            Array.Empty<Type>());
        ConstructorInfo conObj = typeof(object).GetConstructor(Array.Empty<Type>())!;

        ILProxy il = new(constructor.GetILGenerator(), Builders is not null);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, conObj);
        il.Emit(OpCodes.Ret);

        result = builder.CreateType();

        Builders?.Add(DebugTypeLayout(result, v =>
        {
            v.AppendLine();

            v.AppendLine($"  public {id}()");
            v.AppendLine($"  {{");
            v.AppendIndented("    ", il.ToString());
            v.AppendLine($"  }}");
        }));

        GeneratedStructTypes.Add((type, result));

        int managedSize = SizeOf(result);

        if (!base.FindSize(type, out int expectedSize, out PossibleDiagnostic? findSizeError))
        {
            error = new PossibleDiagnostic($"Couldn't check the generated struct's size.", findSizeError);
            return false;
        }

        if (managedSize != expectedSize)
        {
            error = new PossibleDiagnostic($"Generated struct's ({result}) size ({managedSize}) doesn't match with the expected size ({expectedSize}).");
            return false;
        }

        return true;
    }
    bool ToType(ArrayType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (type.Length is not CompiledEvaluatedValue _length)
        {
            error = new PossibleDiagnostic($"The array's length must be a constant", false);
            result = null;
            return false;
        }

        if (_length.Value.Type == RuntimeType.F32 || _length.Value <= 0)
        {
            error = new PossibleDiagnostic($"Invalid array length");
            result = null;
            return false;
        }

        error = null;

        result = GeneratedInlineArrayTypes.FirstOrDefault(v => v.Type.Equals(type)).Value;
        if (result is not null) return true;

        string id = GenerateTypeId(type);

        if (!FindSize(type.Of, out int elementSize, out error))
        {
            return false;
        }

        TypeBuilder builder = Module.DefineType(id, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, typeof(ValueType), PackingSize.Size1, elementSize * (int)_length.Value);

        ConstructorBuilder constructor = builder.DefineConstructor(
            MethodAttributes.Public |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            Array.Empty<Type>());
        ConstructorInfo conObj = typeof(object).GetConstructor(Array.Empty<Type>())!;

        ILProxy il = new(constructor.GetILGenerator(), Builders is not null);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, conObj);
        il.Emit(OpCodes.Ret);

        result = builder.CreateType();

        Builders?.Add(DebugTypeLayout(result, v =>
        {
            v.AppendLine();
            v.AppendLine($"  public {id}()");
            v.AppendLine($"  {{");
            v.AppendIndented("    ", il.ToString());
            v.AppendLine($"  }}");
        }));

        GeneratedInlineArrayTypes.Add((type, result));

        int size = SizeOf(result);

        if (!base.FindSize(type, out int expectedSize, out PossibleDiagnostic? findSizeError))
        {
            error = new PossibleDiagnostic($"Couldn't check the generated struct's size.", findSizeError);
            return false;
        }

        if (size != expectedSize)
        {
            error = new PossibleDiagnostic($"Generated struct's ({result}) size ({size}) doesn't match with the expected size ({expectedSize}).");
            return false;
        }

        return true;
    }

    DynamicMethod GenerateCodeForTopLevelStatements(ImmutableArray<CompiledStatement> statements, ref bool successful)
    {
        DynamicMethod method = new(
            "top_level_statements",
            typeof(int),
            Array.Empty<Type>(),
            Module);
        ILProxy il = new(method.GetILGenerator(), Builders is not null);

        EmitFunctionBody(statements, BuiltinType.I32, il, ref successful);

        successful = successful && CheckCode(method);
        Builders?.Add(il.ToString());

        return method;
    }

    void EmitFunctionBody(ImmutableArray<CompiledStatement> statements, GeneralType returnType, ILProxy il, ref bool successful)
    {
        ImmutableArray<KeyValuePair<CompiledVariableDeclaration, LocalBuilder>> savedLocals = LocalBuilders.ToImmutableArray();
        ImmutableArray<Label> savedLoopLabels = LoopLabels.ToImmutableArray();

        LocalBuilders.Clear();
        LoopLabels.Clear();

        foreach (CompiledStatement statement in statements)
        {
            EmitStatement(statement, il, ref successful);
            if (statement is CompiledReturn) goto end;
        }

        if (!EmitDefaultValue(returnType, il, out PossibleDiagnostic? defaultValueError))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical(defaultValueError.Message));
            successful = false;
        }

        il.Emit(OpCodes.Ret);

    end:

        LocalBuilders.Clear();
        LoopLabels.Clear();

        LocalBuilders.AddRange(savedLocals);
        LoopLabels.AddRange(savedLoopLabels);
    }

    readonly Stack<ICompiledFunctionDefinition> EmittingFunctionsStack = new();
    bool EmitFunction(ICompiledFunctionDefinition function, [NotNullWhen(true)] out DynamicMethod? dynamicMethod)
    {
        dynamicMethod = null;

        if (FunctionBuilders.TryGetValue(function, out dynamicMethod))
        {
            if (!EmittedFunctions.Contains(function))
            {
                throw new UnreachableException();
            }
            return true;
        }

        if (EmittedFunctions.Contains(function) && !FunctionBuilders.ContainsKey(function))
        {
            throw new UnreachableException();
        }

        if (EmittingFunctionsStack.Any(v => v == function))
        {
            return false;
        }

        using StackAuto<ICompiledFunctionDefinition> _ = EmittingFunctionsStack.PushAuto(function);
        if (EmittingFunctionsStack.Count > 10)
        {
            throw new StackOverflowException();
        }

        CompiledBlock? body = Functions.FirstOrDefault(v => v.Function == function)?.Body;

        if (body is null)
        {
            return false;
        }

        if (!EmitFunctionSignature(function, out dynamicMethod))
        {
            return false;
        }

        FunctionBuilders.Add(function, dynamicMethod);

        if (EmittedFunctions.Contains(function)) return true;

        ILProxy il = new(dynamicMethod.GetILGenerator(), Builders is not null);

        bool successful = true;
        EmitFunctionBody(body.Statements, function is CompiledConstructorDefinition ? BuiltinType.Void : function.Type, il, ref successful);

        successful = successful && CheckCode(dynamicMethod);

        if (!successful)
        {
            FunctionBuilders.Remove(function);
            return false;
        }

        if (Builders is not null)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine($"{dynamicMethod}");
            stringBuilder.AppendLine($"{{");
            stringBuilder.AppendIndented("  ", il.ToString());
            stringBuilder.AppendLine($"}}");
            Builders.Add(stringBuilder.ToString());
        }

        EmittedFunctions.Add(function);
        return true;
    }

    bool EmitFunctionSignature(ICompiledFunctionDefinition function, [NotNullWhen(true)] out DynamicMethod? dynamicMethod)
    {
        dynamicMethod = null;
        if (!ToType(function is CompiledConstructorDefinition ? BuiltinType.Void : function.Type, out Type? returnType, out PossibleDiagnostic? returnTypeError))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical(returnTypeError.Message));
            return false;
        }
        if (!ToType(function.ParameterTypes, out Type[]? parameterTypes, out PossibleDiagnostic? parameterTypesError))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical(parameterTypesError.Message));
            return false;
        }

        string identifier = function switch
        {
            CompiledFunctionDefinition v => v.Identifier.Content,
            CompiledOperatorDefinition v => $"op_{v.Identifier.Content switch
            {
                "+" => "add",
                "-" => "sub",
                "*" => "mul",
                "/" => "div",
                "%" => "mod",
                _ => v.Identifier.Content,
            }}",
            CompiledConstructorDefinition v => $"ctor_{v.Identifier.Content}",
            CompiledGeneralFunctionDefinition v => $"genr_{v.Identifier.Content}",
            _ => throw new UnreachableException(),
        };
        identifier = Utils.MakeUnique(identifier, v => !FunctionBuilders.Any(w => w.Value.Name == v));

        dynamicMethod = new DynamicMethod(
            identifier,
            returnType,
            parameterTypes,
            Module
        );
        return true;
    }

    public static bool ScanForPointer(GeneralType type, Stack<GeneralType>? stack = null)
    {
        stack ??= new();
        if (stack.Any(v => v.Equals(type))) return true;
        using StackAuto<GeneralType> _ = stack.PushAuto(type);

        return type switch
        {
            BuiltinType => false,
            PointerType => true,
            AliasType v => ScanForPointer(v.Value),
            StructType v => v.Struct.Fields.Any(field => ScanForPointer(GeneralType.InsertTypeParameters(field.Type, v.TypeArguments) ?? field.Type)),
            ArrayType v => ScanForPointer(v.Of),
            FunctionType => true,
            GenericType => true,
            _ => throw new UnreachableException(),
        };
    }

    static bool CheckMarshalSafety(GeneralType type, [NotNullWhen(false)] out PossibleDiagnostic? error, Stack<GeneralType>? stack = null)
    {
        error = null;

        stack ??= new();
        if (stack.Any(v => v.Equals(type))) return true;
        using StackAuto<GeneralType> _ = stack.PushAuto(type);

        if (type is GenericType)
        {
            error = new PossibleDiagnostic($"Can't marshal a generic type (like wtf bro what is this fr)");
            return false;
        }

        if (type is FunctionType)
        {
            error = new PossibleDiagnostic($"Can't marshal a function pointer fr");
            return false;
        }

        if (type switch
        {
            BuiltinType => false,
            PointerType v => ScanForPointer(v.To), // Top-level pointers are okay
            AliasType v => ScanForPointer(v.Value),
            StructType v => v.Struct.Fields.Any(field => ScanForPointer(GeneralType.InsertTypeParameters(field.Type, v.TypeArguments) ?? field.Type)),
            ArrayType v => ScanForPointer(v.Of),
            _ => throw new UnreachableException(),
        })
        {
            error = new PossibleDiagnostic($"Can't marshal nested pointers");
            return false;
        }

        return true;
    }

    public unsafe bool GenerateImplMarshaled(CompiledFunction function, [NotNullWhen(true)] out ExternalFunctionScopedSyncCallback? marshaled, out DynamicMethod? raw)
    {
        marshaled = null;
        raw = null;
        if (_marshalCache.TryGetValue(function, out marshaled)) return true;

        if (function.Function is IHaveAttributes attributes &&
            attributes.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function {function.ToReadable()} marked as MSIL incompatible", attributes.Attributes.First(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier), false));
            return false;
        }

        if (!function.Function.IsMsilCompatible)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function {function.ToReadable()} is not MSIL incompatible", (ILocated)function.Function, false));
            return false;
        }

        if (!EmitFunction(function.Function, out DynamicMethod? builder)) return false;
        if (!WrapWithMarshaling(builder, out DynamicMethod? marshaledBuilder)) return false;

        for (int i = 0; i < function.Function.Parameters.Length; i++)
        {
            if (!CheckMarshalSafety(function.Function.ParameterTypes[i], out PossibleDiagnostic? safetyError1))
            {
                Diagnostics.Add(safetyError1.ToWarning(((FunctionThingDefinition)function.Function).Parameters[i].Type));
                return false;
            }
        }
        if (!CheckMarshalSafety(function.Function.Type, out PossibleDiagnostic? safetyError2))
        {
            Diagnostics.Add(safetyError2.ToWarning(function.Function is FunctionDefinition v ? v.Type.Location : new Location(((FunctionThingDefinition)function.Function).Identifier.Position, function.Function.File)));
            return false;
        }

        marshaled = (ExternalFunctionScopedSyncCallback)marshaledBuilder.CreateDelegate(typeof(ExternalFunctionScopedSyncCallback));
        raw = builder;
        _marshalCache.Add(function, marshaled);

        return true;
    }

    static int GetManagedSize(Type type)
    {
        DynamicMethod method = new("GetManagedSizeImpl", typeof(uint), Array.Empty<Type>(), false);
        ILProxy gen = new(method.GetILGenerator(), false);
        gen.Emit(OpCodes.Sizeof, type);
        gen.Emit(OpCodes.Ret);
        Func<uint> func = (Func<uint>)method.CreateDelegate(typeof(Func<uint>));
        return checked((int)func());
    }

    static string DebugTypeLayout(Type type, Action<StringBuilder>? additionalStuff = null)
    {
        object? value = Activator.CreateInstance(type);
        StringBuilder result = new();
        result.AppendLine($"{type} : {type.BaseType} {{");
        if (type.StructLayoutAttribute is not null)
        {
            result.AppendLine($"  CharSet: {type.StructLayoutAttribute.CharSet}");
            result.AppendLine($"  Pack: {type.StructLayoutAttribute.Pack}");
            result.AppendLine($"  Size: {GetManagedSize(type)} (defined as {type.StructLayoutAttribute.Size})");
            result.AppendLine($"  Layout: {type.StructLayoutAttribute.Value}");
            result.AppendLine($"");
        }
        else
        {
            result.AppendLine($"  Size: {GetManagedSize(type)}");
            result.AppendLine($"");
        }
        List<(FieldInfo, nint)> fieldAddresses = new();
        nint smallestAddress = default;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            DynamicMethod method = new("DebugTypeLayoutImpl", typeof(nint), new Type[] { typeof(object) }, false);
            ILProxy gen = new(method.GetILGenerator(), false);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldflda, field);
            gen.Emit(OpCodes.Ret);
            nint address = value is null ? default : ((Func<object, nint>)method.CreateDelegate(typeof(Func<object, nint>))).Invoke(value);
            fieldAddresses.Add((field, address));
            if (smallestAddress == default || smallestAddress > address)
            {
                smallestAddress = address;
            }
        }
        fieldAddresses.Sort(static (a, b) => (int)(a.Item2 - b.Item2));
        foreach ((FieldInfo field, nint address) in fieldAddresses)
        {
            result.Append("  ");
            if (value is not null)
            {
                result.Append($"{address - smallestAddress,-3}");
            }
            result.Append($"{field.FieldType} {field.Name}");
            result.AppendLine();
        }
        additionalStuff?.Invoke(result);
        result.AppendLine($"}}");
        return result.ToString();
    }

    readonly Dictionary<CompiledFunction, ExternalFunctionScopedSyncCallback> _marshalCache = new();

    static bool IsPointer(Type type) => type.IsPointer || type == typeof(nint) || type == typeof(nuint);

    enum MarshalDirection
    {
        VmToMsil,
        MsilToVm,
    }

    static Type MarshalType(Type type, MarshalDirection direction)
    {
        switch (direction)
        {
            case MarshalDirection.VmToMsil:
                if (IsPointer(type))
                {
                    return typeof(nint);
                }

                return type;
            case MarshalDirection.MsilToVm:
                if (IsPointer(type))
                {
                    return typeof(int);
                }

                return type;
            default:
                return type;
        }
    }

    static bool EmitValueMarshal(ILProxy il, Type type, MarshalDirection direction, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (direction == MarshalDirection.VmToMsil)
        {
            Type marshaledType = MarshalType(type, MarshalDirection.MsilToVm);
            if (!LoadIndirect(marshaledType, il, out error))
            {
                return false;
            }
        }

        if (IsPointer(type))
        {
            if (direction == MarshalDirection.VmToMsil)
            {
                il.Emit(OpCodes.Conv_I);
            }

            Label zeroLabel = il.DefineLabel();

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brfalse_S, zeroLabel);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(direction switch
            {
                MarshalDirection.VmToMsil => OpCodes.Add,
                MarshalDirection.MsilToVm => OpCodes.Sub,
                _ => throw new UnreachableException(),
            });

            il.MarkLabel(zeroLabel);

            if (direction == MarshalDirection.MsilToVm)
            {
                il.Emit(OpCodes.Conv_I4);
            }
        }

        if (direction == MarshalDirection.MsilToVm)
        {
            Type marshaledType = MarshalType(type, MarshalDirection.MsilToVm);
            if (!StoreIndirect(marshaledType, il, out error))
            {
                return false;
            }
        }

        return true;
    }

    bool WrapWithMarshaling(DynamicMethod method, [NotNullWhen(true)] out DynamicMethod? marshaled)
    {
        marshaled = new(
            $"marshal___{method.Name}",
            typeof(void),
            new Type[] { typeof(nint), typeof(nint), typeof(nint) },
            Module);

        (Type Type, int Size)[] parameters =
            method.GetParameters()
            .Select(v => (v.ParameterType, MarshalType(v.ParameterType, MarshalDirection.MsilToVm)))
            .Select(v => (v.ParameterType, GetManagedSize(v.Item2)))
            .ToArray();
        int parametersSize = parameters.Sum(v => v.Size);

        ILProxy il = new(marshaled.GetILGenerator(), Builders is not null);
        if (method.ReturnType != typeof(void)) il.Emit(OpCodes.Ldarg_2);

        for (int i = 0; i < parameters.Length; i++)
        {
            (Type parameterType, int parameterSize) = parameters[i];

            parametersSize -= parameterSize;

            il.Emit(OpCodes.Ldarg_1);
            if (parametersSize > 0)
            {
                EmitValue(parametersSize, il);
                il.Emit(OpCodes.Conv_I);
                il.Emit(OpCodes.Add);
            }

            if (!EmitValueMarshal(il, parameterType, MarshalDirection.VmToMsil, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(DiagnosticWithoutContext.Error(error1.Message));
                return false;
            }
        }

        il.Emit(OpCodes.Call, method);

        if (method.ReturnType != typeof(void))
        {
            if (!EmitValueMarshal(il, method.ReturnType, MarshalDirection.MsilToVm, out PossibleDiagnostic? error2))
            {
                Diagnostics.Add(DiagnosticWithoutContext.Error(error2.Message));
                return false;
            }
        }

        il.Emit(OpCodes.Ret);

        if (Builders is not null)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine($"{marshaled}");
            stringBuilder.AppendLine($"{{");
            stringBuilder.AppendIndented("  ", il.ToString());
            stringBuilder.AppendLine($"}}");
            Builders.Add(stringBuilder.ToString());
        }

        if (!CheckCode(marshaled))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Error("Failed to generate valid MSIL"));
            return false;
        }

        return true;
    }

    Func<int> GenerateImpl()
    {
        bool successful = true;

        DynamicMethod result = GenerateCodeForTopLevelStatements(TopLevelStatements, ref successful);

        if (!successful && !Diagnostics.HasErrors)
        {
            Diagnostics.Add(DiagnosticWithoutContext.Internal($"Failed to generate valid MSIL"));
        }

        StringBuilder builder = new();
        foreach (Type type in Module.GetTypes()) Stringify(builder, 0, type);
        foreach (DynamicMethod method in FunctionBuilders.Values.Append(result)) Stringify(builder, 0, method);

        return (Func<int>)result.CreateDelegate(typeof(Func<int>));
    }

    public static Func<int> Generate(CompilerResult compilerResult, DiagnosticsCollection diagnostics, ILGeneratorSettings settings, ModuleBuilder? module = null)
        => new CodeGeneratorForIL(compilerResult, diagnostics, settings, module)
        .GenerateImpl();
}
