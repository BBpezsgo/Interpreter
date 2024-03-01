using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
[StructLayout(LayoutKind.Explicit)]
public partial struct DataItem
{
    public static DataItem Null => default;

    public readonly RuntimeType Type => type;

    [FieldOffset(0)] readonly RuntimeType type;

    #region Value Fields

    [FieldOffset(1)] byte _byte;
    [FieldOffset(1)] int _integer;
    [FieldOffset(1)] float _single;
    [FieldOffset(1)] char _char;

    #endregion

    public readonly bool IsNull => type == RuntimeType.Null;

    #region Value Properties

    /// <exception cref="RuntimeException"/>
    public byte VByte
    {
        readonly get => Type switch
        {
            RuntimeType.Byte => _byte,
            _ => throw new RuntimeException($"Can't cast {Type} to byte")
        };
        set => _byte = value;
    }
    /// <exception cref="RuntimeException"/>
    public int VInt
    {
        readonly get => Type switch
        {
            RuntimeType.Integer => _integer,
            _ => throw new RuntimeException($"Can't cast {Type} to integer")
        };
        set => _integer = value;
    }
    /// <exception cref="RuntimeException"/>
    public float VSingle
    {
        readonly get => Type switch
        {
            RuntimeType.Single => _single,
            _ => throw new RuntimeException($"Can't cast {Type} to float")
        };
        set => _single = value;
    }
    /// <exception cref="RuntimeException"/>
    public char VChar
    {
        readonly get => Type switch
        {
            RuntimeType.Char => _char,
            RuntimeType.Integer => (char)_integer,
            RuntimeType.Byte => (char)_integer,
            _ => throw new RuntimeException($"Can't cast {Type} to char")
        };
        set => _char = value;
    }

    #endregion

    #region Constructors

    DataItem(RuntimeType type)
    {
        this.type = type;

        this._integer = default;
        this._byte = default;
        this._single = default;
        this._char = default;
    }

    public DataItem(int value) : this(RuntimeType.Integer)
    { this._integer = value; }
    public DataItem(byte value) : this(RuntimeType.Byte)
    { this._byte = value; }
    public DataItem(float value) : this(RuntimeType.Single)
    { this._single = value; }
    public DataItem(char value) : this(RuntimeType.Char)
    { this._char = value; }
    public DataItem(bool value) : this(value ? 1 : 0)
    { }

    #endregion

    public readonly int? Integer => Type switch
    {
        RuntimeType.Byte => this.VByte,
        RuntimeType.Integer => this.VInt,
        RuntimeType.Char => this.VChar,
        RuntimeType.Single => null,
        RuntimeType.Null => null,
        _ => throw new UnreachableException(),
    };

    public readonly byte? Byte
    {
        get
        {
            switch (type)
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

    public readonly object? GetValue() => type switch
    {
        RuntimeType.Null => (object?)null,
        RuntimeType.Byte => (object)_byte,
        RuntimeType.Integer => (object)_integer,
        RuntimeType.Single => (object)_single,
        RuntimeType.Char => (object)_char,
        _ => throw new UnreachableException(),
    };

    public readonly string GetDebuggerDisplay() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Integer => VInt.ToString(CultureInfo.InvariantCulture),
        RuntimeType.Byte => VByte.ToString(CultureInfo.InvariantCulture),
        RuntimeType.Single => VSingle.ToString(CultureInfo.InvariantCulture) + "f",
        RuntimeType.Char => $"'{VChar.Escape()}'",
        _ => throw new UnreachableException(),
    };

    public override readonly int GetHashCode() => Type switch
    {
        RuntimeType.Byte => HashCode.Combine(Type, _byte),
        RuntimeType.Integer => HashCode.Combine(Type, _integer),
        RuntimeType.Single => HashCode.Combine(Type, _single),
        RuntimeType.Char => HashCode.Combine(Type, _char),
        _ => throw new UnreachableException(),
    };

    public readonly void DebugPrint()
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

        if (value is byte @byte)
        { return new DataItem(@byte); }

        if (value is int @int)
        { return new DataItem(@int); }

        if (value is float @float)
        { return new DataItem(@float); }

        if (value is bool @bool)
        { return new DataItem(@bool); }

        if (value is char @char)
        { return new DataItem(@char); }

        if (value is Enum @enum)
        {
            TypeCode underlyingType = @enum.GetTypeCode();
            object enumValue = underlyingType switch
            {
                TypeCode.Empty => throw new NotImplementedException(),
                TypeCode.Object => throw new NotImplementedException(),
                TypeCode.DBNull => throw new NotImplementedException(),
                TypeCode.Boolean => (bool)value,
                TypeCode.Char => (char)value,
                TypeCode.SByte => (sbyte)value,
                TypeCode.Byte => (byte)value,
                TypeCode.Int16 => (short)value,
                TypeCode.UInt16 => (ushort)value,
                TypeCode.Int32 => (int)value,
                TypeCode.UInt32 => (uint)value,
                TypeCode.Int64 => (long)value,
                TypeCode.UInt64 => (ulong)value,
                TypeCode.Single => (float)value,
                TypeCode.Double => (double)value,
                TypeCode.Decimal => (decimal)value,
                TypeCode.DateTime => (DateTime)value,
                TypeCode.String => (string)value,
                _ => throw new UnreachableException(),
            };
            return DataItem.GetValue(enumValue);
        }

        throw new NotImplementedException($"Type conversion for type {value.GetType()} not implemented");
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
        Compiler.BasicType.Void => throw new InternalException($"Type \"{type}\" does not have a default value"),
        _ => DataItem.Null,
    };

    public static bool TryShrinkToByte(DataItem value, out DataItem result)
    {
        switch (value.type)
        {
            case RuntimeType.Byte:
                result = value;
                return true;
            case RuntimeType.Integer:
                if (value._integer < byte.MinValue || value._integer > byte.MaxValue)
                {
                    result = default;
                    return false;
                }
                result = new DataItem((byte)value._integer);
                return true;
            default:
                result = default;
                return false;
        }
    }

    public static bool TryShrinkToByte(ref DataItem value)
    {
        switch (value.type)
        {
            case RuntimeType.Byte:
                return true;
            case RuntimeType.Integer:
                if (value._integer < byte.MinValue || value._integer > byte.MaxValue)
                {
                    return false;
                }
                value = new DataItem((byte)value._integer);
                return true;
            default:
                return false;
        }
    }
}
