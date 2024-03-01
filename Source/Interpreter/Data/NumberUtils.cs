namespace LanguageCore.Runtime;

public partial struct DataItem :
    INumberBase<DataItem>,
    IBinaryNumber<DataItem>,
    INumber<DataItem>,
    IUnsignedNumber<DataItem>,
    ISignedNumber<DataItem>,
    IComparable,
    IConvertible,
    ISpanFormattable,
    IComparable<DataItem>,
    IEquatable<DataItem>,
    IBinaryInteger<DataItem>
{
    public static DataItem One => new(1);
    public static DataItem NegativeOne => new(-1);
    public static DataItem Zero => new(0);
    public static int Radix => 10;

    public static DataItem AdditiveIdentity => 0;
    public static DataItem MultiplicativeIdentity => 1;

    public static DataItem Abs(DataItem value) => value.type switch
    {
        RuntimeType.Null => value,
        RuntimeType.Byte => value,
        RuntimeType.Integer => new DataItem(Math.Abs(value._integer)),
        RuntimeType.Single => new DataItem(Math.Abs(value._single)),
        RuntimeType.Char => value,
        _ => throw new UnreachableException(),
    };

    /// <exception cref="System.NotSupportedException"/>
    [DoesNotReturn]
    public static bool IsCanonical(DataItem value) => throw new System.NotSupportedException("What is IsCanonical???");
    public static bool IsComplexNumber(DataItem value) => false;
    public static bool IsImaginaryNumber(DataItem value) => false;
    public static bool IsNormal(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Single => float.IsNormal(value._single),
        _ => true,
    };
    public static bool IsRealNumber(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Single => float.IsRealNumber(value._single),
        _ => true,
    };
    /// <exception cref="NotImplementedException"/>
    public static bool IsSubnormal(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Single => float.IsSubnormal(value._single),
        _ => throw new NotImplementedException(),
    };

    public static bool IsEvenInteger(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Byte => byte.IsEvenInteger(value._byte),
        RuntimeType.Integer => int.IsEvenInteger(value._integer),
        RuntimeType.Single => float.IsEvenInteger(value._single),
        RuntimeType.Char => ushort.IsEvenInteger(value._char),
        _ => throw new UnreachableException(),
    };
    public static bool IsOddInteger(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Byte => byte.IsOddInteger(value._byte),
        RuntimeType.Integer => int.IsOddInteger(value._integer),
        RuntimeType.Single => float.IsOddInteger(value._single),
        RuntimeType.Char => ushort.IsOddInteger(value._char),
        _ => throw new UnreachableException(),
    };

    public static bool IsFinite(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Single => float.IsFinite(value._single),
        _ => true,
    };
    public static bool IsInfinity(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Single => float.IsInfinity(value._single),
        _ => false,
    };

    public static bool IsInteger(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Single => float.IsInfinity(value._single),
        _ => true,
    };

    public static bool IsNaN(DataItem value) => value.type switch
    {
        RuntimeType.Null => true,
        RuntimeType.Single => float.IsNaN(value._single),
        _ => false,
    };
    public static bool IsZero(DataItem value) => value.type switch
    {
        RuntimeType.Null => true,
        RuntimeType.Byte => value._byte == 0,
        RuntimeType.Integer => value._integer == 0,
        RuntimeType.Single => value._single == 0,
        RuntimeType.Char => value._char == 0,
        _ => throw new UnreachableException(),
    };

    public static bool IsNegative(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Integer => int.IsNegative(value._integer),
        RuntimeType.Single => float.IsNegative(value._single),
        _ => false,
    };
    public static bool IsPositive(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Integer => int.IsPositive(value._integer),
        RuntimeType.Single => float.IsPositive(value._single),
        _ => true,
    };

    public static bool IsNegativeInfinity(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Single => float.IsNegativeInfinity(value._single),
        _ => false,
    };
    public static bool IsPositiveInfinity(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Single => float.IsPositiveInfinity(value._single),
        _ => false,
    };

    public static bool IsPow2(DataItem value) => value.type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Byte => byte.IsPow2(value._byte),
        RuntimeType.Integer => int.IsPow2(value._integer),
        RuntimeType.Single => float.IsPow2(value._single),
        RuntimeType.Char => ushort.IsPow2(value._char),
        _ => throw new UnreachableException(),
    };

    public static DataItem Log2(DataItem value) => value.type switch
    {
        RuntimeType.Null => value,
        RuntimeType.Byte => new DataItem((byte)byte.Log2(value._byte)),
        RuntimeType.Integer => new DataItem((int)int.Log2(value._integer)),
        RuntimeType.Single => new DataItem((float)float.Log2(value._single)),
        RuntimeType.Char => new DataItem((ushort)ushort.Log2(value._char)),
        _ => throw new UnreachableException(),
    };

    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    public readonly int CompareTo(object? obj) => obj is not DataItem other ? 0 : CompareTo(other);
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
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

    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    public static DataItem MaxMagnitude(DataItem x, DataItem y)
    {
        DataItem.MakeSameType(ref x, ref y);
        return x.type switch
        {
            RuntimeType.Null => DataItem.Null,
            RuntimeType.Byte => new DataItem(byte.Max(x._byte, y._byte)),
            RuntimeType.Integer => new DataItem(int.MaxMagnitude(x._integer, y._integer)),
            RuntimeType.Single => new DataItem(float.MaxMagnitude(x._single, y._single)),
            RuntimeType.Char => new DataItem(ushort.Max(x._char, y._char)),
            _ => throw new UnreachableException(),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    public static DataItem MaxMagnitudeNumber(DataItem x, DataItem y)
    {
        DataItem.MakeSameType(ref x, ref y);
        return x.type switch
        {
            RuntimeType.Null => DataItem.Null,
            RuntimeType.Byte => new DataItem(byte.Max(x._byte, y._byte)),
            RuntimeType.Integer => new DataItem(int.MaxMagnitude(x._integer, y._integer)),
            RuntimeType.Single => new DataItem(float.MaxMagnitudeNumber(x._single, y._single)),
            RuntimeType.Char => new DataItem(ushort.Max(x._char, y._char)),
            _ => throw new UnreachableException(),
        };
    }

    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    public static DataItem MinMagnitude(DataItem x, DataItem y)
    {
        DataItem.MakeSameType(ref x, ref y);
        return x.type switch
        {
            RuntimeType.Null => DataItem.Null,
            RuntimeType.Byte => new DataItem(byte.Min(x._byte, y._byte)),
            RuntimeType.Integer => new DataItem(int.MinMagnitude(x._integer, y._integer)),
            RuntimeType.Single => new DataItem(float.MinMagnitude(x._single, y._single)),
            RuntimeType.Char => new DataItem(ushort.Min(x._char, y._char)),
            _ => throw new UnreachableException(),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    public static DataItem MinMagnitudeNumber(DataItem x, DataItem y)
    {
        DataItem.MakeSameType(ref x, ref y);
        return x.type switch
        {
            RuntimeType.Null => DataItem.Null,
            RuntimeType.Byte => new DataItem(byte.Min(x._byte, y._byte)),
            RuntimeType.Integer => new DataItem(int.MinMagnitude(x._integer, y._integer)),
            RuntimeType.Single => new DataItem(float.MinMagnitudeNumber(x._single, y._single)),
            RuntimeType.Char => new DataItem(ushort.Min(x._char, y._char)),
            _ => throw new UnreachableException(),
        };
    }

    public static DataItem Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
    {
        if (int.TryParse(s, style, provider, out int integer))
        { return new DataItem(integer); }

        if (float.TryParse(s, style, provider, out float @float))
        { return new DataItem(@float); }

        if (s.Length == 1)
        { return new DataItem(s[0]); }

        throw new FormatException();
    }
    public static DataItem Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (int.TryParse(s, provider, out int integer))
        { return new DataItem(integer); }

        if (float.TryParse(s, provider, out float @float))
        { return new DataItem(@float); }

        if (s.Length == 1)
        { return new DataItem(s[0]); }

        throw new FormatException();
    }

    public static DataItem Parse(string s, NumberStyles style, IFormatProvider? provider)
    {
        if (int.TryParse(s, style, provider, out int integer))
        { return new DataItem(integer); }

        if (float.TryParse(s, style, provider, out float @float))
        { return new DataItem(@float); }

        if (char.TryParse(s, out char character))
        { return new DataItem(character); }

        throw new FormatException();
    }
    public static DataItem Parse(string s, IFormatProvider? provider)
    {
        if (int.TryParse(s, provider, out int integer))
        { return new DataItem(integer); }

        if (float.TryParse(s, provider, out float @float))
        { return new DataItem(@float); }

        if (char.TryParse(s, out char character))
        { return new DataItem(character); }

        throw new FormatException();
    }

    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out DataItem result)
    {
        if (int.TryParse(s, style, provider, out int integer))
        {
            result = new DataItem(integer);
            return true;
        }

        if (float.TryParse(s, style, provider, out float @float))
        {
            result = new DataItem(@float);
            return true;
        }

        if (s.Length == 1)
        {
            result = new DataItem(s[0]);
            return true;
        }

        result = default;
        return false;
    }
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out DataItem result)
    {
        if (int.TryParse(s, provider, out int integer))
        {
            result = new DataItem(integer);
            return true;
        }

        if (float.TryParse(s, provider, out float @float))
        {
            result = new DataItem(@float);
            return true;
        }

        if (s.Length == 1)
        {
            result = new DataItem(s[0]);
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out DataItem result)
    {
        if (int.TryParse(s, style, provider, out int integer))
        {
            result = new DataItem(integer);
            return true;
        }

        if (float.TryParse(s, style, provider, out float @float))
        {
            result = new DataItem(@float);
            return true;
        }

        if (char.TryParse(s, out char character))
        {
            result = new DataItem(character);
            return true;
        }

        result = default;
        return false;
    }
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out DataItem result)
    {
        if (int.TryParse(s, provider, out int integer))
        {
            result = new DataItem(integer);
            return true;
        }

        if (float.TryParse(s, provider, out float @float))
        {
            result = new DataItem(@float);
            return true;
        }

        if (char.TryParse(s, out char character))
        {
            result = new DataItem(character);
            return true;
        }

        result = default;
        return false;
    }

    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = default;
        return type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => _byte.TryFormat(destination, out charsWritten, format, provider),
            RuntimeType.Integer => _integer.TryFormat(destination, out charsWritten, format, provider),
            RuntimeType.Single => _single.TryFormat(destination, out charsWritten, format, provider),
            RuntimeType.Char => false,
            _ => false,
        };
    }

    static bool INumberBase<DataItem>.TryConvertFromChecked<TOther>(TOther value, out DataItem result) => throw new NotImplementedException();
    static bool INumberBase<DataItem>.TryConvertFromSaturating<TOther>(TOther value, out DataItem result) => throw new NotImplementedException();
    static bool INumberBase<DataItem>.TryConvertFromTruncating<TOther>(TOther value, out DataItem result) => throw new NotImplementedException();
    static bool INumberBase<DataItem>.TryConvertToChecked<TOther>(DataItem value, out TOther result) => throw new NotImplementedException();
    static bool INumberBase<DataItem>.TryConvertToSaturating<TOther>(DataItem value, out TOther result) => throw new NotImplementedException();
    static bool INumberBase<DataItem>.TryConvertToTruncating<TOther>(DataItem value, out TOther result) => throw new NotImplementedException();

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
    public readonly bool ToBoolean(IFormatProvider? provider) => Type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Byte => _byte != 0,
        RuntimeType.Integer => _integer != 0,
        RuntimeType.Single => _single != 0f,
        RuntimeType.Char => _char != 0,
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(Boolean)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly byte ToByte(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToByte(_byte),
        RuntimeType.Integer => Convert.ToByte(_integer),
        RuntimeType.Char => Convert.ToByte(_char),
        RuntimeType.Single => Convert.ToByte(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(Byte)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly char ToChar(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToChar(_byte),
        RuntimeType.Integer => Convert.ToChar(_integer),
        RuntimeType.Char => Convert.ToChar(_char),
        RuntimeType.Single => Convert.ToChar(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(VChar)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    [DoesNotReturn]
    public readonly DateTime ToDateTime(IFormatProvider? provider) => throw new InvalidCastException($"Can't cast {type} to {nameof(DateTime)}");

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly decimal ToDecimal(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToDecimal(_byte),
        RuntimeType.Integer => Convert.ToDecimal(_integer),
        RuntimeType.Char => Convert.ToDecimal(_char),
        RuntimeType.Single => Convert.ToDecimal(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(Decimal)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public readonly double ToDouble(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToDouble(_byte),
        RuntimeType.Integer => Convert.ToDouble(_integer),
        RuntimeType.Char => Convert.ToDouble(_char),
        RuntimeType.Single => Convert.ToDouble(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(Double)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly short ToInt16(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToInt16(_byte),
        RuntimeType.Integer => Convert.ToInt16(_integer),
        RuntimeType.Char => Convert.ToInt16(_char),
        RuntimeType.Single => Convert.ToInt16(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(Int16)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly int ToInt32(IFormatProvider? provider = null) => type switch
    {
        RuntimeType.Byte => Convert.ToInt32(_byte),
        RuntimeType.Integer => Convert.ToInt32(_integer),
        RuntimeType.Char => Convert.ToInt32(_char),
        RuntimeType.Single => Convert.ToInt32(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(Int32)}"),
    };

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly int RoundToInt32(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => (int)_byte,
        RuntimeType.Integer => (int)_integer,
        RuntimeType.Char => (int)_char,
        RuntimeType.Single => checked((int)MathF.Round(_single)),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(Int32)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly long ToInt64(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToInt64(_byte),
        RuntimeType.Integer => Convert.ToInt64(_integer),
        RuntimeType.Char => Convert.ToInt64(_char),
        RuntimeType.Single => Convert.ToInt64(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(Int64)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly sbyte ToSByte(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToSByte(_byte),
        RuntimeType.Integer => Convert.ToSByte(_integer),
        RuntimeType.Char => Convert.ToSByte(_char),
        RuntimeType.Single => Convert.ToSByte(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(SByte)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public readonly float ToSingle(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToSingle(_byte),
        RuntimeType.Integer => Convert.ToSingle(_integer),
        RuntimeType.Char => Convert.ToSingle(_char),
        RuntimeType.Single => Convert.ToSingle(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(VSingle)}"),
    };

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

    public readonly string ToString(string? format, IFormatProvider? formatProvider) => type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.Byte => _byte.ToString(format, formatProvider),
        RuntimeType.Integer => _integer.ToString(format, formatProvider),
        RuntimeType.Single => _single.ToString(format, formatProvider),
        RuntimeType.Char => _char.ToString(formatProvider),
        _ => throw new UnreachableException(),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly object ToType(Type conversionType, IFormatProvider? provider)
    {
        if (conversionType == typeof(byte)) return ToByte(provider);
        if (conversionType == typeof(sbyte)) return ToSByte(provider);
        if (conversionType == typeof(short)) return ToInt16(provider);
        if (conversionType == typeof(ushort)) return ToUInt16(provider);
        if (conversionType == typeof(int)) return ToInt32(provider);
        if (conversionType == typeof(uint)) return ToUInt32(provider);
        if (conversionType == typeof(long)) return ToInt64(provider);
        if (conversionType == typeof(ulong)) return ToUInt64(provider);
        if (conversionType == typeof(float)) return ToSingle(provider);
        if (conversionType == typeof(decimal)) return ToDecimal(provider);
        if (conversionType == typeof(double)) return ToDouble(provider);
        if (conversionType == typeof(bool)) return ToBoolean(provider);
        if (conversionType == typeof(char)) return ToChar(provider);
        if (conversionType == typeof(DateTime)) return ToDateTime(provider);

        if (conversionType == typeof(IntPtr))
        {
            if (IntPtr.Size == 4)
            { return new IntPtr(ToInt32(provider)); }
            else
            { return new IntPtr(ToInt64(provider)); }
        }

        if (conversionType == typeof(UIntPtr))
        {
            if (UIntPtr.Size == 4)
            { return new UIntPtr(ToUInt32(provider)); }
            else
            { return new UIntPtr(ToUInt64(provider)); }
        }

        throw new InvalidCastException($"Can't cast {type} to {conversionType}");
    }

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly object ToType(TypeCode conversionType, IFormatProvider? provider) => conversionType switch
    {
        TypeCode.Byte => ToByte(provider),
        TypeCode.SByte => ToSByte(provider),
        TypeCode.Int16 => ToInt16(provider),
        TypeCode.UInt16 => ToUInt16(provider),
        TypeCode.Int32 => ToInt32(provider),
        TypeCode.UInt32 => ToUInt32(provider),
        TypeCode.Int64 => ToInt64(provider),
        TypeCode.UInt64 => ToUInt64(provider),
        TypeCode.Single => ToSingle(provider),
        TypeCode.Decimal => ToDecimal(provider),
        TypeCode.Double => ToDouble(provider),
        TypeCode.Boolean => ToBoolean(provider),
        TypeCode.Char => ToChar(provider),
        TypeCode.Empty => throw new InvalidCastException($"Can't cast {type} to null"),
        TypeCode.Object => throw new InvalidCastException($"Can't cast {type} to {nameof(Object)}"),
        TypeCode.DBNull => throw new InvalidCastException($"Can't cast {type} to {nameof(DBNull)}"),
        TypeCode.DateTime => ToDateTime(provider),
        TypeCode.String => throw new InvalidCastException($"Can't cast {type} to {nameof(String)}"),
        _ => throw new InvalidCastException($"Can't cast {type} to {conversionType}")
    };

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly T ToType<T>(IFormatProvider? provider) => (T)ToType(typeof(T), provider);

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly ushort ToUInt16(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToUInt16(_byte),
        RuntimeType.Integer => Convert.ToUInt16(_integer),
        RuntimeType.Char => Convert.ToUInt16(_char),
        RuntimeType.Single => Convert.ToUInt16(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(UInt16)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly uint ToUInt32(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToUInt32(_byte),
        RuntimeType.Integer => Convert.ToUInt32(_integer),
        RuntimeType.Char => Convert.ToUInt32(_char),
        RuntimeType.Single => Convert.ToUInt32(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(System.UInt32)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public readonly ulong ToUInt64(IFormatProvider? provider) => type switch
    {
        RuntimeType.Byte => Convert.ToUInt64(_byte),
        RuntimeType.Integer => Convert.ToUInt64(_integer),
        RuntimeType.Char => Convert.ToUInt16(_char),
        RuntimeType.Single => Convert.ToUInt64(_single),
        _ => throw new InvalidCastException($"Can't cast {type} to {nameof(System.UInt64)}"),
    };

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

    public static bool TryCast(ref DataItem value, RuntimeType? targetType)
        => targetType.HasValue && DataItem.TryCast(ref value, targetType.Value);
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

    public readonly int GetByteCount() => type switch
    {
        RuntimeType.Null => 0,
        RuntimeType.Byte => sizeof(byte),
        RuntimeType.Integer => sizeof(int),
        RuntimeType.Single => sizeof(float),
        RuntimeType.Char => sizeof(char),
        _ => throw new UnreachableException(),
    };
    /// <inheritdoc/>
    /// <exception cref="NotImplementedException"/>
    [DoesNotReturn]
    public readonly int GetShortestBitLength() => throw new NotImplementedException();
    public static DataItem PopCount(DataItem value) => value.type switch
    {
        RuntimeType.Byte => (DataItem)BitOperations.PopCount(value.VByte),
        RuntimeType.Integer => (DataItem)BitOperations.PopCount(unchecked((uint)value.VInt)),
        RuntimeType.Char => (DataItem)BitOperations.PopCount(value._char),
        _ => throw new NotImplementedException($"Can't do {nameof(PopCount)} on type {value.type}"),
    };
    public static DataItem TrailingZeroCount(DataItem value) => value.type switch
    {
        RuntimeType.Byte => (DataItem)BitOperations.TrailingZeroCount(value.VByte),
        RuntimeType.Integer => (DataItem)BitOperations.TrailingZeroCount(unchecked((uint)value.VInt)),
        RuntimeType.Char => (DataItem)BitOperations.TrailingZeroCount(value._char),
        _ => throw new NotImplementedException($"Can't do {nameof(TrailingZeroCount)} on type {value.type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="NotImplementedException"/>
    [DoesNotReturn]
    public static bool TryReadBigEndian(ReadOnlySpan<byte> source, bool isUnsigned, out DataItem value) => throw new NotImplementedException();
    /// <inheritdoc/>
    /// <exception cref="NotImplementedException"/>
    [DoesNotReturn]
    public static bool TryReadLittleEndian(ReadOnlySpan<byte> source, bool isUnsigned, out DataItem value) => throw new NotImplementedException();
    /// <inheritdoc/>
    /// <exception cref="NotImplementedException"/>
    [DoesNotReturn]
    public readonly bool TryWriteBigEndian(Span<byte> destination, out int bytesWritten) => throw new NotImplementedException();
    /// <inheritdoc/>
    /// <exception cref="NotImplementedException"/>
    [DoesNotReturn]
    public readonly bool TryWriteLittleEndian(Span<byte> destination, out int bytesWritten) => throw new NotImplementedException();

    /// <exception cref="InvalidCastException"/>
    public static bool operator true(DataItem v) => v.ToBoolean(null);
    /// <exception cref="InvalidCastException"/>
    public static bool operator false(DataItem v) => !v.ToBoolean(null);

    /// <exception cref="InvalidCastException"/>
    public static explicit operator bool(DataItem v) => v.ToBoolean(null);
    public static implicit operator DataItem(bool v) => new(v);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator byte(DataItem v) => v.ToByte(null);
    public static implicit operator DataItem(byte v) => new(v);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator sbyte(DataItem v) => v.ToSByte(null);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator short(DataItem v) => v.ToInt16(null);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ushort(DataItem v) => v.ToUInt16(null);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator int(DataItem v) => v.ToInt32(null);
    public static implicit operator DataItem(int v) => new(v);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator uint(DataItem v) => v.ToUInt32(null);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator long(DataItem v) => v.ToInt64(null);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ulong(DataItem v) => v.ToUInt64(null);

    /// <exception cref="InvalidCastException"/>
    public static explicit operator float(DataItem v) => v.ToSingle(null);
    public static implicit operator DataItem(float v) => new(v);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator decimal(DataItem v) => v.ToDecimal(null);

    /// <exception cref="InvalidCastException"/>
    public static explicit operator double(DataItem v) => v.ToDouble(null);

    /// <exception cref="OverflowException"/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator char(DataItem v) => v.ToChar(null);
    public static implicit operator DataItem(char v) => new(v);
}
