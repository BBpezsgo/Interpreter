using System.Runtime.InteropServices;

namespace LanguageCore.Compiler;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
[StructLayout(LayoutKind.Explicit)]
public readonly partial struct CompiledValue
{
    public static CompiledValue Null => default;

    [FieldOffset(0)] public readonly RuntimeType Type;

    [FieldOffset(1)] public readonly int I32;
    [FieldOffset(1)] public readonly float F32;
    [FieldOffset(1)] public readonly byte U8;
    [FieldOffset(1)] public readonly sbyte I8;
    [FieldOffset(1)] public readonly char Char;
    [FieldOffset(1)] public readonly short I16;
    [FieldOffset(1)] public readonly uint U32;

    public bool IsNull => Type == RuntimeType.Null;

    #region Constructors

    CompiledValue(RuntimeType type)
    {
        Type = type;
    }

    public CompiledValue(byte value) : this(RuntimeType.U8)
    { U8 = value; }
    public CompiledValue(sbyte value) : this(RuntimeType.I8)
    { I8 = value; }
    public CompiledValue(char value) : this(RuntimeType.Char)
    { Char = value; }
    public CompiledValue(ushort value) : this(RuntimeType.Char)
    { Char = (char)value; }
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

    public string GetDebuggerDisplay() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.U8 => U8.ToString(CultureInfo.InvariantCulture),
        RuntimeType.I8 => I8.ToString(CultureInfo.InvariantCulture),
        RuntimeType.Char => $"'{Char.Escape()}'",
        RuntimeType.I16 => I16.ToString(CultureInfo.InvariantCulture),
        RuntimeType.F32 => F32.ToString(CultureInfo.InvariantCulture) + "f",
        RuntimeType.U32 => U32.ToString(CultureInfo.InvariantCulture),
        RuntimeType.I32 => I32.ToString(CultureInfo.InvariantCulture),
        _ => throw new UnreachableException(),
    };

    public override int GetHashCode() => Type switch
    {
        RuntimeType.U8 => HashCode.Combine(Type, U8),
        RuntimeType.I8 => HashCode.Combine(Type, I8),
        RuntimeType.Char => HashCode.Combine(Type, Char),
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

            case RuntimeType.Char:
            {
                if (value.Char is < (char)byte.MinValue or > (char)byte.MaxValue)
                { return false; }
                value = new CompiledValue((byte)value.Char);
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
                value = new CompiledValue((char)value.U8);
                return true;
            }

            case RuntimeType.I8:
            {
                value = new CompiledValue((char)value.U8);
                return true;
            }

            case RuntimeType.Char: return true;

            case RuntimeType.I16:
            {
                if (value.I16 < (short)char.MinValue)
                { return false; }
                value = new CompiledValue((char)value.I16);
                return true;
            }

            case RuntimeType.U32:
            {
                if (value.U32 is < char.MinValue or > char.MaxValue)
                { return false; }
                value = new CompiledValue((char)value.U32);
                return true;
            }

            case RuntimeType.I32:
            {
                if (value.I32 is < char.MinValue or > char.MaxValue)
                { return false; }
                value = new CompiledValue((char)value.I32);
                return true;
            }

            default:
                return false;
        }
    }
}
