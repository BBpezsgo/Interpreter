namespace LanguageCore.Runtime;

public partial struct DataItem :
    IComparable,
    IComparable<DataItem>,
    IEquatable<DataItem>
{
    public static bool IsZero(DataItem value) => value.type switch
    {
        RuntimeType.Null => true,
        RuntimeType.Byte => value._byte == 0,
        RuntimeType.Integer => value._integer == 0,
        RuntimeType.Single => value._single == 0,
        RuntimeType.Char => value._char == 0,
        _ => throw new UnreachableException(),
    };

    public readonly int CompareTo(object? obj) => obj is not DataItem other ? 0 : CompareTo(other);
    public readonly int CompareTo(DataItem other)
    {
        DataItem self = this;
        DataItem.MakeSameType(ref self, ref other);
        return self.type switch
        {
            RuntimeType.Null => 0,
            RuntimeType.Byte => self._byte.CompareTo(other._byte),
            RuntimeType.Integer => self._integer.CompareTo(other._integer),
            RuntimeType.Single => self._single.CompareTo(other._single),
            RuntimeType.Char => self._char.CompareTo(other._char),
            _ => 0,
        };
    }

    public readonly TypeCode GetTypeCode() => type switch
    {
        RuntimeType.Null => TypeCode.Empty,
        RuntimeType.Byte => TypeCode.Byte,
        RuntimeType.Integer => TypeCode.Int32,
        RuntimeType.Single => TypeCode.Single,
        RuntimeType.Char => TypeCode.UInt16,
        _ => throw new UnreachableException(),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    [DoesNotReturn]
    public readonly DateTime ToDateTime(IFormatProvider? provider) => throw new InvalidCastException($"Can't cast {type} to {nameof(DateTime)}");

    public override readonly string ToString() => type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Byte => _byte.ToString(),
        RuntimeType.Integer => _integer.ToString(),
        RuntimeType.Char => _char.ToString(),
        RuntimeType.Single => _single.ToString(),
        _ => throw new UnreachableException(),
    };

    public readonly string ToString(IFormatProvider? provider) => type switch
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
    public readonly object ToType(Type conversionType, IFormatProvider? provider)
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
        if (conversionType == typeof(DateTime)) return ToDateTime(provider);

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

        throw new InvalidCastException($"Can't cast {type} to {conversionType}");
    }
    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly object ToType(TypeCode conversionType, IFormatProvider? provider) => conversionType switch
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
        TypeCode.Empty => throw new InvalidCastException($"Can't cast {type} to null"),
        TypeCode.Object => throw new InvalidCastException($"Can't cast {type} to {nameof(Object)}"),
        TypeCode.DBNull => throw new InvalidCastException($"Can't cast {type} to {nameof(DBNull)}"),
        TypeCode.DateTime => ToDateTime(provider),
        TypeCode.String => throw new InvalidCastException($"Can't cast {type} to {nameof(String)}"),
        _ => throw new InvalidCastException($"Can't cast {type} to {conversionType}")
    };
    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly T ToType<T>(IFormatProvider? provider) => (T)ToType(typeof(T), provider);

    public static void Cast(ref DataItem value, RuntimeType targetType)
    {
        value = targetType switch
        {
            RuntimeType.Byte => value.Type switch
            {
                RuntimeType.Byte => new DataItem((byte)value.VByte),
                RuntimeType.Integer => new DataItem((byte)(value.VInt % byte.MaxValue)),
                RuntimeType.Single => new DataItem((byte)value.VSingle),
                RuntimeType.Char => new DataItem((byte)value.VChar),
                _ => throw new InvalidCastException($"Can't cast {value.type} to {targetType}"),
            },
            RuntimeType.Integer => value.Type switch
            {
                RuntimeType.Byte => new DataItem((int)value.VByte),
                RuntimeType.Integer => new DataItem((int)value.VInt),
                RuntimeType.Single => new DataItem((int)value.VSingle),
                RuntimeType.Char => new DataItem((int)value.VChar),
                _ => throw new InvalidCastException($"Can't cast {value.type} to {targetType}"),
            },
            RuntimeType.Single => value.Type switch
            {
                RuntimeType.Byte => new DataItem((float)value.VByte),
                RuntimeType.Integer => new DataItem((float)value.VInt),
                RuntimeType.Single => new DataItem((float)value.VSingle),
                RuntimeType.Char => new DataItem((float)value.VChar),
                _ => throw new InvalidCastException($"Can't cast {value.type} to {targetType}"),
            },
            RuntimeType.Char => value.Type switch
            {
                RuntimeType.Byte => new DataItem((char)value.VByte),
                RuntimeType.Integer => new DataItem((char)value.VInt),
                RuntimeType.Single => new DataItem((char)value.VSingle),
                RuntimeType.Char => new DataItem((char)value.VChar),
                _ => throw new InvalidCastException($"Can't cast {value.type} to {targetType}"),
            },
            _ => throw new InvalidCastException($"Can't cast {value.type} to {targetType}"),
        };
    }
    public static bool TryCast(ref DataItem value, RuntimeType targetType)
    {
        switch (targetType)
        {
            case RuntimeType.Byte:
                switch (value.Type)
                {
                    case RuntimeType.Byte:
                        return true;
                    case RuntimeType.Integer:
                        if (value.VInt >= byte.MinValue && value.VInt <= byte.MaxValue)
                        {
                            value = new DataItem((byte)value.VInt);
                            return true;
                        }
                        return false;
                    case RuntimeType.Single:
                        return false;
                    case RuntimeType.Char:
                        if (value.VChar >= byte.MinValue && value.VChar <= byte.MaxValue)
                        {
                            value = new DataItem((byte)value.VChar);
                            return true;
                        }
                        return false;
                    default:
                        return false;
                }
            case RuntimeType.Integer:
                switch (value.Type)
                {
                    case RuntimeType.Byte:
                        value = new DataItem((int)value.VByte);
                        return true;
                    case RuntimeType.Integer:
                        return true;
                    case RuntimeType.Single:
                        return false;
                    case RuntimeType.Char:
                        value = new DataItem((int)value.VChar);
                        return true;
                    default:
                        return false;
                }
            case RuntimeType.Single:
                switch (value.Type)
                {
                    case RuntimeType.Byte:
                        value = new DataItem((float)value.VByte);
                        return true;
                    case RuntimeType.Integer:
                        value = new DataItem((float)value.VInt);
                        return true;
                    case RuntimeType.Single:
                        return true;
                    case RuntimeType.Char:
                        value = new DataItem((float)value.VChar);
                        return true;
                    default:
                        return false;
                }
            case RuntimeType.Char:
                switch (value.Type)
                {
                    case RuntimeType.Byte:
                        value = new DataItem((char)value.VByte);
                        return true;
                    case RuntimeType.Integer:
                        if (value.VInt >= char.MinValue && value.VInt <= char.MaxValue)
                        {
                            value = new DataItem((char)value.VInt);
                            return true;
                        }
                        return false;
                    case RuntimeType.Single:
                        return false;
                    case RuntimeType.Char:
                        return true;
                    default:
                        return false;
                }
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static bool operator true(DataItem v) => (bool)v;
    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static bool operator false(DataItem v) => !(bool)v;

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator bool(DataItem v) => v.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Byte => v._byte != 0,
        RuntimeType.Integer => v._integer != 0,
        RuntimeType.Single => v._single != 0f,
        RuntimeType.Char => v._char != 0,
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(bool)}"),
    };
    public static implicit operator DataItem(bool v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator byte(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToByte(v._byte),
        RuntimeType.Integer => Convert.ToByte(v._integer),
        RuntimeType.Char => Convert.ToByte(v._char),
        RuntimeType.Single => Convert.ToByte(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(byte)}"),
    };
    public static implicit operator DataItem(byte v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator sbyte(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToSByte(v._byte),
        RuntimeType.Integer => Convert.ToSByte(v._integer),
        RuntimeType.Char => Convert.ToSByte(v._char),
        RuntimeType.Single => Convert.ToSByte(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(sbyte)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator short(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToInt16(v._byte),
        RuntimeType.Integer => Convert.ToInt16(v._integer),
        RuntimeType.Char => Convert.ToInt16(v._char),
        RuntimeType.Single => Convert.ToInt16(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(short)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ushort(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToUInt16(v._byte),
        RuntimeType.Integer => Convert.ToUInt16(v._integer),
        RuntimeType.Char => Convert.ToUInt16(v._char),
        RuntimeType.Single => Convert.ToUInt16(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(ushort)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator int(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToInt32(v._byte),
        RuntimeType.Integer => Convert.ToInt32(v._integer),
        RuntimeType.Char => Convert.ToInt32(v._char),
        RuntimeType.Single => Convert.ToInt32(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(int)}"),
    };
    public static implicit operator DataItem(int v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator uint(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToUInt32(v._byte),
        RuntimeType.Integer => Convert.ToUInt32(v._integer),
        RuntimeType.Char => Convert.ToUInt32(v._char),
        RuntimeType.Single => Convert.ToUInt32(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(uint)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator long(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToInt64(v._byte),
        RuntimeType.Integer => Convert.ToInt64(v._integer),
        RuntimeType.Char => Convert.ToInt64(v._char),
        RuntimeType.Single => Convert.ToInt64(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(long)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ulong(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToUInt64(v._byte),
        RuntimeType.Integer => Convert.ToUInt64(v._integer),
        RuntimeType.Char => Convert.ToUInt16(v._char),
        RuntimeType.Single => Convert.ToUInt64(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(ulong)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator float(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToSingle(v._byte),
        RuntimeType.Integer => Convert.ToSingle(v._integer),
        RuntimeType.Char => Convert.ToSingle(v._char),
        RuntimeType.Single => Convert.ToSingle(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(float)}"),
    };
    public static implicit operator DataItem(float v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator decimal(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToDecimal(v._byte),
        RuntimeType.Integer => Convert.ToDecimal(v._integer),
        RuntimeType.Char => Convert.ToDecimal(v._char),
        RuntimeType.Single => Convert.ToDecimal(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(decimal)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator double(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToDouble(v._byte),
        RuntimeType.Integer => Convert.ToDouble(v._integer),
        RuntimeType.Char => Convert.ToDouble(v._char),
        RuntimeType.Single => Convert.ToDouble(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(double)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator char(DataItem v) => v.type switch
    {
        RuntimeType.Byte => Convert.ToChar(v._byte),
        RuntimeType.Integer => Convert.ToChar(v._integer),
        RuntimeType.Char => Convert.ToChar(v._char),
        RuntimeType.Single => Convert.ToChar(v._single),
        _ => throw new InvalidCastException($"Can't cast {v.type} to {typeof(char)}"),
    };
    public static implicit operator DataItem(char v) => new(v);
}
