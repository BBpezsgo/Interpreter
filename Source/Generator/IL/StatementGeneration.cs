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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement));
            successful = false;

            if (!EmittedGlobalVariables.TryGetValue(statement, out FieldInfo? field))
            {
                if (successful)
                { Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Identifier}\" wasn't emitted for some reason", statement)); }
                else
                { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{statement.Identifier}\" wasn't emitted for some reason", statement)); }
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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement));
            successful = false;

            if (!EmittedGlobalVariables.TryGetValue(statement.Variable, out FieldInfo? field))
            {
                if (successful)
                { Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
                else
                { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
                successful = false;
                return;
            }

            il.Emit(OpCodes.Ldsfld, field);
            return;
        }

        if (!LocalBuilders.TryGetValue(statement.Variable, out LocalBuilder? local))
        {
            if (successful)
            { Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
            else
            { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
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
            Diagnostics.Add(Diagnostic.InternalNoBreak($"Failed to emit function \"{statement.Function}\"", statement));
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
                        GlobalContextType_Targets.SetValue(DelegateTargets.ToArray(), null);
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

                Diagnostics.Add(Diagnostic.CriticalNoBreak($"Non-static external functions not supported", statement));
                successful = false;
                return;
            }
            case ExternalFunctionStub:
                Diagnostics.Add(Diagnostic.CriticalNoBreak($"Can't call an external function stub", statement));
                successful = false;
                return;
            default:
                Diagnostics.Add(Diagnostic.CriticalNoBreak($"{statement.Function.GetType()} external functions not supported", statement));
                successful = false;
                return;
        }
    }
    void EmitStatement(CompiledVariableSetter statement, ILProxy il, ref bool successful)
    {
        if (statement.Variable.IsGlobal)
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement));
            successful = false;

            if (!EmittedGlobalVariables.TryGetValue(statement.Variable, out FieldInfo? field))
            {
                if (successful)
                { Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
                else
                { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
                successful = false;
                return;
            }

            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Stsfld, field);
            return;
        }

        if (!LocalBuilders.TryGetValue(statement.Variable, out LocalBuilder? local))
        {
            if (successful)
            { Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
            else
            { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{statement.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Pointers are banned by the generator settings", statement));
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
                Diagnostics.Add(Diagnostic.CriticalNoBreak($"Unsafe!!!", statement));
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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Pointers are banned by the generator settings", statement));
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
        EmitStatement(statement.VariableDeclaration, il, ref successful);

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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Pointers are banned by the generator settings", statement));
            successful = false;
            return;
        }

        switch (statement.Of)
        {
            case CompiledVariableGetter v:
                if (v.Variable.IsGlobal)
                {
                    Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement));
                    successful = false;

                    if (!EmittedGlobalVariables.TryGetValue(v.Variable, out FieldInfo? field))
                    {
                        if (successful)
                        { Diagnostics.Add(Diagnostic.Critical($"Variable \"{v.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
                        else
                        { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{v.Variable.Identifier}\" wasn't emitted for some reason", statement)); }
                        successful = false;
                        return;
                    }

                    il.Emit(OpCodes.Ldsflda, field);
                    return;
                }

                if (!LocalBuilders.TryGetValue(v.Variable, out LocalBuilder? local))
                {
                    if (successful)
                    { Diagnostics.Add(Diagnostic.Critical($"Variable \"{v.Variable.Identifier}\" not compiled", statement)); }
                    else
                    { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{v.Variable.Identifier}\" not compiled", statement)); }
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
                    il.Emit(OpCodes.Ldelema, elementType);

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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Heap is banned by the generator settings", statement));
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
                        Allocator = (CompiledFunctionDefinition)allocatorCaller.Function,
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
                        Allocator = (CompiledFunctionDefinition)allocatorCaller.Function,
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
                    Allocator = (CompiledFunctionDefinition)allocatorCaller.Function,
                    Location = statement.Location,
                    SaveValue = statement.SaveValue,
                    Type = resultPointerType,
                }, il, ref successful);
                return;
            }
            else
            {
                Diagnostics.Add(Diagnostic.ErrorNoBreak($"Unrecognised allocation", allocatorCaller));
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
                Diagnostics.Add(Diagnostic.Warning($"Be careful! (casting {statement.Value.Type} to {statement.Type})", statement));
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
        Diagnostics.Add(Diagnostic.InternalNoBreak($"Fake type casts are unsafe (tried to cast {statement.Value.Type} to {statement.Type})", statement));
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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Crashing is banned by the generator settings", statement));
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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Heap is banned by the generator settings", statement));
            successful = false;
            return;
        }

        if (statement.IsASCII)
        {
            EmitValue(statement.Value.Length + 1, il);
            il.Emit(OpCodes.Newarr, typeof(byte));
            for (int i = 0; i < statement.Value.Length; i++)
            {
                il.Emit(OpCodes.Dup);
                EmitValue(i, il);
                EmitValue((byte)statement.Value[i], il);
                il.Emit(OpCodes.Stelem_I1);
            }
            il.Emit(OpCodes.Dup);
            EmitValue(statement.Value.Length, il);
            EmitValue(0, il);
            il.Emit(OpCodes.Stelem_I1);
        }
        else
        {
            EmitValue(statement.Value.Length + 1, il);
            il.Emit(OpCodes.Newarr, typeof(char));
            for (int i = 0; i < statement.Value.Length; i++)
            {
                il.Emit(OpCodes.Dup);
                EmitValue(i, il);
                EmitValue(statement.Value[i], il);
                il.Emit(OpCodes.Stelem_I2);
            }
            il.Emit(OpCodes.Dup);
            EmitValue(statement.Value.Length, il);
            EmitValue(0, il);
            il.Emit(OpCodes.Stelem_I2);
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
            il.Emit(OpCodes.Conv_U);

            EmitStatement(statement.Index, il, ref successful);
            il.Emit(OpCodes.Conv_I);

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
            il.Emit(OpCodes.Conv_U);

            EmitStatement(statement.Index, il, ref successful);
            il.Emit(OpCodes.Conv_I);

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
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Pointers are banned by the generator settings", statement));
            successful = false;
            return;
        }

        //var function = GetOrEmitFunctionSignature(statement.Function);
        //il.Emit(OpCodes.Ldftn, function);

        Diagnostics.Add(Diagnostic.CriticalNoBreak($"Function address getters not supported", statement));
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

    static bool ToType(PointerType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        result = typeof(nint);
        return true;
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
        StringBuilder res = new();
        GenerateTypeId(type, res);
        return res.ToString();
    }
    void GenerateTypeId(GeneralType type, StringBuilder builder)
    {
        switch (type.FinalValue)
        {
            case BuiltinType v:
                builder.Append(v.Type);
                return;
            case PointerType v:
                builder.Append($"{v.To}*");
                return;
            case FunctionType v:
                builder.Append($"{v.ReturnType}(");
                for (int i = 0; i < v.Parameters.Length; i++)
                {
                    if (i > 0) builder.Append(',');
                    GenerateTypeId(v.Parameters[i], builder);
                }
                builder.Append($")*");
                return;
            case ArrayType v:
                builder.Append($"{v.Of}[{v.ComputedLength?.ToString() ?? ""}]");
                return;
            case StructType v:
                string id = v.Struct.Identifier.Content;
                foreach ((_, GeneralType item) in v.TypeArguments)
                {
                    id += "`" + item;
                }
                id = MakeUnique(id);
                builder.Append(id);
                return;
        }
    }

    static string MakeUnique(string id, Func<string, bool> uniqueChecker)
    {
        if (uniqueChecker.Invoke(id)) return id;

        for (int i = 0; i < 64; i++)
        {
            string idCandidate = $"{id}_{i}";
            if (uniqueChecker.Invoke(idCandidate)) return idCandidate;
            continue;
        }

        throw new InternalExceptionWithoutContext($"Failed to generate unique id for {id}");
    }

    string MakeUnique(string id) => MakeUnique(id, v => Module.GetType(v) is null);

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

        TypeBuilder builder = Module.DefineType(id, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, typeof(ValueType));

        StringBuilder? stringBuilder = Builders is null ? null : new();

        if (stringBuilder is not null)
        {
            stringBuilder.AppendLine($"struct {id}");
            stringBuilder.AppendLine($"{{");
        }

        foreach (CompiledField field in type.Struct.Fields)
        {
            GeneralType fieldType = type.ReplaceType(field.Type, out error);
            if (error is not null) return false;
            if (!ToType(fieldType, out Type? fieldType1, out error)) return false;

            builder.DefineField(field.Identifier.Content, fieldType1, FieldAttributes.Public);
            stringBuilder?.AppendLine($"  public {fieldType1} {field.Identifier.Content};");
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

        if (stringBuilder is not null)
        {
            stringBuilder.AppendLine($"  public {id}()");
            stringBuilder.AppendLine($"  {{");
            stringBuilder.AppendIndented("    ", il.ToString());
            stringBuilder.AppendLine($"  }}");
            stringBuilder.AppendLine($"}}");

            Builders?.Add(stringBuilder.ToString());
        }

        result = builder.CreateType();
        GeneratedStructTypes.Add((type, result));
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

        TypeBuilder builder = Module.DefineType(id, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, typeof(ValueType), elementSize * (int)_length.Value);

        StringBuilder? stringBuilder = Builders is null ? null : new();

        if (stringBuilder is not null)
        {
            stringBuilder.AppendLine($"struct {id}");
            stringBuilder.AppendLine($"{{");
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

        if (stringBuilder is not null)
        {
            stringBuilder.AppendLine($"  public {id}()");
            stringBuilder.AppendLine($"  {{");
            stringBuilder.AppendIndented("    ", il.ToString());
            stringBuilder.AppendLine($"  }}");
            stringBuilder.AppendLine($"}}");

            Builders?.Add(stringBuilder.ToString());
        }

        result = builder.CreateType();
        GeneratedInlineArrayTypes.Add((type, result));
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
        KeyValuePair<CompiledVariableDeclaration, LocalBuilder>[] savedLocals = LocalBuilders.ToArray();
        Label[] savedLoopLabels = LoopLabels.ToArray();

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

        if (EmittedFunctions.Contains(function)) return true;

        ILProxy il = new(dynamicMethod.GetILGenerator(), Builders is not null);

        bool successful = true;
        EmitFunctionBody(body.Statements, function is CompiledConstructorDefinition ? BuiltinType.Void : function.Type, il, ref successful);

        if (Builders is not null)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine($"{dynamicMethod}");
            stringBuilder.AppendLine($"{{");
            stringBuilder.AppendIndented("  ", il.ToString());
            stringBuilder.AppendLine($"}}");
            Builders.Add(stringBuilder.ToString());
        }

        if (!successful)
        {
            FunctionBuilders.Remove(function);
            return false;
        }

        successful = successful && CheckCode(dynamicMethod);
        EmittedFunctions.Add(function);
        return true;
    }

    bool EmitFunctionSignature(ICompiledFunctionDefinition function, [NotNullWhen(true)] out DynamicMethod? dynamicMethod)
    {
        if (FunctionBuilders.TryGetValue(function, out dynamicMethod))
        {
            return true;
        }

        dynamicMethod = null;
        if (!ToType(function is CompiledConstructorDefinition ? BuiltinType.Void : function.Type, out Type? returnType, out PossibleDiagnostic? returnTypeError))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical(returnTypeError.Message));
            return false;
        }
        if (!ToType(function.ParameterTypes.ToImmutableArray(), out Type[]? parameterTypes, out PossibleDiagnostic? parameterTypesError))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical(parameterTypesError.Message));
            return false;
        }

        dynamicMethod = function switch
        {
            CompiledFunctionDefinition v => new DynamicMethod(
                v.Identifier.Content,
                returnType,
                parameterTypes,
                Module),
            CompiledOperatorDefinition v => new DynamicMethod(
                $"op_{v.Identifier.Content}",
                returnType,
                parameterTypes,
                Module),
            CompiledConstructorDefinition v => new DynamicMethod(
                $"ctor_{v.Identifier.Content}",
                returnType,
                parameterTypes,
                Module),
            CompiledGeneralFunctionDefinition v => new DynamicMethod(
                $"genr_{v.Identifier.Content}",
                returnType,
                parameterTypes,
                Module),
            _ => throw new UnreachableException(),
        };
        FunctionBuilders.Add(function, dynamicMethod);
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

    public unsafe bool GenerateImplMarshaled(CompiledFunction function, [NotNullWhen(true)] out ExternalFunctionScopedSyncCallback? marshaled)
    {
        marshaled = null;

        if (function.Function is IHaveAttributes attributes &&
            attributes.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Function {function} marked as MSIL incompatible", attributes.Attributes.First(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier)));
            return false;
        }

        if (!EmitFunction(function.Function, out DynamicMethod? builder)) return false;
        if (!WrapWithMarshaling(builder, out DynamicMethod? marshaledBuilder)) return false;

        for (int i = 0; i < function.Function.Parameters.Count; i++)
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

    readonly Dictionary<object, DynamicMethod> _marshalCache = new();

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
        if (_marshalCache.TryGetValue(method, out marshaled)) return true;

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
        int parametersSize = parameters.Aggregate(0, (a, b) => a + b.Size);

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

        _marshalCache.Add(method, marshaled);
        return true;
    }

    Func<int> GenerateImpl()
    {
        bool successful = true;

        DynamicMethod result = GenerateCodeForTopLevelStatements(TopLevelStatements, ref successful);

        if (!successful && !Diagnostics.HasErrors)
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical($"Failed to generate valid MSIL"));
        }

        //StringBuilder builder = new();
        //foreach (Type type in Module.GetTypes()) Stringify(builder, 0, type);
        //foreach (DynamicMethod method in FunctionBuilders.Values.Append(result)) Stringify(builder, 0, method);
        //Console.WriteLine(builder);

        return (Func<int>)result.CreateDelegate(typeof(Func<int>));
    }

    public static Func<int> Generate(CompilerResult compilerResult, DiagnosticsCollection diagnostics, ILGeneratorSettings settings, ModuleBuilder? module = null)
        => new CodeGeneratorForIL(compilerResult, diagnostics, settings, module)
        .GenerateImpl();
}
