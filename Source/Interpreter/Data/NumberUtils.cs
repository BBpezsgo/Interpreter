namespace LanguageCore.Runtime;

public partial struct DataItem :
    IEquatable<DataItem>
{
    public static bool IsZero(DataItem value) => value.Type switch
    {
        RuntimeType.Null => true,
        RuntimeType.Byte => (byte)value._integer == 0,
        RuntimeType.Integer => value._integer == 0,
        RuntimeType.Single => value._single == 0,
        RuntimeType.Char => (char)value._integer == 0,
        _ => throw new UnreachableException(),
    };

    public override string ToString() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Byte => ((byte)_integer).ToString(),
        RuntimeType.Integer => _integer.ToString(),
        RuntimeType.Char => ((char)_integer).ToString(),
        RuntimeType.Single => _single.ToString(),
        _ => throw new UnreachableException(),
    };

    public string ToString(IFormatProvider? provider) => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Byte => Convert.ToString((byte)_integer, provider),
        RuntimeType.Integer => Convert.ToString(_integer, provider),
        RuntimeType.Char => Convert.ToString((char)_integer, provider),
        RuntimeType.Single => Convert.ToString(_single, provider),
        _ => throw new UnreachableException(),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public object ToType(Type conversionType)
    {
        if (conversionType == typeof(byte)) return (byte)this;
        if (conversionType == typeof(sbyte)) return (sbyte)(byte)this;
        if (conversionType == typeof(short)) return (short)(ushort)this;
        if (conversionType == typeof(ushort)) return (ushort)this;
        if (conversionType == typeof(int)) return (int)this;
        if (conversionType == typeof(uint)) return (uint)(int)this;
        if (conversionType == typeof(long)) return (long)(int)this;
        if (conversionType == typeof(ulong)) return (ulong)(int)this;
        if (conversionType == typeof(float)) return (float)this;
        if (conversionType == typeof(decimal)) return (decimal)(float)this;
        if (conversionType == typeof(double)) return (double)(float)this;
        if (conversionType == typeof(bool)) return (bool)this;
        if (conversionType == typeof(char)) return (char)this;

        if (conversionType == typeof(IntPtr))
        {
            if (IntPtr.Size == 4)
            { return new IntPtr((int)this); }
            else
            { return new IntPtr((long)(int)this); }
        }

        if (conversionType == typeof(UIntPtr))
        {
            if (UIntPtr.Size == 4)
            { return new UIntPtr((uint)(int)this); }
            else
            { return new UIntPtr((ulong)(int)this); }
        }

        throw new InvalidCastException($"Can't cast {Type} to {conversionType}");
    }

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public T ToType<T>() => (T)ToType(typeof(T));

    public static bool TryCast(ref DataItem value, RuntimeType targetType)
    {
        value = targetType switch
        {
            RuntimeType.Null => DataItem.Null,
            RuntimeType.Byte => value.Type switch
            {
                RuntimeType.Byte => value,
                RuntimeType.Integer => (value._integer is >= byte.MinValue and <= byte.MaxValue) ? new DataItem((byte)value._integer) : value,
                RuntimeType.Single => value,
                RuntimeType.Char => ((ushort)(char)value._integer is >= byte.MinValue and <= byte.MaxValue) ? new DataItem((byte)(char)value._integer) : value,
                _ => value,
            },
            RuntimeType.Integer => value.Type switch
            {
                RuntimeType.Byte => new DataItem((int)(byte)value._integer),
                RuntimeType.Integer => value,
                RuntimeType.Single => value,
                RuntimeType.Char => new DataItem((int)(char)value._integer),
                _ => value,
            },
            RuntimeType.Single => value.Type switch
            {
                RuntimeType.Byte => new DataItem((float)(byte)value._integer),
                RuntimeType.Integer => new DataItem((float)value._integer),
                RuntimeType.Single => value,
                RuntimeType.Char => new DataItem((float)(char)value._integer),
                _ => value,
            },
            RuntimeType.Char => value.Type switch
            {
                RuntimeType.Byte => new DataItem((char)(byte)value._integer),
                RuntimeType.Integer => (value._integer is >= char.MinValue and <= char.MaxValue) ? new DataItem((char)value._integer) : value,
                RuntimeType.Single => value,
                RuntimeType.Char => value,
                _ => value,
            },
            _ => value,
        };

        return value.Type == targetType;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static bool operator true(DataItem v) => (bool)v;
    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static bool operator false(DataItem v) => !(bool)v;

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator bool(DataItem v) => v.Type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Byte => (byte)v._integer != 0,
        RuntimeType.Integer => v._integer != 0,
        RuntimeType.Single => v._single != 0f,
        RuntimeType.Char => (char)v._integer != 0,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(bool)}"),
    };
    public static implicit operator DataItem(bool v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator byte(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => (byte)v._integer,
        RuntimeType.Integer => (byte)v._integer,
        RuntimeType.Char => (byte)v._integer,
        RuntimeType.Single => (byte)v._single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(byte)}"),
    };
    public static implicit operator DataItem(byte v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ushort(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => (ushort)(byte)v._integer,
        RuntimeType.Integer => (ushort)v._integer,
        RuntimeType.Char => (ushort)v._integer,
        RuntimeType.Single => (ushort)v._single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(ushort)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator int(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => (int)(byte)v._integer,
        RuntimeType.Integer => (int)v._integer,
        RuntimeType.Char => (int)(char)v._integer,
        RuntimeType.Single => (int)v._single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(int)}"),
    };
    public static implicit operator DataItem(int v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator float(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => (float)(byte)v._integer,
        RuntimeType.Integer => (float)v._integer,
        RuntimeType.Char => (float)(char)v._integer,
        RuntimeType.Single => (float)v._single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(float)}"),
    };
    public static implicit operator DataItem(float v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator char(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => (char)(byte)v._integer,
        RuntimeType.Integer => (char)v._integer,
        RuntimeType.Char => (char)(char)v._integer,
        RuntimeType.Single => (char)v._single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(char)}"),
    };
    public static implicit operator DataItem(char v) => new(v);
}
