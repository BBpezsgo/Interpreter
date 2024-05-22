namespace LanguageCore.Compiler;

public partial struct CompiledValue :
    IEquatable<CompiledValue>
{
    public static bool IsZero(CompiledValue value) => value.Type switch
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

    public static bool TryCast(ref CompiledValue value, RuntimeType targetType)
    {
        value = targetType switch
        {
            RuntimeType.Null => CompiledValue.Null,
            RuntimeType.Byte => value.Type switch
            {
                RuntimeType.Byte => value,
                RuntimeType.Integer => (value.Int is >= byte.MinValue and <= byte.MaxValue) ? new CompiledValue((byte)value.Int) : value,
                RuntimeType.Single => value,
                RuntimeType.Char => ((ushort)value.Char is >= byte.MinValue and <= byte.MaxValue) ? new CompiledValue((byte)value.Char) : value,
                _ => value,
            },
            RuntimeType.Integer => value.Type switch
            {
                RuntimeType.Byte => new CompiledValue((int)value.Byte),
                RuntimeType.Integer => value,
                RuntimeType.Single => value,
                RuntimeType.Char => new CompiledValue((int)value.Char),
                _ => value,
            },
            RuntimeType.Single => value.Type switch
            {
                RuntimeType.Byte => new CompiledValue((float)value.Byte),
                RuntimeType.Integer => new CompiledValue((float)value.Int),
                RuntimeType.Single => value,
                RuntimeType.Char => new CompiledValue((float)value.Char),
                _ => value,
            },
            RuntimeType.Char => value.Type switch
            {
                RuntimeType.Byte => new CompiledValue((char)value.Byte),
                RuntimeType.Integer => (value.Int is >= char.MinValue and <= char.MaxValue) ? new CompiledValue((char)value.Int) : value,
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
    public static bool operator true(CompiledValue v) => (bool)v;
    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static bool operator false(CompiledValue v) => !(bool)v;

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator bool(CompiledValue v) => v.Type switch
    {
        RuntimeType.Null => false,
        RuntimeType.Byte => v.Byte != 0,
        RuntimeType.Integer => v.Int != 0,
        RuntimeType.Single => v.Single != 0f,
        RuntimeType.Char => v.Char != 0,
        _ => throw new UnreachableException(),
    };
    public static implicit operator CompiledValue(bool v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator byte(CompiledValue v) => v.Type switch
    {
        RuntimeType.Byte => v.Byte,
        RuntimeType.Integer => (byte)v.Int,
        RuntimeType.Char => (byte)v.Char,
        RuntimeType.Single => (byte)v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(byte)}"),
    };
    public static implicit operator CompiledValue(byte v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ushort(CompiledValue v) => v.Type switch
    {
        RuntimeType.Byte => v.Byte,
        RuntimeType.Integer => (ushort)v.Int,
        RuntimeType.Char => v.Char,
        RuntimeType.Single => (ushort)v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(ushort)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator int(CompiledValue v) => v.Type switch
    {
        RuntimeType.Byte => v.Byte,
        RuntimeType.Integer => v.Int,
        RuntimeType.Char => v.Char,
        RuntimeType.Single => (int)v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(int)}"),
    };
    public static implicit operator CompiledValue(int v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator float(CompiledValue v) => v.Type switch
    {
        RuntimeType.Byte => v.Byte,
        RuntimeType.Integer => v.Int,
        RuntimeType.Char => v.Char,
        RuntimeType.Single => v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(float)}"),
    };
    public static implicit operator CompiledValue(float v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator char(CompiledValue v) => v.Type switch
    {
        RuntimeType.Byte => (char)v.Byte,
        RuntimeType.Integer => (char)v.Int,
        RuntimeType.Char => v.Char,
        RuntimeType.Single => (char)v.Single,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(char)}"),
    };
    public static implicit operator CompiledValue(char v) => new(v);
}
