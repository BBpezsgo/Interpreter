using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
[StructLayout(LayoutKind.Explicit)]
public readonly partial struct DataItem
{
    public static DataItem Null => default;

    [field: FieldOffset(0)] public RuntimeType Type { get; }

    #region Value Fields

    [FieldOffset(1)] readonly int _integer;
    [FieldOffset(1)] readonly float _single;

    #endregion

    public bool IsNull => Type == RuntimeType.Null;

    #region Value Properties

    public int UnsafeInt => _integer;
    public char UnsafeChar => (char)_integer;
    public byte UnsafeByte => (byte)_integer;
    public float UnsafeFloat => _single;

    public int? Int => Type switch
    {
        RuntimeType.Byte => _integer,
        RuntimeType.Integer => _integer,
        RuntimeType.Char => _integer,
        RuntimeType.Single => (int)_single,
        RuntimeType.Null => null,
        _ => throw new UnreachableException(),
    };

    public byte? Byte => Type switch
    {
        RuntimeType.Byte => (byte)_integer,
        RuntimeType.Integer => (byte)_integer,
        RuntimeType.Single => (byte)_single,
        RuntimeType.Char => (byte)_integer,
        RuntimeType.Null => null,
        _ => throw new UnreachableException(),
    };

    #endregion

    #region Constructors

    DataItem(RuntimeType type)
    {
        Type = type;
        _integer = default;
        _single = default;
    }

    public DataItem(int value) : this(RuntimeType.Integer)
    { _integer = value; }
    public DataItem(byte value) : this(RuntimeType.Byte)
    { _integer = value; }
    public DataItem(float value) : this(RuntimeType.Single)
    { _single = value; }
    public DataItem(char value) : this(RuntimeType.Char)
    { _integer = value; }
    public DataItem(bool value) : this(value ? 1 : 0)
    { }

    #endregion

    public object? GetValue() => Type switch
    {
        RuntimeType.Null => (object?)null,
        RuntimeType.Byte => (object)(byte)_integer,
        RuntimeType.Integer => (object)_integer,
        RuntimeType.Single => (object)_single,
        RuntimeType.Char => (object)(char)_integer,
        _ => throw new UnreachableException(),
    };

    public string GetDebuggerDisplay() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Integer => _integer.ToString(CultureInfo.InvariantCulture),
        RuntimeType.Byte => ((byte)_integer).ToString(CultureInfo.InvariantCulture),
        RuntimeType.Single => _single.ToString(CultureInfo.InvariantCulture) + "f",
        RuntimeType.Char => $"'{((char)_integer).Escape()}'",
        _ => throw new UnreachableException(),
    };

    public override int GetHashCode() => Type switch
    {
        RuntimeType.Byte => HashCode.Combine(Type, (byte)_integer),
        RuntimeType.Integer => HashCode.Combine(Type, _integer),
        RuntimeType.Single => HashCode.Combine(Type, _single),
        RuntimeType.Char => HashCode.Combine(Type, (char)_integer),
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
                Console.Write((byte)_integer);
                break;
            case RuntimeType.Integer:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(_integer);
                break;
            case RuntimeType.Single:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(_single);
                break;
            case RuntimeType.Char:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"\'{((char)_integer).Escape()}\'");
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
    /// <exception cref="ArgumentNullException"/>
    public static DataItem GetValue(object? value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        return value switch
        {
            byte v => new DataItem(v),
            int v => new DataItem(v),
            float v => new DataItem(v),
            bool v => new DataItem(v),
            char v => new DataItem(v),
            Enum v => v.GetTypeCode() switch
            {
                TypeCode.Boolean => new DataItem((bool)value),
                TypeCode.Char => new DataItem((char)value),
                TypeCode.SByte => new DataItem((sbyte)value),
                TypeCode.Byte => new DataItem((byte)value),
                TypeCode.Int16 => new DataItem((short)value),
                TypeCode.UInt16 => new DataItem((char)value),
                TypeCode.Int32 => new DataItem((int)value),
                TypeCode.Single => new DataItem((float)value),
                _ => throw new NotImplementedException($"Cannot convert {value} to {typeof(DataItem)}"),
            },
            _ => throw new NotImplementedException($"Cannot convert {value.GetType()} to {typeof(DataItem)}"),
        };
    }

    public static bool TryShrinkTo8bit(ref DataItem value)
    {
        switch (value.Type)
        {
            case RuntimeType.Byte: return true;

            case RuntimeType.Char:
            {
                if ((char)value._integer is < (char)byte.MinValue or > (char)byte.MaxValue)
                { return false; }
                value = new DataItem((byte)value._integer);
                return true;
            }

            case RuntimeType.Integer:
            {
                if (value._integer is < byte.MinValue or > byte.MaxValue)
                { return false; }
                value = new DataItem((byte)value._integer);
                return true;
            }

            default: return false;
        }
    }

    public static bool TryShrinkTo16bit(ref DataItem value)
    {
        switch (value.Type)
        {
            case RuntimeType.Byte:
            {
                value = new DataItem((char)value._integer);
                return true;
            }

            case RuntimeType.Char: return true;

            case RuntimeType.Integer:
            {
                if (value._integer is < char.MinValue or > char.MaxValue)
                { return false; }
                value = new DataItem((char)value._integer);
                return true;
            }

            default:
                return false;
        }
    }
}
