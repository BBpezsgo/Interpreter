using System.Reflection;
using System.Reflection.Emit;
using LanguageCore.Compiler;
using LanguageCore.IL.Reflection;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForIL : CodeGenerator
{
    protected override unsafe bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = sizeof(nint);
        error = null;
        return true;
    }

    protected override unsafe bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = sizeof(nint);
        error = null;
        return true;
    }

    bool LoadIndirect(GeneralType type, ILProxy il, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        switch (type.FinalValue)
        {
            case BuiltinType v:
                switch (v.Type)
                {
                    case BasicType.U8: il.Emit(OpCodes.Ldind_U1); return true;
                    case BasicType.I8: il.Emit(OpCodes.Ldind_I1); return true;
                    case BasicType.U16: il.Emit(OpCodes.Ldind_U2); return true;
                    case BasicType.I16: il.Emit(OpCodes.Ldind_I2); return true;
                    case BasicType.U32: il.Emit(OpCodes.Ldind_I4); return true;
                    case BasicType.I32: il.Emit(OpCodes.Ldind_U4); return true;
                    case BasicType.U64: il.Emit(OpCodes.Ldind_I8); return true;
                    case BasicType.I64: il.Emit(OpCodes.Ldind_I8); return true;
                    case BasicType.F32: il.Emit(OpCodes.Ldind_R4); return true;
                    case BasicType.Void:
                    case BasicType.Any:
                    default:
                        break;
                }
                break;
            case StructType v:
                if (!ToType(v, out Type? _type, out error)) return false;
                il.Emit(OpCodes.Ldobj, _type);
                return true;
            case PointerType:
                il.Emit(OpCodes.Ldind_Ref);
                return true;
            default:
                break;
        }

        error = new PossibleDiagnostic($"Unimplemented dereference for type {type.FinalValue}");
        return false;
    }

    bool StoreIndirect(GeneralType type, ILProxy il, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        switch (type.FinalValue)
        {
            case BuiltinType v:
                switch (v.Type)
                {
                    case BasicType.U8: il.Emit(OpCodes.Stind_I1); return true;
                    case BasicType.I8: il.Emit(OpCodes.Stind_I1); return true;
                    case BasicType.U16: il.Emit(OpCodes.Stind_I2); return true;
                    case BasicType.I16: il.Emit(OpCodes.Stind_I2); return true;
                    case BasicType.U32: il.Emit(OpCodes.Stind_I4); return true;
                    case BasicType.I32: il.Emit(OpCodes.Stind_I4); return true;
                    case BasicType.U64: il.Emit(OpCodes.Stind_I8); return true;
                    case BasicType.I64: il.Emit(OpCodes.Stind_I8); return true;
                    case BasicType.F32: il.Emit(OpCodes.Stind_R4); return true;
                    case BasicType.Void:
                    case BasicType.Any:
                    default:
                        break;
                }
                break;
            case StructType v:
                if (!ToType(v, out Type? _type, out error)) return false;
                il.Emit(OpCodes.Stobj, _type);
                return true;
            case PointerType:
                il.Emit(OpCodes.Stind_Ref);
                return true;
            default:
                break;
        }

        error = new PossibleDiagnostic($"Unimplemented dereference for type {type.FinalValue}");
        return false;
    }

    static bool LoadIndirect(Type type, ILProxy il, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (type == typeof(int)) { il.Emit(OpCodes.Ldind_I4); return true; }
        else if (type == typeof(long)) { il.Emit(OpCodes.Ldind_I8); return true; }
        else if (type == typeof(short)) { il.Emit(OpCodes.Ldind_I2); return true; }
        else if (type == typeof(sbyte)) { il.Emit(OpCodes.Ldind_I1); return true; }
        else if (type == typeof(uint)) { il.Emit(OpCodes.Ldind_U4); return true; }
        else if (type == typeof(ushort)) { il.Emit(OpCodes.Ldind_U2); return true; }
        else if (type == typeof(byte)) { il.Emit(OpCodes.Ldind_U1); return true; }
        else if (type == typeof(float)) { il.Emit(OpCodes.Ldind_R4); return true; }
        else if (type == typeof(double)) { il.Emit(OpCodes.Ldind_R8); return true; }
        else if (type == typeof(nint)) { il.Emit(OpCodes.Ldind_I); return true; }
        else if (!type.IsPrimitive && type.IsValueType && !type.IsEnum) { il.Emit(OpCodes.Ldobj, type); return true; }
        else
        {
            Debugger.Break();
            error = new PossibleDiagnostic($"Unimplemented dereference for type {type}");
            return false;
        }
    }

    static bool StoreIndirect(Type type, ILProxy il, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (type == typeof(int)) { il.Emit(OpCodes.Stind_I4); return true; }
        else if (type == typeof(long)) { il.Emit(OpCodes.Stind_I8); return true; }
        else if (type == typeof(short)) { il.Emit(OpCodes.Stind_I2); return true; }
        else if (type == typeof(sbyte)) { il.Emit(OpCodes.Stind_I1); return true; }
        else if (type == typeof(ulong)) { il.Emit(OpCodes.Stind_I8); return true; }
        else if (type == typeof(ushort)) { il.Emit(OpCodes.Stind_I2); return true; }
        else if (type == typeof(byte)) { il.Emit(OpCodes.Stind_I1); return true; }
        else if (type == typeof(float)) { il.Emit(OpCodes.Stind_R4); return true; }
        else if (type == typeof(double)) { il.Emit(OpCodes.Stind_R8); return true; }
        else if (type == typeof(void)) { return true; }
        else if (!type.IsPrimitive && type.IsValueType && !type.IsEnum) { il.Emit(OpCodes.Stobj, type); return true; }
        else
        {
            Debugger.Break();
            error = new PossibleDiagnostic($"{type}");
            return false;
        }
    }

    static void StoreLocal(ILProxy il, int localIndex)
    {
        switch (localIndex)
        {
            case 0: il.Emit(OpCodes.Stloc_0); break;
            case 1: il.Emit(OpCodes.Stloc_1); break;
            case 2: il.Emit(OpCodes.Stloc_2); break;
            case 3: il.Emit(OpCodes.Stloc_3); break;
            default: il.Emit(OpCodes.Stloc, localIndex); break;
        }
    }

    static void LoadLocal(ILProxy il, int localIndex)
    {
        switch (localIndex)
        {
            case 0: il.Emit(OpCodes.Ldloc_0); break;
            case 1: il.Emit(OpCodes.Ldloc_1); break;
            case 2: il.Emit(OpCodes.Ldloc_2); break;
            case 3: il.Emit(OpCodes.Ldloc_3); break;
            default: il.Emit(OpCodes.Ldloc, localIndex); break;
        }
    }

    static void LoadArgument(ILProxy il, int index)
    {
        switch (index)
        {
            case 0: il.Emit(OpCodes.Ldarg_0); break;
            case 1: il.Emit(OpCodes.Ldarg_1); break;
            case 2: il.Emit(OpCodes.Ldarg_2); break;
            case 3: il.Emit(OpCodes.Ldarg_3); break;
            default: il.Emit(OpCodes.Ldarg, index); break;
        }
    }

    static bool EmitDefaultValue(GeneralType type, ILProxy il, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        switch (type.FinalValue)
        {
            case BuiltinType v:
            {
                switch (v.Type)
                {
                    case BasicType.Void:
                        return true;
                    case BasicType.I32:
                        il.Emit(OpCodes.Ldc_I4_0);
                        return true;
                    case BasicType.F32:
                        il.Emit(OpCodes.Ldc_R4, 0f);
                        return true;
                    case BasicType.U8:
                        il.Emit(OpCodes.Ldc_I4_S, (byte)0);
                        return true;
                    default:
                        Debugger.Break();
                        error = new PossibleDiagnostic($"Unimplemented return type {type}");
                        return false;
                }
            }
            case PointerType:
            {
                il.Emit(OpCodes.Ldnull);
                return true;
            }
            default:
                Debugger.Break();
                error = new PossibleDiagnostic($"Unimplemented return type {type}");
                return false;
        }
    }

    static void EmitValue(float value, ILProxy il) => il.Emit(OpCodes.Ldc_R4, value);
    static void EmitValue(int value, ILProxy il)
    {
        switch (value)
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
            case >= sbyte.MinValue and <= sbyte.MaxValue: il.Emit(OpCodes.Ldc_I4_S, (sbyte)value); break;
            default: il.Emit(OpCodes.Ldc_I4, value); break;
        }
    }

    static void Stringify(StringBuilder builder, int indentation, Type type)
    {
        builder.Indent(indentation);
        builder.Append($"{type.Attributes & TypeAttributes.VisibilityMask} ");

        foreach (TypeAttributes attribute in CompatibilityUtils.GetEnumValues<TypeAttributes>()
            .Where(v => (v & TypeAttributes.VisibilityMask) == 0 && v != 0))
        {
            if (type.Attributes.HasFlag(attribute))
            {
                builder.Append($"{attribute} ");
            }
        }

        builder.Append(type.Name);
        builder.AppendLine();

        builder.Indent(indentation);
        builder.Append('{');
        builder.AppendLine();

        foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
        {
            if (member is FieldInfo field)
            {
                Stringify(builder, indentation + 1, field);
            }
        }

        builder.Indent(indentation);
        builder.Append('}');
        builder.AppendLine();
    }

    static void Stringify(StringBuilder builder, int indentation, FieldInfo field)
    {
        builder.Indent(indentation);
        builder.Append($"{field.Attributes & FieldAttributes.FieldAccessMask} ");

        foreach (FieldAttributes attribute in CompatibilityUtils.GetEnumValues<FieldAttributes>()
            .Where(v => (v & FieldAttributes.FieldAccessMask) == 0 && v != 0))
        {
            if (field.Attributes.HasFlag(attribute))
            {
                builder.Append($"{attribute} ");
            }
        }

        builder.Append(field.FieldType.ToString());
        builder.Append(' ');
        builder.Append(field.Name);
        builder.Append(';');
        builder.AppendLine();
    }

    static void Stringify(StringBuilder builder, int indentation, DynamicMethod method)
    {
        builder.Indent(indentation);
        builder.Append($"{method.Attributes & MethodAttributes.MemberAccessMask} ");

        foreach (MethodAttributes attribute in CompatibilityUtils.GetEnumValues<MethodAttributes>()
            .Where(v => (v & MethodAttributes.MemberAccessMask) == 0 && v != 0))
        {
            if (method.Attributes.HasFlag(attribute))
            {
                builder.Append($"{attribute} ");
            }
        }

        builder.Append(method.ReturnType.ToString());
        builder.Append(' ');
        builder.Append(method.Name);
        builder.Append('(');
        ParameterInfo[] parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            ParameterInfo parameter = parameters[i];
            foreach (ParameterAttributes attribute in CompatibilityUtils.GetEnumValues<ParameterAttributes>().Where(v => v is not ParameterAttributes.None))
            {
                if (parameter.Attributes.HasFlag(attribute))
                {
                    builder.Append($"{attribute} ");
                }
            }
            builder.Append(parameter.ParameterType.ToString());
            builder.Append(' ');
            builder.Append(parameter.Name);
        }
        builder.Append(')');
        builder.AppendLine();

        builder.Indent(indentation);
        builder.Append('{');
        builder.AppendLine();

        byte[]? il = DynamicMethodILProvider.GetByteArray(method);
        if (il is null)
        {
            builder.Indent(indentation + 1);
            builder.AppendLine("// IL isn't avaliable");
        }
        else
        {
            foreach (ILInstruction instruction in new ILReader(il, new DynamicScopeTokenResolver(method)))
            {
                builder.Indent(indentation + 1);
                builder.Append(instruction.ToString());
                builder.AppendLine();
            }
        }

        builder.Indent(indentation);
        builder.Append('}');
        builder.AppendLine();
        builder.AppendLine();
    }

    static bool CheckCode(DynamicMethod method)
    {
        try
        {
            foreach (ILInstruction instruction in new ILReader(DynamicMethodILProvider.GetByteArray(method) ?? Array.Empty<byte>(), new DynamicScopeTokenResolver(method)))
            {

            }
        }
        catch
        {
            Debugger.Break();
            return false;
        }
        return true;
    }
}
