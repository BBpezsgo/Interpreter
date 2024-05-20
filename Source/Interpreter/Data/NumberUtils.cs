namespace LanguageCore.Runtime;

public partial struct DataItem :
    IEquatable<DataItem>
{
    public static bool IsZero(DataItem value) => value.Type switch
    {
        RuntimeType.Null => true,
        RuntimeType.Byte => value.Byte == 0,
        RuntimeType.Integer => value.Int == 0,
        RuntimeType.Single => value.Single == 0,
        RuntimeType.Char => value.Char == 0,
        _ => throw new UnreachableException(),
    };

    public override string ToString() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Byte => Byte.ToString(),
        RuntimeType.Integer => Int.ToString(),
        RuntimeType.Char => Char.ToString(),
        RuntimeType.Single => Single.ToString(),
        _ => throw new UnreachableException(),
    };

    public string ToString(IFormatProvider? provider) => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Byte => Convert.ToString(Byte, provider),
        RuntimeType.Integer => Convert.ToString(Int, provider),
        RuntimeType.Char => Convert.ToString(Char, provider),
        RuntimeType.Single => Convert.ToString(Single, provider),
        _ => throw new UnreachableException(),
    };

    /// <inheritdoc/>
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
                RuntimeType.Integer => (value.Int is >= byte.MinValue and <= byte.MaxValue) ? new DataItem((byte)value.Int) : value,
                RuntimeType.Single => value,
                RuntimeType.Char => ((ushort)value.Char is >= byte.MinValue and <= byte.MaxValue) ? new DataItem((byte)value.Char) : value,
                _ => value,
            },
            RuntimeType.Integer => value.Type switch
            {
                RuntimeType.Byte => new DataItem((int)value.Byte),
                RuntimeType.Integer => value,
                RuntimeType.Single => value,
                RuntimeType.Char => new DataItem((int)value.Char),
                _ => value,
            },
            RuntimeType.Single => value.Type switch
            {
                RuntimeType.Byte => new DataItem((float)value.Byte),
                RuntimeType.Integer => new DataItem((float)value.Int),
                RuntimeType.Single => value,
                RuntimeType.Char => new DataItem((float)value.Char),
                _ => value,
            },
            RuntimeType.Char => value.Type switch
            {
                RuntimeType.Byte => new DataItem((char)value.Byte),
                RuntimeType.Integer => (value.Int is >= char.MinValue and <= char.MaxValue) ? new DataItem((char)value.Int) : value,
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
        RuntimeType.Byte => v.Byte != 0,
        RuntimeType.Integer => v.Int != 0,
        RuntimeType.Single => v.Single != 0f,
        RuntimeType.Char => v.Char != 0,
        _ => throw new UnreachableException(),
    };
    public static implicit operator DataItem(bool v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator byte(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => v.Byte,
        RuntimeType.Integer => (byte)v.Int,
        RuntimeType.Char => (byte)v.Char,
        RuntimeType.Single => (byte)v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(byte)}"),
    };
    public static implicit operator DataItem(byte v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ushort(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => v.Byte,
        RuntimeType.Integer => (ushort)v.Int,
        RuntimeType.Char => v.Char,
        RuntimeType.Single => (ushort)v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(ushort)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator int(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => v.Byte,
        RuntimeType.Integer => v.Int,
        RuntimeType.Char => v.Char,
        RuntimeType.Single => (int)v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(int)}"),
    };
    public static implicit operator DataItem(int v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator float(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => v.Byte,
        RuntimeType.Integer => v.Int,
        RuntimeType.Char => v.Char,
        RuntimeType.Single => v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(float)}"),
    };
    public static implicit operator DataItem(float v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator char(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => (char)v.Byte,
        RuntimeType.Integer => (char)v.Int,
        RuntimeType.Char => v.Char,
        RuntimeType.Single => (char)v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(char)}"),
    };
    public static implicit operator DataItem(char v) => new(v);
}
