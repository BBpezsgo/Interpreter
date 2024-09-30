namespace LanguageCore.Compiler;

public partial struct CompiledValue :
    IEquatable<CompiledValue>
{
    public static bool IsZero(CompiledValue value) => value.Type switch
    {
        RuntimeType.Null => true,
        RuntimeType.U8 => value.U8 == default,
        RuntimeType.I8 => value.I8 == default,
        RuntimeType.Char => value.Char == default,
        RuntimeType.I16 => value.I16 == default,
        RuntimeType.U32 => value.U32 == default,
        RuntimeType.I32 => value.I32 == default,
        RuntimeType.F32 => value.F32 == default,
        _ => throw new UnreachableException(),
    };

    public override string ToString() => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.U8 => U8.ToString(),
        RuntimeType.I8 => I8.ToString(),
        RuntimeType.Char => Char.ToString(),
        RuntimeType.I16 => I16.ToString(),
        RuntimeType.U32 => U32.ToString(),
        RuntimeType.I32 => I32.ToString(),
        RuntimeType.F32 => F32.ToString(),
        _ => throw new UnreachableException(),
    };

    public string ToString(IFormatProvider? provider) => Type switch
    {
        RuntimeType.Null => "null",
        RuntimeType.U8 => Convert.ToString(U8, provider),
        RuntimeType.I8 => Convert.ToString(I8, provider),
        RuntimeType.Char => Convert.ToString(Char, provider),
        RuntimeType.I16 => Convert.ToString(I16, provider),
        RuntimeType.U32 => Convert.ToString(U32, provider),
        RuntimeType.I32 => Convert.ToString(I32, provider),
        RuntimeType.F32 => Convert.ToString(F32, provider),
        _ => throw new UnreachableException(),
    };

    public readonly bool TryCast(GeneralType type, out CompiledValue value)
    {
        value = default;
        return type.FinalValue switch
        {
            BuiltinType builtinType => TryCast(builtinType.RuntimeType, out value),
            _ => false
        };
    }

    public readonly bool TryCast(RuntimeType targetType, out CompiledValue value)
    {
#pragma warning disable CS0652
#pragma warning disable IDE0078
        value = targetType switch
        {
            RuntimeType.Null => CompiledValue.Null,
            RuntimeType.U8 => Type switch
            {
                RuntimeType.U8 => (U8 >= byte.MinValue && U8 <= byte.MaxValue) ? new CompiledValue((byte)I8) : this,
                RuntimeType.I8 => (I8 >= byte.MinValue && I8 <= byte.MaxValue) ? new CompiledValue((byte)I8) : this,
                RuntimeType.Char => (Char >= byte.MinValue && Char <= byte.MaxValue) ? new CompiledValue((byte)Char) : this,
                RuntimeType.I16 => (I16 >= byte.MinValue && I16 <= byte.MaxValue) ? new CompiledValue((byte)I16) : this,
                RuntimeType.U32 => (U32 >= byte.MinValue && U32 <= byte.MaxValue) ? new CompiledValue((byte)U32) : this,
                RuntimeType.I32 => (I32 >= byte.MinValue && I32 <= byte.MaxValue) ? new CompiledValue((byte)I32) : this,
                _ => this,
            },
            RuntimeType.I8 => Type switch
            {
                RuntimeType.U8 => (U8 >= sbyte.MinValue && U8 <= sbyte.MaxValue) ? new CompiledValue((sbyte)I8) : this,
                RuntimeType.I8 => (I8 >= sbyte.MinValue && I8 <= sbyte.MaxValue) ? new CompiledValue((sbyte)I8) : this,
                RuntimeType.Char => (Char >= sbyte.MinValue && Char <= sbyte.MaxValue) ? new CompiledValue((sbyte)Char) : this,
                RuntimeType.I16 => (I16 >= sbyte.MinValue && I16 <= sbyte.MaxValue) ? new CompiledValue((sbyte)I16) : this,
                RuntimeType.U32 => (U32 >= sbyte.MinValue && U32 <= sbyte.MaxValue) ? new CompiledValue((sbyte)U32) : this,
                RuntimeType.I32 => (I32 >= sbyte.MinValue && I32 <= sbyte.MaxValue) ? new CompiledValue((sbyte)I32) : this,
                _ => this,
            },
            RuntimeType.Char => Type switch
            {
                RuntimeType.U8 => (U8 >= char.MinValue && U8 <= char.MaxValue) ? new CompiledValue((char)I8) : this,
                RuntimeType.I8 => (I8 >= char.MinValue && I8 <= char.MaxValue) ? new CompiledValue((char)I8) : this,
                RuntimeType.Char => (Char >= char.MinValue && Char <= char.MaxValue) ? new CompiledValue((char)Char) : this,
                RuntimeType.I16 => (I16 >= char.MinValue && I16 <= char.MaxValue) ? new CompiledValue((char)I16) : this,
                RuntimeType.U32 => (U32 >= char.MinValue && U32 <= char.MaxValue) ? new CompiledValue((char)U32) : this,
                RuntimeType.I32 => (I32 >= char.MinValue && I32 <= char.MaxValue) ? new CompiledValue((char)I32) : this,
                _ => this,
            },
            RuntimeType.I16 => Type switch
            {
                RuntimeType.U8 => (U8 >= short.MinValue && U8 <= short.MaxValue) ? new CompiledValue((short)I8) : this,
                RuntimeType.I8 => (I8 >= short.MinValue && I8 <= short.MaxValue) ? new CompiledValue((short)I8) : this,
                RuntimeType.Char => (Char >= short.MinValue && Char <= short.MaxValue) ? new CompiledValue((short)Char) : this,
                RuntimeType.I16 => (I16 >= short.MinValue && I16 <= short.MaxValue) ? new CompiledValue((short)I16) : this,
                RuntimeType.U32 => (U32 >= short.MinValue && U32 <= short.MaxValue) ? new CompiledValue((short)U32) : this,
                RuntimeType.I32 => (I32 >= short.MinValue && I32 <= short.MaxValue) ? new CompiledValue((short)I32) : this,
                _ => this,
            },
            RuntimeType.U32 => Type switch
            {
                RuntimeType.U8 => (U8 >= uint.MinValue && U8 <= uint.MaxValue) ? new CompiledValue((uint)I8) : this,
                RuntimeType.I8 => (I8 >= uint.MinValue && I8 <= uint.MaxValue) ? new CompiledValue((uint)I8) : this,
                RuntimeType.Char => (Char >= uint.MinValue && Char <= uint.MaxValue) ? new CompiledValue((uint)Char) : this,
                RuntimeType.I16 => (I16 >= uint.MinValue && I16 <= uint.MaxValue) ? new CompiledValue((uint)I16) : this,
                RuntimeType.U32 => (U32 >= uint.MinValue && U32 <= uint.MaxValue) ? new CompiledValue((uint)U32) : this,
                RuntimeType.I32 => (I32 >= uint.MinValue && I32 <= uint.MaxValue) ? new CompiledValue((uint)I32) : this,
                _ => this,
            },
            RuntimeType.I32 => Type switch
            {
                RuntimeType.U8 => (U8 >= int.MinValue && U8 <= int.MaxValue) ? new CompiledValue((int)I8) : this,
                RuntimeType.I8 => (I8 >= int.MinValue && I8 <= int.MaxValue) ? new CompiledValue((int)I8) : this,
                RuntimeType.Char => (Char >= int.MinValue && Char <= int.MaxValue) ? new CompiledValue((int)Char) : this,
                RuntimeType.I16 => (I16 >= int.MinValue && I16 <= int.MaxValue) ? new CompiledValue((int)I16) : this,
                RuntimeType.U32 => (U32 >= int.MinValue && U32 <= int.MaxValue) ? new CompiledValue((int)U32) : this,
                RuntimeType.I32 => (I32 >= int.MinValue && I32 <= int.MaxValue) ? new CompiledValue((int)I32) : this,
                _ => this,
            },
            RuntimeType.F32 => this.Type switch
            {
                RuntimeType.U8 => new CompiledValue((float)I8),
                RuntimeType.I8 => new CompiledValue((float)I8),
                RuntimeType.Char => new CompiledValue((float)Char),
                RuntimeType.I16 => new CompiledValue((float)I16),
                RuntimeType.U32 => new CompiledValue((float)U32),
                RuntimeType.I32 => new CompiledValue((float)I32),
                RuntimeType.F32 => new CompiledValue((float)F32),
                _ => this,
            },
            _ => this,
        };
#pragma warning restore IDE0078 // Use pattern matching
#pragma warning restore CS0652 // Comparison to integral constant is useless; the constant is outside the range of the type

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
        RuntimeType.U8 => v.U8 != default,
        RuntimeType.I8 => v.I8 != default,
        RuntimeType.Char => v.Char != default,
        RuntimeType.I16 => v.I16 != default,
        RuntimeType.I32 => v.I32 != default,
        RuntimeType.U32 => v.U32 != default,
        RuntimeType.F32 => v.F32 != default,
        _ => throw new UnreachableException(),
    };
    public static implicit operator CompiledValue(bool v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator byte(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (byte)v.U8,
        RuntimeType.I8 => (byte)v.I8,
        RuntimeType.Char => (byte)v.Char,
        RuntimeType.I16 => (byte)v.I16,
        RuntimeType.I32 => (byte)v.I32,
        RuntimeType.F32 => (byte)v.F32,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(byte)}"),
    };
    public static implicit operator CompiledValue(byte v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator ushort(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (ushort)v.U8,
        RuntimeType.I8 => (ushort)v.U8,
        RuntimeType.Char => (ushort)v.Char,
        RuntimeType.I16 => (ushort)v.I16,
        RuntimeType.U32 => (ushort)v.U32,
        RuntimeType.I32 => (ushort)v.I32,
        RuntimeType.F32 => (ushort)v.F32,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(ushort)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator int(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (int)v.U8,
        RuntimeType.I8 => (int)v.U8,
        RuntimeType.Char => (int)v.Char,
        RuntimeType.I16 => (int)v.I16,
        RuntimeType.U32 => (int)v.U32,
        RuntimeType.I32 => (int)v.I32,
        RuntimeType.F32 => (int)v.F32,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(int)}"),
    };
    public static implicit operator CompiledValue(int v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator float(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (float)v.U8,
        RuntimeType.I8 => (float)v.U8,
        RuntimeType.Char => (float)v.Char,
        RuntimeType.I16 => (float)v.I16,
        RuntimeType.U32 => (float)v.U32,
        RuntimeType.I32 => (float)v.I32,
        RuntimeType.F32 => (float)v.F32,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(float)}"),
    };
    public static implicit operator CompiledValue(float v) => new(v);

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static explicit operator char(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (char)v.U8,
        RuntimeType.I8 => (char)v.U8,
        RuntimeType.Char => (char)v.Char,
        RuntimeType.I16 => (char)v.I16,
        RuntimeType.U32 => (char)v.U32,
        RuntimeType.I32 => (char)v.I32,
        RuntimeType.F32 => (char)v.F32,
        _ => throw new InvalidCastException($"Can't cast {v.Type} to {typeof(char)}"),
    };
    public static implicit operator CompiledValue(char v) => new(v);
}
