using System.Reflection.Emit;
using LanguageCore.Compiler;

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
}
