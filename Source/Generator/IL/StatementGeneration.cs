using System.Reflection;
using System.Reflection.Emit;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    readonly Dictionary<CompiledVariableDeclaration, LocalBuilder> Locals = new();
    readonly Dictionary<ICompiledFunction, DynamicMethod> Functions = new();
    readonly Stack<Label> LoopLabels = new();

    ModuleBuilder Module = null!;

    void GenerateDeallocator(CompiledCleanup cleanup, ILGenerator il)
    {
        if (cleanup.Deallocator is null) return;

        if (cleanup.Deallocator.ExternalFunctionName is not null)
        {
            Diagnostics.Add(Diagnostic.Critical($"External deallocator not supported", cleanup));
            return;
        }

        if (cleanup.Deallocator.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Deallocator should not return anything", cleanup));
            return;
        }

        if (!Functions.TryGetValue(cleanup.Deallocator, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Function \"{cleanup.Deallocator}\" wasn't compiled", cleanup));
            return;
        }

        if (function.GetParameters().Length != 1)
        {
            Diagnostics.Add(Diagnostic.Internal($"Invalid deallocator", cleanup));
            return;
        }

        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Call, function);
    }

    void GenerateDestructor(CompiledCleanup cleanup, ILGenerator il)
    {
        GeneralType deallocateableType = cleanup.TrashType;

        if (cleanup.TrashType.Is<PointerType>())
        {
            if (cleanup.Destructor is null)
            {
                GenerateDeallocator(cleanup, il);
                return;
            }
        }
        else
        {
            if (cleanup.Destructor is null)
            { return; }
        }

        if (!Functions.TryGetValue(cleanup.Destructor, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Function \"{cleanup.Destructor}\" wasn't compiled", cleanup));
            return;
        }

        if (function.GetParameters().Length != 1)
        {
            Diagnostics.Add(Diagnostic.Internal($"Invalid destructor", cleanup));
            return;
        }

        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Call, function);

        if (deallocateableType.Is<PointerType>())
        {
            GenerateDeallocator(cleanup, il);
        }
    }

    void EmitStatement(CompiledEvaluatedValue statement, ILGenerator il)
    {
        switch (statement.Value.Type)
        {
            case RuntimeType.U8:
                il.Emit(OpCodes.Ldc_I4_S, statement.Value.U8);
                return;
            case RuntimeType.I8:
                il.Emit(OpCodes.Ldc_I4_S, statement.Value.U8);
                return;
            case RuntimeType.U16:
                // There is no overload for ushort ...
                il.Emit(OpCodes.Ldc_I4, statement.Value.I16);
                return;
            case RuntimeType.I16:
                il.Emit(OpCodes.Ldc_I4, statement.Value.I16);
                return;
            case RuntimeType.I32:
                switch (statement.Value.I32)
                {
                    case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
                    case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                    default: il.Emit(OpCodes.Ldc_I4, statement.Value.I32); break;
                }
                return;
            case RuntimeType.U32:
                switch (statement.Value.U32)
                {
                    case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                    default: il.Emit(OpCodes.Ldc_I4, statement.Value.U32); break;
                }
                return;
            case RuntimeType.F32:
                il.Emit(OpCodes.Ldc_R4, statement.Value.F32);
                return;
            case RuntimeType.Null:
                Diagnostics.Add(Diagnostic.Internal($"Value has type of null", statement));
                return;
        }
    }
    void EmitStatement(CompiledReturn statement, ILGenerator il)
    {
        if (statement.Value is not null)
        {
            EmitStatement(statement.Value, il);
        }
        il.Emit(OpCodes.Ret);
    }
    void EmitStatement(CompiledBinaryOperatorCall statement, ILGenerator il)
    {
        switch (statement.Operator)
        {
            case "+":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Add);
                return;
            }
            case "-":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Sub);
                return;
            }
            case "*":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Mul);
                return;
            }
            case "/":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Div);
                return;
            }
            case "&":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.And);
                return;
            }
            case "|":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Or);
                return;
            }
            case "^":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Xor);
                return;
            }
            case "<<":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Shl);
                return;
            }
            case ">>":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Shr);
                return;
            }
            case "<":
            {
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);

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
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);

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
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);

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
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);

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
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);

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
                EmitStatement(statement.Left, il);
                EmitStatement(statement.Right, il);

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

                EmitStatement(statement.Left, il);
                il.Emit(OpCodes.Brfalse, labelFalse);

                EmitStatement(statement.Right, il);
                il.Emit(OpCodes.Brfalse, labelFalse);

                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelFalse);
                il.Emit(OpCodes.Ldc_I4_0);

                il.MarkLabel(labelEnd);

                return;
            }
        }

        Diagnostics.Add(Diagnostic.Critical($"Unimplemented binary operator {statement.Operator}", statement));
    }
    void EmitStatement(CompiledUnaryOperatorCall statement, ILGenerator il)
    {
        switch (statement.Operator)
        {
            case "!":
            {
                Label labelFalse = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                EmitStatement(statement.Left, il);
                il.Emit(OpCodes.Brfalse, labelFalse);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelFalse);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
        }

        Diagnostics.Add(Diagnostic.Critical($"Unimplemented unary operator {statement.Operator}", statement));
    }
    void EmitStatement(CompiledVariableDeclaration statement, ILGenerator il)
    {
        if (Locals.ContainsKey(statement)) return;
        LocalBuilder local = Locals[statement] = il.DeclareLocal(ToType(statement.Type));
        if (statement.InitialValue is not null)
        {
            if (statement.InitialValue is CompiledConstructorCall constructorCall)
            {
                EmitStatement(constructorCall, il, local);
            }
            else if (statement.InitialValue is CompiledStackAllocation compiledStackAllocation)
            {
                EmitStatement(compiledStackAllocation, il, local);
            }
            else
            {
                EmitStatement(statement.InitialValue, il);
                il.Emit(OpCodes.Stloc, local.LocalIndex);
            }
        }
    }
    void EmitStatement(CompiledVariableGetter statement, ILGenerator il)
    {
        il.Emit(OpCodes.Ldloc, Locals[statement.Variable].LocalIndex);
    }
    void EmitStatement(CompiledParameterGetter statement, ILGenerator il)
    {
        il.Emit(OpCodes.Ldarg, statement.Variable.Index);
    }
    void EmitStatement(CompiledFieldGetter statement, ILGenerator il)
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

        EmitStatement(_object, il);

        while (objectType.To.Is(out PointerType? indirectPointer))
        {
            il.Emit(OpCodes.Ldind_Ref);
            objectType = indirectPointer;
        }

        if (objectType.To.Is(out StructType? structType))
        {
            Type type = ToType(structType);
            FieldInfo? field = type.GetField(statement.Field.Identifier.Content);
            if (field is null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Field \"{statement.Field.Identifier.Content}\" not found in type {type}", _object));
                return;
            }
            il.Emit(OpCodes.Ldfld, field);
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a struct", statement.Object));
        }
    }
    void EmitStatement(CompiledFunctionCall statement, ILGenerator il)
    {
        if (!Functions.TryGetValue(statement.Function, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Function \"{statement.Function}\" wasn't compiled", statement));
            return;
        }

        for (int i = 0; i < statement.Arguments.Length; i++)
        {
            EmitStatement(statement.Arguments[i].Value, il);
        }

        il.Emit(OpCodes.Call, function);

        if (!statement.SaveValue && function.ReturnType != typeof(void))
        {
            il.Emit(OpCodes.Pop);
        }
    }
    void EmitStatement(CompiledExternalFunctionCall statement, ILGenerator il)
    {
        switch (statement.Function)
        {
            case ExternalFunctionSync f:
            {
                if (f.UnmarshaledCallback.Method.IsStatic)
                {
                    foreach (CompiledPassedArgument item in statement.Arguments)
                    {
                        EmitStatement(item.Value, il);
                    }
                    il.Emit(OpCodes.Call, f.UnmarshaledCallback.GetMethodInfo());

                    return;
                }

                Diagnostics.Add(Diagnostic.Critical($"Non-static external functions not supported", statement));
                break;
            }

            default:
                Diagnostics.Add(Diagnostic.Critical($"{statement.Function.GetType()} external functions not supported", statement));
                break;
        }
    }
    void EmitStatement(CompiledVariableSetter statement, ILGenerator il)
    {
        EmitStatement(statement.Value, il);
        il.Emit(OpCodes.Stloc, Locals[statement.Variable].LocalIndex);
    }
    void EmitStatement(CompiledParameterSetter statement, ILGenerator il)
    {
        EmitStatement(statement.Value, il);
        il.Emit(OpCodes.Starg, statement.Variable.Index);
    }
    void EmitStatement(CompiledFieldSetter statement, ILGenerator il)
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

        EmitStatement(_object, il);

        while (objectType.To.Is(out PointerType? indirectPointer))
        {
            il.Emit(OpCodes.Ldind_Ref);
            objectType = indirectPointer;
        }

        if (objectType.To.Is(out StructType? structType))
        {
            Type type = ToType(structType);
            FieldInfo? field = type.GetField(statement.Field.Identifier.Content);
            if (field is null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Field \"{statement.Field.Identifier.Content}\" not found in type {type}", _object));
                return;
            }

            EmitStatement(statement.Value, il);
            il.Emit(OpCodes.Stfld, field);
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a struct", statement.Object));
        }
    }
    void EmitStatement(CompiledIndirectSetter statement, ILGenerator il)
    {
        switch (statement.AddressValue)
        {
            case CompiledVariableGetter:
            case CompiledParameterGetter:
            case CompiledFieldGetter:
                break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unsafe!!!", statement));
                return;
        }

        EmitStatement(statement.AddressValue, il);
        EmitStatement(statement.Value, il);
        if (!statement.AddressValue.Type.Is(out PointerType? pointer))
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a pointer", statement.AddressValue));
            return;
        }

        switch (pointer.To.FinalValue)
        {
            case BuiltinType v:
                switch (v.Type)
                {
                    case BasicType.U8: il.Emit(OpCodes.Stind_I1); break;
                    case BasicType.I8: il.Emit(OpCodes.Stind_I1); break;
                    case BasicType.U16: il.Emit(OpCodes.Stind_I2); break;
                    case BasicType.I16: il.Emit(OpCodes.Stind_I2); break;
                    case BasicType.U32: il.Emit(OpCodes.Stind_I4); break;
                    case BasicType.I32: il.Emit(OpCodes.Stind_I4); break;
                    case BasicType.U64: il.Emit(OpCodes.Stind_I8); break;
                    case BasicType.I64: il.Emit(OpCodes.Stind_I8); break;
                    case BasicType.F32: il.Emit(OpCodes.Stind_R4); break;
                    default:
                        Diagnostics.Add(Diagnostic.Critical($"Unimplemented indirect setter for type {v.Type}", statement));
                        break;
                }
                break;
            case StructType v:
                il.Emit(OpCodes.Stobj, ToType(v));
                break;
            case PointerType:
                il.Emit(OpCodes.Stind_Ref);
                break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unimplemented indirect setter for type {pointer.To.FinalValue}", statement));
                break;
        }
    }
    void EmitStatement(CompiledPointer statement, ILGenerator il)
    {
        switch (statement.To)
        {
            case CompiledVariableGetter:
            case CompiledParameterGetter:
            case CompiledFieldGetter:
                break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unsafe!!!", statement));
                return;
        }

        EmitStatement(statement.To, il);

        if (!statement.To.Type.Is(out PointerType? pointer))
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a pointer", statement.To));
            return;
        }

        switch (pointer.To.FinalValue)
        {
            case BuiltinType v:
                switch (v.Type)
                {
                    case BasicType.U8: il.Emit(OpCodes.Ldind_I1); break;
                    case BasicType.I8: il.Emit(OpCodes.Ldind_I1); break;
                    case BasicType.U16: il.Emit(OpCodes.Ldind_I2); break;
                    case BasicType.I16: il.Emit(OpCodes.Ldind_I2); break;
                    case BasicType.U32: il.Emit(OpCodes.Ldind_I4); break;
                    case BasicType.I32: il.Emit(OpCodes.Ldind_I4); break;
                    case BasicType.U64: il.Emit(OpCodes.Ldind_I8); break;
                    case BasicType.I64: il.Emit(OpCodes.Ldind_I8); break;
                    case BasicType.F32: il.Emit(OpCodes.Ldind_R4); break;
                    default:
                        Diagnostics.Add(Diagnostic.Critical($"Unimplemented dereference for type {v.Type}", statement));
                        break;
                }
                break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unimplemented dereference for type {pointer.To.FinalValue}", statement));
                break;
        }
    }
    void EmitStatement(CompiledWhileLoop statement, ILGenerator il)
    {
        Label loopStart = il.DefineLabel();
        Label loopEnd = il.DefineLabel();

        LoopLabels.Push(loopEnd);

        il.MarkLabel(loopStart);
        EmitStatement(statement.Condition, il);
        il.Emit(OpCodes.Brfalse, loopEnd);

        EmitStatement(statement.Body, il);

        il.Emit(OpCodes.Br, loopStart);
        il.MarkLabel(loopEnd);

        if (LoopLabels.Pop() != loopEnd)
        {
            Diagnostics.Add(Diagnostic.Internal($"Something went wrong ...", statement));
        }
    }
    void EmitStatement(CompiledForLoop statement, ILGenerator il)
    {
        EmitStatement(statement.VariableDeclaration, il);

        Label loopStart = il.DefineLabel();
        Label loopEnd = il.DefineLabel();

        LoopLabels.Push(loopEnd);

        il.MarkLabel(loopStart);
        EmitStatement(statement.Condition, il);
        il.Emit(OpCodes.Brfalse, loopEnd);

        EmitStatement(statement.Body, il);
        EmitStatement(statement.Expression, il);
        il.Emit(OpCodes.Br, loopStart);

        il.MarkLabel(loopEnd);

        if (LoopLabels.Pop() != loopEnd)
        {
            Diagnostics.Add(Diagnostic.Internal($"Something went wrong ...", statement));
        }
    }
    void EmitStatement(CompiledIf statement, ILGenerator il)
    {
        Label labelEnd = il.DefineLabel();

        CompiledBranch? current = statement;
        while (current is not null)
        {
            switch (current)
            {
                case CompiledIf _if:
                    Label labelNext = il.DefineLabel();

                    EmitStatement(_if.Condition, il);
                    il.Emit(OpCodes.Brfalse, labelNext);

                    EmitStatement(_if.Body, il);
                    il.Emit(OpCodes.Br, labelEnd);

                    il.MarkLabel(labelNext);

                    current = _if.Next;
                    break;

                case CompiledElse _else:
                    EmitStatement(_else.Body, il);

                    current = null;
                    break;

                default:
                    throw new UnreachableException();
            }
        }

        il.MarkLabel(labelEnd);
    }
    void EmitStatement(CompiledBreak statement, ILGenerator il)
    {
        if (LoopLabels.Count == 0)
        {
            Diagnostics.Add(Diagnostic.Critical($"You can only break in a loop", statement));
            return;
        }

        il.Emit(OpCodes.Br, LoopLabels.Last);
    }
    void EmitStatement(CompiledBlock statement, ILGenerator il)
    {
        foreach (CompiledStatement v in statement.Statements)
        {
            EmitStatement(v, il);
        }
    }
    void EmitStatement(CompiledAddressGetter statement, ILGenerator il)
    {
        switch (statement.Of)
        {
            case CompiledVariableGetter v:
                il.Emit(OpCodes.Ldloca, Locals[v.Variable]);
                break;
            case CompiledParameterGetter v:
                il.Emit(OpCodes.Ldarga, v.Variable.Index);
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

                EmitStatement(_object, il);

                while (objectType.To.Is(out PointerType? indirectPointer))
                {
                    il.Emit(OpCodes.Ldind_Ref);
                    objectType = indirectPointer;
                }

                if (objectType.To.Is(out StructType? structType))
                {
                    Type type = ToType(structType);
                    FieldInfo? field = type.GetField(v.Field.Identifier.Content);
                    if (field is null)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Field \"{v.Field.Identifier.Content}\" not found in type {type}", _object));
                        return;
                    }
                    il.Emit(OpCodes.Ldflda, field);
                }
                else
                {
                    Diagnostics.Add(Diagnostic.Critical($"This should be a struct", v.Object));
                }
                break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Can't get the address of {statement.Of} ({statement.Of.GetType().Name})", statement.Of));
                break;
        }
    }
    void EmitStatement(CompiledTypeCast statement, ILGenerator il)
    {
        EmitStatement(statement.Value, il);
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
                        Diagnostics.Add(Diagnostic.Critical($"Invalid casting type {v.Type}", statement));
                        break;
                }
                return;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unimplemented casting type {statement.Type.FinalValue}", statement));
                break;
        }
    }
    void EmitStatement(CompiledCrash statement, ILGenerator il)
    {
        il.Emit(OpCodes.Ldstr, "Crash");
        il.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new Type[] { typeof(string) })!);
        il.Emit(OpCodes.Throw);
    }
    void EmitStatement(CompiledSizeof statement, ILGenerator il)
    {
        if (!FindSize(statement.Of, out int size, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(statement));
            return;
        }

        il.Emit(OpCodes.Ldc_I4, size);
    }
    void EmitStatement(CompiledDelete statement, ILGenerator il)
    {
        EmitStatement(statement.Value, il);
        GenerateDestructor(statement.Cleanup, il);
        il.Emit(OpCodes.Pop);
    }
    void EmitStatement(CompiledConstructorCall statement, ILGenerator il, LocalBuilder? destination = null)
    {
        if (!Functions.TryGetValue(statement.Function, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Function \"{statement.Function}\" wasn't compiled", statement));
            return;
        }

        if (!statement.Object.Type.Is<PointerType>())
        {
            Diagnostics.Add(Diagnostic.Internal($"Only pointer constructors supported", statement));
            return;
        }

        if (destination is not null && statement.Object is CompiledStackAllocation stackAllocation)
        {
            EmitStatement(stackAllocation, il, destination);

            il.Emit(OpCodes.Ldloc, destination.LocalIndex);
            for (int i = 0; i < statement.Arguments.Length; i++)
            {
                EmitStatement(statement.Arguments[i].Value, il);
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
            EmitStatement(statement.Object, il);

            il.Emit(OpCodes.Dup);

            for (int i = 0; i < statement.Arguments.Length; i++)
            {
                EmitStatement(statement.Arguments[i].Value, il);
            }

            il.Emit(OpCodes.Call, function);

            if (!statement.SaveValue &&
                function.ReturnType != typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }
        }
    }
    void EmitStatement(CompiledStackAllocation statement, ILGenerator il, LocalBuilder? destination = null)
    {
        switch (statement.Type)
        {
            case StructType v:
            {
                Type type = ToType(v);
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
                    il.Emit(OpCodes.Ldloc, local.LocalIndex);
                }
                break;
            }
            default:
            {
                Diagnostics.Add(Diagnostic.Internal($"Only structs supported for stack allocations", statement));
                break;
            }
        }
    }
    void EmitStatement(CompiledStringInstance statement, ILGenerator il)
    {
        if (statement.IsASCII)
        {
            Diagnostics.Add(Diagnostic.Internal($"ASCII strings not supported", statement));
        }

        il.Emit(OpCodes.Ldstr, statement.Value + "\0");
    }
    void EmitStatement(CompiledIndexGetter statement, ILGenerator il)
    {
        if (statement.Base.Type.Is(out PointerType? v1) &&
            v1.To.Is(out ArrayType? v2) &&
            v2.Of.SameAs(BuiltinType.Char))
        {
            EmitStatement(statement.Base, il);
            EmitStatement(statement.Index, il);
            il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("get_Chars", new Type[] { typeof(int) })!);
            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"Unimplemented address getter {statement.Base.Type}[{statement.Index.Type}]", statement));
    }
    void EmitStatement(CompiledIndexSetter statement, ILGenerator il)
    {
        Diagnostics.Add(Diagnostic.Critical($"Unimplemented address setter {statement.Base.Type}[{statement.Index.Type}]", statement));
    }
    void EmitStatement(FunctionAddressGetter statement, ILGenerator il)
    {
        if (!Functions.TryGetValue(statement.Function, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Function \"{statement.Function}\" wasn't compiled", statement));
            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"Function address getters not supported", statement));
        // il.Emit(OpCodes.Ldftn, function);
    }
    void EmitStatement(CompiledStatement statement, ILGenerator il)
    {
        switch (statement)
        {
            case EmptyStatement: break;
            case CompiledReturn v: EmitStatement(v, il); break;
            case CompiledEvaluatedValue v: EmitStatement(v, il); break;
            case CompiledBinaryOperatorCall v: EmitStatement(v, il); break;
            case CompiledUnaryOperatorCall v: EmitStatement(v, il); break;
            case CompiledVariableDeclaration v: EmitStatement(v, il); break;
            case CompiledVariableGetter v: EmitStatement(v, il); break;
            case CompiledParameterGetter v: EmitStatement(v, il); break;
            case CompiledFieldGetter v: EmitStatement(v, il); break;
            case CompiledPointer v: EmitStatement(v, il); break;
            case CompiledFunctionCall v: EmitStatement(v, il); break;
            case CompiledExternalFunctionCall v: EmitStatement(v, il); break;
            case CompiledVariableSetter v: EmitStatement(v, il); break;
            case CompiledParameterSetter v: EmitStatement(v, il); break;
            case CompiledFieldSetter v: EmitStatement(v, il); break;
            case CompiledIndirectSetter v: EmitStatement(v, il); break;
            case CompiledWhileLoop v: EmitStatement(v, il); break;
            case CompiledBlock v: EmitStatement(v, il); break;
            case CompiledForLoop v: EmitStatement(v, il); break;
            case CompiledIf v: EmitStatement(v, il); break;
            case CompiledBreak v: EmitStatement(v, il); break;
            case CompiledAddressGetter v: EmitStatement(v, il); break;
            case CompiledFakeTypeCast v: EmitStatement(v.Value, il); break;
            case CompiledTypeCast v: EmitStatement(v, il); break;
            case CompiledCrash v: EmitStatement(v, il); break;
            case CompiledStatementWithValueThatActuallyDoesntHaveValue v: EmitStatement(v.Statement, il); break;
            case CompiledSizeof v: EmitStatement(v, il); break;
            case CompiledDelete v: EmitStatement(v, il); break;
            case CompiledStackAllocation v: EmitStatement(v, il); break;
            case CompiledConstructorCall v: EmitStatement(v, il); break;
            case CompiledStringInstance v: EmitStatement(v, il); break;
            case CompiledIndexGetter v: EmitStatement(v, il); break;
            case CompiledIndexSetter v: EmitStatement(v, il); break;
            case FunctionAddressGetter v: EmitStatement(v, il); break;
            default: throw new NotImplementedException(statement.GetType().Name);
        }
    }

    static string GetTypeId(GeneralType type)
    {
        StringBuilder res = new();
        GetTypeId(res, type);
        return res.ToString();
    }
    static void GetTypeId(StringBuilder builder, GeneralType type)
    {
        switch (type)
        {
            case AliasType v: GetTypeId(builder, v.Value); break;
            case StructType v: GetTypeId(builder, v); break;
            case BuiltinType v: GetTypeId(builder, v); break;
            case ArrayType v: GetTypeId(builder, v); break;
            case FunctionType v: GetTypeId(builder, v); break;
            case PointerType v: GetTypeId(builder, v); break;
            default: throw new UnreachableException();
        }
    }
    static void GetTypeId(StringBuilder builder, BuiltinType type)
    {
        builder.Append("b_");
        builder.Append(type.Type switch
        {
            BasicType.Void => "void",
            BasicType.Any => "any",
            BasicType.U8 => "u8",
            BasicType.I8 => "i8",
            BasicType.U16 => "u16",
            BasicType.I16 => "i16",
            BasicType.U32 => "u32",
            BasicType.I32 => "i32",
            BasicType.U64 => "u64",
            BasicType.I64 => "i64",
            BasicType.F32 => "f32",
            _ => "bruh",
        });
    }
    static void GetTypeId(StringBuilder builder, StructType type)
    {
        builder.Append("s_");
        builder.Append(type.Struct.Identifier.Content);
        if (type.Struct.Template is not null)
        {
            builder.Append("___");
            foreach (Tokenizing.Token typeParameter in type.Struct.Template.Parameters)
            {
                builder.Append('_');
                if (type.TypeArguments.TryGetValue(typeParameter.Content, out GeneralType? typeArgument))
                {
                    GetTypeId(builder, typeArgument);
                }
            }
        }
    }
    static void GetTypeId(StringBuilder builder, ArrayType type)
    {
        builder.Append("a_");
        builder.Append(type.ComputedLength?.ToString() ?? "_");
        builder.Append('_');
        GetTypeId(builder, type.Of);
    }
    static void GetTypeId(StringBuilder builder, FunctionType type)
    {
        builder.Append("f_");
        GetTypeId(builder, type.ReturnType);
        for (int i = 0; i < type.Parameters.Length; i++)
        {
            builder.Append('_');
            GetTypeId(builder, type.Parameters[i]);
        }
    }
    static void GetTypeId(StringBuilder builder, PointerType type)
    {
        builder.Append("p_");
        GetTypeId(builder, type.To);
    }

    Type[] ToType(ImmutableArray<GeneralType> types)
    {
        Type[] result = new Type[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            result[i] = ToType(types[i]);
        }
        return result;
    }
    Type ToType(GeneralType type) => type switch
    {
        AliasType v => ToType(v.Value),
        BuiltinType v => ToType(v),
        StructType v => ToType(v),
        PointerType v => ToType(v),
        ArrayType v => ToType(v),
        FunctionType v => ToType(v),
        _ => throw new UnreachableException(),
    };
    Type ToType(PointerType type)
    {
        if (type.To.Is(out ArrayType? arrayType) &&
            arrayType.Of.SameAs(BuiltinType.Char))
        {
            return typeof(string);
        }
        return typeof(nint);
    }
    Type ToType(StructType type)
    {
        string id = GetTypeId(type);
        Type? result = Module.GetType(id, false, false);
        if (result is not null) return result;
        TypeBuilder builder = Module.DefineType(id, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, typeof(ValueType));
        foreach (CompiledField field in type.Struct.Fields)
        {
            GeneralType fieldType = type.ReplaceType(field.Type, out PossibleDiagnostic? error);
            if (error is not null) Diagnostics.Add(DiagnosticWithoutContext.Critical(error.Message));
            builder.DefineField(field.Identifier.Content, ToType(fieldType), FieldAttributes.Public);
        }
        return builder.CreateType();
    }
    Type ToType(BuiltinType type) => type.Type switch
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
    Type ToType(ArrayType type)
    {
        throw new NotImplementedException();
    }
    Type ToType(FunctionType type)
    {
        return typeof(nint);
    }

    Func<int> GenerateCodeForTopLevelStatements(ImmutableArray<CompiledStatement> statements)
    {
        DynamicMethod method = new(
            "top_level_statements",
            typeof(int),
            Array.Empty<Type>(),
            Module);
        ILGenerator il = method.GetILGenerator();

        EmitMethod(statements, BuiltinType.I32, il);

        return (Func<int>)method.CreateDelegate(typeof(Func<int>));
    }

    void EmitMethod(ImmutableArray<CompiledStatement> statements, GeneralType returnType, ILGenerator il)
    {
        Locals.Clear();

        foreach (CompiledStatement statement in statements)
        {
            EmitStatement(statement, il);
            if (statement is CompiledReturn) goto end;
        }

        switch (returnType.FinalValue)
        {
            case BuiltinType v:
            {
                switch (v.Type)
                {
                    case BasicType.Void:
                        break;
                    case BasicType.I32:
                        il.Emit(OpCodes.Ldc_I4_0);
                        break;
                    default:
                        Diagnostics.Add(DiagnosticWithoutContext.Critical($"Unimplemented return type {v.Type}"));
                        break;
                }
                break;
            }
            case PointerType v:
            {
                il.Emit(OpCodes.Ldnull);
                break;
            }
            default:
                Diagnostics.Add(DiagnosticWithoutContext.Critical($"Unimplemented return type {returnType.FinalValue}"));
                break;
        }
        il.Emit(OpCodes.Ret);

    end:

        Locals.Clear();
    }

    Func<int> GenerateCode(CompilerResult compilerResult)
    {
        CompilerResult res = compilerResult;

        {
            ImmutableArray<CompiledFunction>.Builder compiledFunctions = ImmutableArray.CreateBuilder<CompiledFunction>();
            ImmutableArray<CompiledOperator>.Builder compiledOperators = ImmutableArray.CreateBuilder<CompiledOperator>();
            ImmutableArray<CompiledGeneralFunction>.Builder compiledGeneralFunctions = ImmutableArray.CreateBuilder<CompiledGeneralFunction>();
            ImmutableArray<CompiledConstructor>.Builder compiledConstructors = ImmutableArray.CreateBuilder<CompiledConstructor>();

            foreach ((ICompiledFunction function, CompiledStatement _) in res.Functions2)
            {
                switch (function)
                {
                    case CompiledFunction compiledFunction:
                        compiledFunctions.Add(compiledFunction);
                        break;
                    case CompiledOperator compiledOperator:
                        compiledOperators.Add(compiledOperator);
                        break;
                    case CompiledGeneralFunction compiledGeneralFunction:
                        compiledGeneralFunctions.Add(compiledGeneralFunction);
                        break;
                    case CompiledConstructor compiledConstructor:
                        compiledConstructors.Add(compiledConstructor);
                        break;
                    default:
                        throw new UnreachableException();
                }
            }

            CompiledFunctions = compiledFunctions.ToImmutable();
            CompiledOperators = compiledOperators.ToImmutable();
            CompiledGeneralFunctions = compiledGeneralFunctions.ToImmutable();
            CompiledConstructors = compiledConstructors.ToImmutable();
        }

        AssemblyBuilder assemBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName()
        {
            Name = "DynamicAssembly"
        }, AssemblyBuilderAccess.RunAndCollect);
        Module = assemBuilder.DefineDynamicModule("DynamicModule");

        foreach ((ICompiledFunction function, _) in res.Functions2)
        {
            switch (function)
            {
                case CompiledFunction v:
                {
                    Functions[function] = new DynamicMethod(
                        v.Identifier.Content,
                        ToType(v.Type),
                        ToType(v.ParameterTypes),
                        Module);
                    break;
                }
                case CompiledOperator v:
                {
                    Functions[function] = new DynamicMethod(
                        $"op_{v.Identifier.Content}",
                        ToType(v.Type),
                        ToType(v.ParameterTypes),
                        Module);
                    break;
                }
                case CompiledConstructor v:
                {
                    Functions[function] = new DynamicMethod(
                        $"ctor_{v.Identifier.Content}",
                        ToType(v.Type),
                        ToType(v.ParameterTypes),
                        Module);
                    break;
                }
                case CompiledGeneralFunction v:
                {
                    Functions[function] = new DynamicMethod(
                        $"genr_{v.Identifier.Content}",
                        ToType(v.Type),
                        ToType(v.ParameterTypes),
                        Module);
                    break;
                }
                default: throw new UnreachableException();
            }
        }

        Func<int> result = GenerateCodeForTopLevelStatements(res.CompiledStatements);

        foreach ((ICompiledFunction function, CompiledBlock body) in res.Functions2)
        {
            ILGenerator il = Functions[function].GetILGenerator();
            EmitMethod(body.Statements, function.Type, il);
        }

        return result;
    }
}
