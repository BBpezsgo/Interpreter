namespace LanguageCore.Runtime;

public partial struct DataItem :
    IComparable,
    IComparable<DataItem>,
    IEquatable<DataItem>
{
    public static bool IsZero(DataItem value) => value.Type switch
    {
        RuntimeType.Null => true,
        RuntimeType.Byte => value._byte == 0,
        RuntimeType.Integer => value._integer == 0,
        RuntimeType.Single => value._single == 0,
        RuntimeType.Char => value._char == 0,
        _ => throw new UnreachableException(),
    };

    public int CompareTo(object? obj) => obj is not DataItem other ? 0 : CompareTo(other);
    public int CompareTo(DataItem other)
    {
        DataItem self = this;
        DataItem.MakeSameType(ref self, ref other);
        return self.Type switch
        {
            RuntimeType.Null => 0,
            RuntimeType.Byte => self._byte.CompareTo(other._byte),
            RuntimeType.Integer => self._integer.CompareTo(other._integer),
            RuntimeType.Single => self._single.CompareTo(other._single),
            RuntimeType.Char => self._char.CompareTo(other._char),
            _ => 0,
        };
    }

    public TypeCode GetTypeCode() => Type switch
    {
        RuntimeType.Null => TypeCode.Empty,
        RuntimeType.Byte => TypeCode.Byte,
        RuntimeType.Integer => TypeCode.Int32,
        RuntimeType.Single => TypeCode.Single,
        RuntimeType.Char => TypeCode.UInt16,
        _ => throw new UnreachableException(),
    };

    public override string ToString() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Byte => _byte.ToString(),
        RuntimeType.Integer => _integer.ToString(),
        RuntimeType.Char => _char.ToString(),
        RuntimeType.Single => _single.ToString(),
        _ => throw new UnreachableException(),
    };

    public string ToString(IFormatProvider? provider) => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Byte => Convert.ToString(_byte, provider),
        RuntimeType.Integer => Convert.ToString(_integer, provider),
        RuntimeType.Char => Convert.ToString(_char, provider),
        RuntimeType.Single => Convert.ToString(_single, provider),
        _ => throw new UnreachableException(),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public object ToType(Type conversionType)
    {
        if (conversionType == typeof(byte)) return (byte)this;
        if (conversionType == typeof(sbyte)) return (sbyte)this;
        if (conversionType == typeof(short)) return (short)this;
        if (conversionType == typeof(ushort)) return (ushort)this;
        if (conversionType == typeof(int)) return (int)this;
        if (conversionType == typeof(uint)) return (uint)this;
        if (conversionType == typeof(long)) return (long)this;
        if (conversionType == typeof(ulong)) return (ulong)this;
        if (conversionType == typeof(float)) return (float)this;
        if (conversionType == typeof(decimal)) return (decimal)this;
        if (conversionType == typeof(double)) return (double)this;
        if (conversionType == typeof(bool)) return (bool)this;
        if (conversionType == typeof(char)) return (char)this;

        if (conversionType == typeof(IntPtr))
        {
            if (IntPtr.Size == 4)
            { return new IntPtr((int)this); }
            else
            { return new IntPtr((long)this); }
        }

        if (conversionType == typeof(UIntPtr))
        {
            if (UIntPtr.Size == 4)
            { return new UIntPtr((uint)this); }
            else
            { return new UIntPtr((ulong)this); }
        }

        throw new InvalidCastException($"Can't cast {Type} to {conversionType}");
    }

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public object ToType(TypeCode conversionType) => conversionType switch
    {
        TypeCode.Byte => (byte)this,
        TypeCode.SByte => (sbyte)this,
        TypeCode.Int16 => (short)this,
        TypeCode.UInt16 => (ushort)this,
        TypeCode.Int32 => (int)this,
        TypeCode.UInt32 => (uint)this,
        TypeCode.Int64 => (long)this,
        TypeCode.UInt64 => (ulong)this,
        TypeCode.Single => (float)this,
        TypeCode.Decimal => (decimal)this,
        TypeCode.Double => (double)this,
        TypeCode.Boolean => (bool)this,
        TypeCode.Char => (char)this,
        TypeCode.Empty => throw new InvalidCastException($"Can't cast {Type} to null"),
        TypeCode.Object => throw new InvalidCastException($"Can't cast {Type} to {nameof(Object)}"),
        TypeCode.DBNull => throw new InvalidCastException($"Can't cast {Type} to {nameof(DBNull)}"),
        TypeCode.DateTime => throw new InvalidCastException($"Can't cast {Type} to {nameof(DateTime)}"),
        TypeCode.String => throw new InvalidCastException($"Can't cast {Type} to {nameof(String)}"),
        _ => throw new InvalidCastException($"Can't cast {Type} to {conversionType}")
    };

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
                RuntimeType.Integer => (value.VInt is >= byte.MinValue and <= byte.MaxValue) ? new DataItem((byte)value.VInt) : value,
                RuntimeType.Single => value,
                RuntimeType.Char => ((ushort)value.VChar is >= byte.MinValue and <= byte.MaxValue) ? new DataItem((byte)value.VChar) : value,
                _ => value,
            },
            RuntimeType.Integer => value.Type switch
            {
                RuntimeType.Byte => new DataItem(value.VByte),
                RuntimeType.Integer => value,
                RuntimeType.Single => value,
                RuntimeType.Char => new DataItem((byte)value.VChar),
                _ => value,
            },
            RuntimeType.Single => value.Type switch
            {
                RuntimeType.Byte => new DataItem((float)value.VByte),
                RuntimeType.Integer => new DataItem((float)value.VInt),
                RuntimeType.Single => value,
                RuntimeType.Char => new DataItem((float)value.VChar),
                _ => value,
            },
            RuntimeType.Char => value.Type switch
            {
                RuntimeType.Byte => new DataItem((char)value.VByte),
                RuntimeType.Integer => (value.VInt is >= char.MinValue and <= char.MaxValue) ? new DataItem((char)value.VInt) : value,
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
        RuntimeType.Byte => v._byte != 0,
        RuntimeType.Integer => v._integer != 0,
        RuntimeType.Single => v._single != 0f,
        RuntimeType.Char => v._char != 0,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(bool)}"),
    };
    public static implicit operator DataItem(bool v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator byte(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToByte(v._byte),
        RuntimeType.Integer => Convert.ToByte(v._integer),
        RuntimeType.Char => Convert.ToByte(v._char),
        RuntimeType.Single => Convert.ToByte(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(byte)}"),
    };
    public static implicit operator DataItem(byte v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator sbyte(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToSByte(v._byte),
        RuntimeType.Integer => Convert.ToSByte(v._integer),
        RuntimeType.Char => Convert.ToSByte(v._char),
        RuntimeType.Single => Convert.ToSByte(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(sbyte)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator short(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToInt16(v._byte),
        RuntimeType.Integer => Convert.ToInt16(v._integer),
        RuntimeType.Char => Convert.ToInt16(v._char),
        RuntimeType.Single => Convert.ToInt16(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(short)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ushort(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToUInt16(v._byte),
        RuntimeType.Integer => Convert.ToUInt16(v._integer),
        RuntimeType.Char => Convert.ToUInt16(v._char),
        RuntimeType.Single => Convert.ToUInt16(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(ushort)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator int(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToInt32(v._byte),
        RuntimeType.Integer => Convert.ToInt32(v._integer),
        RuntimeType.Char => Convert.ToInt32(v._char),
        RuntimeType.Single => Convert.ToInt32(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(int)}"),
    };
    public static implicit operator DataItem(int v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator uint(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToUInt32(v._byte),
        RuntimeType.Integer => Convert.ToUInt32(v._integer),
        RuntimeType.Char => Convert.ToUInt32(v._char),
        RuntimeType.Single => Convert.ToUInt32(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(uint)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator long(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToInt64(v._byte),
        RuntimeType.Integer => Convert.ToInt64(v._integer),
        RuntimeType.Char => Convert.ToInt64(v._char),
        RuntimeType.Single => Convert.ToInt64(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(long)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ulong(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToUInt64(v._byte),
        RuntimeType.Integer => Convert.ToUInt64(v._integer),
        RuntimeType.Char => Convert.ToUInt16(v._char),
        RuntimeType.Single => Convert.ToUInt64(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(ulong)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator float(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToSingle(v._byte),
        RuntimeType.Integer => Convert.ToSingle(v._integer),
        RuntimeType.Char => Convert.ToSingle(v._char),
        RuntimeType.Single => Convert.ToSingle(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(float)}"),
    };
    public static implicit operator DataItem(float v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator decimal(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToDecimal(v._byte),
        RuntimeType.Integer => Convert.ToDecimal(v._integer),
        RuntimeType.Char => Convert.ToDecimal(v._char),
        RuntimeType.Single => Convert.ToDecimal(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(decimal)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator double(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToDouble(v._byte),
        RuntimeType.Integer => Convert.ToDouble(v._integer),
        RuntimeType.Char => Convert.ToDouble(v._char),
        RuntimeType.Single => Convert.ToDouble(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(double)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator char(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => Convert.ToChar(v._byte),
        RuntimeType.Integer => Convert.ToChar(v._integer),
        RuntimeType.Char => Convert.ToChar(v._char),
        RuntimeType.Single => Convert.ToChar(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(char)}"),
    };
    public static implicit operator DataItem(char v) => new(v);
}
