using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
[StructLayout(LayoutKind.Explicit)]
public readonly partial struct DataItem
{
    public static DataItem Null => default;

    [field: FieldOffset(0)] public RuntimeType Type { get; }

    #region Value Fields

    [FieldOffset(1)] readonly byte _byte;
    [FieldOffset(1)] readonly int _integer;
    [FieldOffset(1)] readonly float _single;
    [FieldOffset(1)] readonly char _char;

    #endregion

    public bool IsNull => Type == RuntimeType.Null;

    #region Value Properties

    /// <exception cref="RuntimeException"/>
    public byte VByte => Type switch
    {
        RuntimeType.Byte => _byte,
        _ => throw new RuntimeException($"Can't cast {Type} to {typeof(byte)}")
    };
    /// <exception cref="RuntimeException"/>
    public int VInt => Type switch
    {
        RuntimeType.Integer => _integer,
        _ => throw new RuntimeException($"Can't cast {Type} to {typeof(int)}")
    };
    /// <exception cref="RuntimeException"/>
    public float VSingle => Type switch
    {
        RuntimeType.Single => _single,
        _ => throw new RuntimeException($"Can't cast {Type} to {typeof(float)}")
    };
    /// <exception cref="RuntimeException"/>
    public char VChar => Type switch
    {
        RuntimeType.Char => _char,
        RuntimeType.Integer => (char)_integer,
        RuntimeType.Byte => (char)_integer,
        _ => throw new RuntimeException($"Can't cast {Type} to {typeof(char)}")
    };

    #endregion

    #region Constructors

    DataItem(RuntimeType type)
    {
        Type = type;
        _integer = default;
        _byte = default;
        _single = default;
        _char = default;
    }

    public DataItem(int value) : this(RuntimeType.Integer)
    { _integer = value; }
    public DataItem(byte value) : this(RuntimeType.Byte)
    { _byte = value; }
    public DataItem(float value) : this(RuntimeType.Single)
    { _single = value; }
    public DataItem(char value) : this(RuntimeType.Char)
    { _char = value; }
    public DataItem(bool value) : this(value ? 1 : 0)
    { }

    #endregion

    public int? Integer => Type switch
    {
        RuntimeType.Byte => VByte,
        RuntimeType.Integer => VInt,
        RuntimeType.Char => VChar,
        RuntimeType.Single => null,
        RuntimeType.Null => null,
        _ => throw new UnreachableException(),
    };

    public byte? Byte
    {
        get
        {
            switch (Type)
            {
                case RuntimeType.Byte:
                    return _byte;
                case RuntimeType.Integer:
                    if (_integer is >= byte.MinValue and <= byte.MaxValue)
                    { return (byte)_integer; }
                    return null;
                case RuntimeType.Single:
                    if (!float.IsInteger(_single)) return null;
                    if (_single is >= byte.MinValue and <= byte.MaxValue)
                    { return (byte)_single; }
                    return null;
                case RuntimeType.Char:
                    if ((ushort)_char is >= byte.MinValue and <= byte.MaxValue)
                    { return (byte)_char; }
                    return null;
                default:
                    return null;
            }
        }
    }

    public object? GetValue() => Type switch
    {
        RuntimeType.Null => (object?)null,
        RuntimeType.Byte => (object)_byte,
        RuntimeType.Integer => (object)_integer,
        RuntimeType.Single => (object)_single,
        RuntimeType.Char => (object)_char,
        _ => throw new UnreachableException(),
    };

    public string GetDebuggerDisplay() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Integer => VInt.ToString(CultureInfo.InvariantCulture),
        RuntimeType.Byte => VByte.ToString(CultureInfo.InvariantCulture),
        RuntimeType.Single => VSingle.ToString(CultureInfo.InvariantCulture) + "f",
        RuntimeType.Char => $"'{VChar.Escape()}'",
        _ => throw new UnreachableException(),
    };

    public override int GetHashCode() => Type switch
    {
        RuntimeType.Byte => HashCode.Combine(Type, _byte),
        RuntimeType.Integer => HashCode.Combine(Type, _integer),
        RuntimeType.Single => HashCode.Combine(Type, _single),
        RuntimeType.Char => HashCode.Combine(Type, _char),
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
                Console.Write(_byte);
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
                Console.Write($"\'{_char.Escape()}\'");
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
                TypeCode.UInt16 => new DataItem((ushort)value),
                TypeCode.Int32 => new DataItem((int)value),
                TypeCode.Single => new DataItem((float)value),
                _ => throw new NotImplementedException($"Cannot convert {value} to {typeof(DataItem)}"),
            },
            _ => throw new NotImplementedException($"Cannot convert {value.GetType()} to {typeof(DataItem)}"),
        };
    }

    public static DataItem GetDefaultValue(RuntimeType type) => type switch
    {
        RuntimeType.Byte => new DataItem((byte)0),
        RuntimeType.Integer => new DataItem((int)0),
        RuntimeType.Single => new DataItem((float)0f),
        RuntimeType.Char => new DataItem((char)'\0'),
        _ => DataItem.Null,
    };

    /// <exception cref="InternalException"/>
    public static DataItem GetDefaultValue(Compiler.BasicType type) => type switch
    {
        Compiler.BasicType.Byte => new DataItem((byte)0),
        Compiler.BasicType.Integer => new DataItem((int)0),
        Compiler.BasicType.Float => new DataItem((float)0f),
        Compiler.BasicType.Char => new DataItem((char)'\0'),
        Compiler.BasicType.Void => throw new InternalException($"Type {type} does not have a default value"),
        _ => DataItem.Null,
    };

    public static bool TryShrinkTo8bit(ref DataItem value)
    {
        switch (value.Type)
        {
            case RuntimeType.Byte: return true;

            case RuntimeType.Char:
            {
                if (value._char < byte.MinValue || value._char > byte.MaxValue)
                { return false; }
                value = new DataItem((byte)value._char);
                return true;
            }

            case RuntimeType.Integer:
            {
                if (value._integer < byte.MinValue || value._integer > byte.MaxValue)
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
                if (value._integer < char.MinValue || value._integer > char.MaxValue)
                { return false; }
                value = new DataItem((char)value._integer);
                return true;
            }

            default:
                return false;
        }
    }
}
