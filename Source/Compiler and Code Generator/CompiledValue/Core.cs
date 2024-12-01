using System.Runtime.InteropServices;
using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

[StructLayout(LayoutKind.Explicit)]
public readonly partial struct CompiledValue
{
    public static CompiledValue Null => default;

    [FieldOffset(0)] public readonly RuntimeType Type;

    [FieldOffset(1)] public readonly int I32;
    [FieldOffset(1)] public readonly float F32;
    [FieldOffset(1)] public readonly byte U8;
    [FieldOffset(1)] public readonly sbyte I8;
    [FieldOffset(1)] public readonly ushort U16;
    [FieldOffset(1)] public readonly char Char;
    [FieldOffset(1)] public readonly short I16;
    [FieldOffset(1)] public readonly uint U32;

    public bool IsNull => Type == RuntimeType.Null;

    public BitWidth BitWidth => Type switch
    {
        RuntimeType.U8 => BitWidth._8,
        RuntimeType.I8 => BitWidth._8,
        RuntimeType.U16 => BitWidth._16,
        RuntimeType.I16 => BitWidth._16,
        RuntimeType.U32 => BitWidth._32,
        RuntimeType.I32 => BitWidth._32,
        RuntimeType.F32 => BitWidth._32,
        _ => default,
    };

    #region Constructors

    CompiledValue(RuntimeType type)
    {
        Type = type;
    }

    public CompiledValue(byte value) : this(RuntimeType.U8)
    { U8 = value; }
    public CompiledValue(sbyte value) : this(RuntimeType.I8)
    { I8 = value; }
    public CompiledValue(char value) : this(RuntimeType.U16)
    { Char = value; }
    public CompiledValue(ushort value) : this(RuntimeType.U16)
    { U16 = value; }
    public CompiledValue(short value) : this(RuntimeType.I16)
    { I16 = value; }
    public CompiledValue(uint value) : this(RuntimeType.U32)
    { U32 = value; }
    public CompiledValue(int value) : this(RuntimeType.I32)
    { I32 = value; }
    public CompiledValue(float value) : this(RuntimeType.F32)
    { F32 = value; }
    public CompiledValue(bool value) : this((byte)(value ? 1 : 0))
    { }

    CompiledValue(int value, RuntimeType type) : this(type)
    { I32 = value; }

    CompiledValue(float value, RuntimeType type) : this(type)
    { F32 = value; }

    public static CompiledValue CreateUnsafe(int value, RuntimeType type) => new(value, type);
    public static CompiledValue CreateUnsafe(float value, RuntimeType type) => new(value, type);

    #endregion

    public override int GetHashCode() => Type switch
    {
        RuntimeType.U8 => HashCode.Combine(Type, U8),
        RuntimeType.I8 => HashCode.Combine(Type, I8),
        RuntimeType.U16 => HashCode.Combine(Type, U16),
        RuntimeType.I16 => HashCode.Combine(Type, I16),
        RuntimeType.U32 => HashCode.Combine(Type, U32),
        RuntimeType.I32 => HashCode.Combine(Type, I32),
        RuntimeType.F32 => HashCode.Combine(Type, F32),
        _ => throw new UnreachableException(),
    };

    public static bool TryShrinkTo8bit(ref CompiledValue value)
    {
        switch (value.Type)
        {
            case RuntimeType.U8: return true;
            case RuntimeType.I8: return true;

            case RuntimeType.U16:
            {
                if (value.U16 is < (ushort)byte.MinValue or > (ushort)byte.MaxValue)
                { return false; }
                value = new CompiledValue((byte)value.U16);
                return true;
            }

            case RuntimeType.I16:
            {
                if (value.I16 is < (short)byte.MinValue or > (short)byte.MaxValue)
                { return false; }
                value = new CompiledValue((short)value.I16);
                return true;
            }

            case RuntimeType.U32:
            {
                if (value.U32 is < (uint)byte.MinValue or > (uint)byte.MaxValue)
                { return false; }
                value = new CompiledValue((uint)value.U32);
                return true;
            }

            case RuntimeType.I32:
            {
                if (value.I32 is < byte.MinValue or > byte.MaxValue)
                { return false; }
                value = new CompiledValue((byte)value.I32);
                return true;
            }

            default: return false;
        }
    }

    public static bool TryShrinkTo16bit(ref CompiledValue value)
    {
        switch (value.Type)
        {
            case RuntimeType.U8:
            {
                value = new CompiledValue((ushort)value.U8);
                return true;
            }

            case RuntimeType.I8:
            {
                value = new CompiledValue((ushort)value.U8);
                return true;
            }

            case RuntimeType.U16: return true;

            case RuntimeType.I16:
            {
                if (value.I16 < (short)ushort.MinValue)
                { return false; }
                value = new CompiledValue((ushort)value.I16);
                return true;
            }

            case RuntimeType.U32:
            {
                if (value.U32 is < ushort.MinValue or > ushort.MaxValue)
                { return false; }
                value = new CompiledValue((ushort)value.U32);
                return true;
            }

            case RuntimeType.I32:
            {
                if (value.I32 is < ushort.MinValue or > ushort.MaxValue)
                { return false; }
                value = new CompiledValue((ushort)value.I32);
                return true;
            }

            default:
                return false;
        }
    }
}
