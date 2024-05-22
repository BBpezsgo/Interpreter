using System.Runtime.InteropServices;

namespace LanguageCore.Compiler;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
[StructLayout(LayoutKind.Explicit)]
public readonly partial struct CompiledValue
{
    public static CompiledValue Null => default;

    [field: FieldOffset(0)] public RuntimeType Type { get; }

    #region Value Fields

    [FieldOffset(1)] readonly int _integer;
    [FieldOffset(1)] readonly float _single;

    #endregion

    public bool IsNull => Type == RuntimeType.Null;

    #region Value Properties

    public int Int => _integer;
    public char Char => (char)_integer;
    public byte Byte => (byte)_integer;
    public float Single => _single;

    #endregion

    #region Constructors

    CompiledValue(RuntimeType type)
    {
        Type = type;
        _integer = default;
        _single = default;
    }

    public CompiledValue(int value) : this(RuntimeType.Integer)
    { _integer = value; }
    public CompiledValue(byte value) : this(RuntimeType.Byte)
    { _integer = value; }
    public CompiledValue(float value) : this(RuntimeType.Single)
    { _single = value; }
    public CompiledValue(char value) : this(RuntimeType.Char)
    { _integer = value; }
    public CompiledValue(ushort value) : this(RuntimeType.Char)
    { _integer = value; }
    public CompiledValue(bool value) : this(value ? 1 : 0)
    { }

    #endregion

    public object? GetValue() => Type switch
    {
        RuntimeType.Null => null,
        RuntimeType.Byte => Byte,
        RuntimeType.Integer => Int,
        RuntimeType.Single => Single,
        RuntimeType.Char => Char,
        _ => throw new UnreachableException(),
    };

    public string GetDebuggerDisplay() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Integer => Int.ToString(CultureInfo.InvariantCulture),
        RuntimeType.Byte => Byte.ToString(CultureInfo.InvariantCulture),
        RuntimeType.Single => Single.ToString(CultureInfo.InvariantCulture) + "f",
        RuntimeType.Char => $"'{Char.Escape()}'",
        _ => throw new UnreachableException(),
    };

    public override int GetHashCode() => Type switch
    {
        RuntimeType.Byte => HashCode.Combine(Type, Byte),
        RuntimeType.Integer => HashCode.Combine(Type, Int),
        RuntimeType.Single => HashCode.Combine(Type, Single),
        RuntimeType.Char => HashCode.Combine(Type, Char),
        _ => throw new UnreachableException(),
    };

    public void DebugPrint()
    {
        ConsoleColor savedFgColor = Console.ForegroundColor;
        ConsoleColor savedBgColor = Console.BackgroundColor;

        switch (Type)
        {
            case RuntimeType.Byte:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(Byte);
                break;
            case RuntimeType.Integer:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(Int);
                break;
            case RuntimeType.Single:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(Single);
                break;
            case RuntimeType.Char:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"\'{Char.Escape()}\'");
                break;
            case RuntimeType.Null:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("null");
                break;
            default: throw new UnreachableException();
        }

        Console.ResetColor();
        Console.ForegroundColor = savedFgColor;
        Console.BackgroundColor = savedBgColor;
    }

    /// <exception cref="NotImplementedException"/>
    public static CompiledValue GetValue(object value) => value switch
    {
        byte v => new CompiledValue(v),
        int v => new CompiledValue(v),
        float v => new CompiledValue(v),
        bool v => new CompiledValue(v),
        char v => new CompiledValue(v),
        _ => throw new NotImplementedException($"Cannot convert {value.GetType()} to {typeof(CompiledValue)}"),
    };

    /// <exception cref="NotImplementedException"/>
    public static CompiledValue GetValue<T>(T value) where T : notnull => value switch
    {
        byte v => new CompiledValue(v),
        int v => new CompiledValue(v),
        float v => new CompiledValue(v),
        bool v => new CompiledValue(v),
        char v => new CompiledValue(v),
        _ => throw new NotImplementedException($"Cannot convert {value.GetType()} to {typeof(CompiledValue)}"),
    };

    public static bool TryShrinkTo8bit(ref CompiledValue value)
    {
        switch (value.Type)
        {
            case RuntimeType.Byte: return true;

            case RuntimeType.Char:
            {
                if (value.Char is < (char)byte.MinValue or > (char)byte.MaxValue)
                { return false; }
                value = new CompiledValue((byte)value.Char);
                return true;
            }

            case RuntimeType.Integer:
            {
                if (value.Int is < byte.MinValue or > byte.MaxValue)
                { return false; }
                value = new CompiledValue((byte)value.Int);
                return true;
            }

            default: return false;
        }
    }

    public static bool TryShrinkTo16bit(ref CompiledValue value)
    {
        switch (value.Type)
        {
            case RuntimeType.Byte:
            {
                value = new CompiledValue((char)value.Byte);
                return true;
            }

            case RuntimeType.Char: return true;

            case RuntimeType.Integer:
            {
                if (value.Int is < char.MinValue or > char.MaxValue)
                { return false; }
                value = new CompiledValue((char)value.Int);
                return true;
            }

            default:
                return false;
        }
    }
}
