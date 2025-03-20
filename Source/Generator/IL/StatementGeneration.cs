using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    readonly Dictionary<CompiledVariableDeclaration, LocalBuilder> Locals = new();
    readonly Dictionary<ICompiledFunction, DynamicMethod> Functions = new();
    readonly Stack<Label> LoopLabels = new();

    ModuleBuilder Module = null!;

    void EmitStatement(CompiledEvaluatedValue statement, ILGenerator il)
    {
        switch (statement.Value.Type)
        {
            case RuntimeType.U16:
                il.Emit(OpCodes.Ldc_I4_S, statement.Value.U16);
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
        }

        throw new NotImplementedException();
    }

    void EmitStatement(CompiledReturn statement, ILGenerator il)
    {
        if (statement.Value is null)
        {
            throw new NotImplementedException();
        }

        EmitStatement(statement.Value, il);
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

        throw new NotImplementedException();
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

        throw new NotImplementedException();
    }

    void EmitStatement(CompiledVariableDeclaration statement, ILGenerator il)
    {
        if (Locals.ContainsKey(statement)) return;
        LocalBuilder local = Locals[statement] = il.DeclareLocal(ToType(statement.Type));
        if (statement.InitialValue is not null)
        {
            EmitStatement(statement.InitialValue, il);
            il.Emit(OpCodes.Stloc, local.LocalIndex);
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
            throw new NotImplementedException();
        }
    }

    void EmitStatement(CompiledFunctionCall statement, ILGenerator il)
    {
        if (!Functions.TryGetValue(statement.Function, out DynamicMethod? function))
        {
            throw new NotImplementedException();
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
        if (statement.Function is ExternalFunctionSync f &&
            f.UnmanagedCallback != 0)
        {
            foreach (CompiledPassedArgument item in statement.Arguments)
            {
                EmitStatement(item.Value, il);
            }
            il.Emit(OpCodes.Ldc_I4, f.UnmanagedCallback);
            il.Emit(OpCodes.Calli);

            return;
        }

        throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
    void EmitStatement(CompiledIndirectSetter statement, ILGenerator il)
    {
        EmitStatement(statement.AddressValue, il);
        EmitStatement(statement.Value, il);
        if (!statement.AddressValue.Type.Is(out PointerType? pointer))
        { throw new NotImplementedException(); }

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
                    case BasicType.F32: il.Emit(OpCodes.Stind_R4); break;
                    default: throw new NotImplementedException();
                }
                break;
            default: throw new NotImplementedException();
        }
    }
    void EmitStatement(CompiledPointer statement, ILGenerator il)
    {
        EmitStatement(statement.To, il);
        if (!statement.To.Type.Is(out PointerType? pointer))
        { throw new NotImplementedException(); }

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
                    case BasicType.F32: il.Emit(OpCodes.Ldind_R4); break;
                    default: throw new NotImplementedException();
                }
                break;
            default: throw new NotImplementedException();
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

        if (LoopLabels.Pop() != loopEnd) throw new InternalExceptionWithoutContext();
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

        if (LoopLabels.Pop() != loopEnd) throw new InternalExceptionWithoutContext();
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

                default: throw new NotImplementedException();
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
                    throw new NotImplementedException();
                }
                break;
            default: throw new NotImplementedException();
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
                    case BasicType.U16: il.Emit(OpCodes.Conv_U2); break;
                    default: throw new NotImplementedException();
                }
                return;
            default: throw new NotImplementedException();
        }
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
            default: throw new NotImplementedException();
        }
    }
    static void GetTypeId(StringBuilder builder, BuiltinType type)
    {
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
            BasicType.F32 => "f32",
            _ => "bruh",
        });
    }
    static void GetTypeId(StringBuilder builder, StructType type)
    {
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
        _ => throw new NotImplementedException(),
    };
    static Type ToType(PointerType type)
    {
        return typeof(nint);
    }
    Type ToType(StructType type)
    {
        string id = GetTypeId(type);
        Type? result = Module.GetType(id, false, false);
        if (result is not null) return result;
        TypeBuilder builder = Module.DefineType(id, TypeAttributes.Public);
        foreach (CompiledField field in type.Struct.Fields)
        {
            GeneralType fieldType = type.ReplaceType(field.Type, out PossibleDiagnostic? error);
            if (error is not null) Diagnostics.Add(DiagnosticWithoutContext.Critical(error.Message));
            builder.DefineField(field.Identifier.Content, ToType(fieldType), FieldAttributes.Public);
        }
        return builder.CreateType();
    }
    static Type ToType(BuiltinType type) => type.Type switch
    {
        BasicType.Void => typeof(void),
        BasicType.U8 => typeof(byte),
        BasicType.I8 => typeof(sbyte),
        BasicType.U16 => typeof(ushort),
        BasicType.I16 => typeof(short),
        BasicType.U32 => typeof(uint),
        BasicType.I32 => typeof(int),
        BasicType.F32 => typeof(float),
        _ => throw new NotImplementedException(),
    };

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
                    default: throw new NotImplementedException();
                }
                break;
            }
            default: throw new NotImplementedException();
        }
        il.Emit(OpCodes.Ret);

    end:

        Locals.Clear();
    }

    Func<int> GenerateCode(CompilerResult2 compilerResult)
    {
        CompilerResult2 res = compilerResult;

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
                        throw new NotImplementedException();
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
                default: throw new NotImplementedException();
            }
        }

        Func<int> result = GenerateCodeForTopLevelStatements(res.Statements2);

        foreach ((ICompiledFunction function, CompiledBlock body) in res.Functions2)
        {
            ILGenerator il = Functions[function].GetILGenerator();
            EmitMethod(body.Statements, function.Type, il);
        }

        return result;
    }
}
