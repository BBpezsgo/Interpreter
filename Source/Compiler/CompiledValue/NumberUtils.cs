namespace LanguageCore.Compiler;

public partial struct CompiledValue :
    IEquatable<CompiledValue>
{
    public static bool IsZero(CompiledValue value) => value.Type switch
    {
        RuntimeType.Null => true,
        RuntimeType.U8 => value.U8 == default,
        RuntimeType.I8 => value.I8 == default,
        RuntimeType.U16 => value.U16 == default,
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
        RuntimeType.U16 => U16.ToString(),
        RuntimeType.I16 => I16.ToString(),
        RuntimeType.U32 => U32.ToString(),
        RuntimeType.I32 => I32.ToString(),
        RuntimeType.F32 => F32.ToString() + "f",
        _ => throw new UnreachableException(),
    };

    public string? ToStringValue() => Type switch
    {
        RuntimeType.Null => null,
        RuntimeType.U8 => U8.ToString(),
        RuntimeType.I8 => I8.ToString(),
        RuntimeType.U16 => U16.ToString(),
        RuntimeType.I16 => I16.ToString(),
        RuntimeType.U32 => U32.ToString(),
        RuntimeType.I32 => I32.ToString(),
        RuntimeType.F32 => F32.ToString(),
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
                RuntimeType.U16 => (U16 >= byte.MinValue && U16 <= byte.MaxValue) ? new CompiledValue((byte)U16) : this,
                RuntimeType.I16 => (I16 >= byte.MinValue && I16 <= byte.MaxValue) ? new CompiledValue((byte)I16) : this,
                RuntimeType.U32 => (U32 >= byte.MinValue && U32 <= byte.MaxValue) ? new CompiledValue((byte)U32) : this,
                RuntimeType.I32 => (I32 >= byte.MinValue && I32 <= byte.MaxValue) ? new CompiledValue((byte)I32) : this,
                _ => this,
            },
            RuntimeType.I8 => Type switch
            {
                RuntimeType.U8 => (U8 >= sbyte.MinValue && U8 <= sbyte.MaxValue) ? new CompiledValue((sbyte)I8) : this,
                RuntimeType.I8 => (I8 >= sbyte.MinValue && I8 <= sbyte.MaxValue) ? new CompiledValue((sbyte)I8) : this,
                RuntimeType.U16 => (U16 >= sbyte.MinValue && U16 <= sbyte.MaxValue) ? new CompiledValue((sbyte)U16) : this,
                RuntimeType.I16 => (I16 >= sbyte.MinValue && I16 <= sbyte.MaxValue) ? new CompiledValue((sbyte)I16) : this,
                RuntimeType.U32 => (U32 >= sbyte.MinValue && U32 <= sbyte.MaxValue) ? new CompiledValue((sbyte)U32) : this,
                RuntimeType.I32 => (I32 >= sbyte.MinValue && I32 <= sbyte.MaxValue) ? new CompiledValue((sbyte)I32) : this,
                _ => this,
            },
            RuntimeType.U16 => Type switch
            {
                RuntimeType.U8 => (U8 >= ushort.MinValue && U8 <= ushort.MaxValue) ? new CompiledValue((ushort)I8) : this,
                RuntimeType.I8 => (I8 >= ushort.MinValue && I8 <= ushort.MaxValue) ? new CompiledValue((ushort)I8) : this,
                RuntimeType.U16 => (U16 >= ushort.MinValue && U16 <= ushort.MaxValue) ? new CompiledValue((ushort)U16) : this,
                RuntimeType.I16 => (I16 >= ushort.MinValue && I16 <= ushort.MaxValue) ? new CompiledValue((ushort)I16) : this,
                RuntimeType.U32 => (U32 >= ushort.MinValue && U32 <= ushort.MaxValue) ? new CompiledValue((ushort)U32) : this,
                RuntimeType.I32 => (I32 >= ushort.MinValue && I32 <= ushort.MaxValue) ? new CompiledValue((ushort)I32) : this,
                _ => this,
            },
            RuntimeType.I16 => Type switch
            {
                RuntimeType.U8 => (U8 >= short.MinValue && U8 <= short.MaxValue) ? new CompiledValue((short)I8) : this,
                RuntimeType.I8 => (I8 >= short.MinValue && I8 <= short.MaxValue) ? new CompiledValue((short)I8) : this,
                RuntimeType.U16 => (U16 >= short.MinValue && U16 <= short.MaxValue) ? new CompiledValue((short)U16) : this,
                RuntimeType.I16 => (I16 >= short.MinValue && I16 <= short.MaxValue) ? new CompiledValue((short)I16) : this,
                RuntimeType.U32 => (U32 >= short.MinValue && U32 <= short.MaxValue) ? new CompiledValue((short)U32) : this,
                RuntimeType.I32 => (I32 >= short.MinValue && I32 <= short.MaxValue) ? new CompiledValue((short)I32) : this,
                _ => this,
            },
            RuntimeType.U32 => Type switch
            {
                RuntimeType.U8 => (U8 >= uint.MinValue && U8 <= uint.MaxValue) ? new CompiledValue((uint)I8) : this,
                RuntimeType.I8 => (I8 >= uint.MinValue && I8 <= uint.MaxValue) ? new CompiledValue((uint)I8) : this,
                RuntimeType.U16 => (U16 >= uint.MinValue && U16 <= uint.MaxValue) ? new CompiledValue((uint)U16) : this,
                RuntimeType.I16 => (I16 >= uint.MinValue && I16 <= uint.MaxValue) ? new CompiledValue((uint)I16) : this,
                RuntimeType.U32 => (U32 >= uint.MinValue && U32 <= uint.MaxValue) ? new CompiledValue((uint)U32) : this,
                RuntimeType.I32 => (I32 >= uint.MinValue && I32 <= uint.MaxValue) ? new CompiledValue((uint)I32) : this,
                _ => this,
            },
            RuntimeType.I32 => Type switch
            {
                RuntimeType.U8 => (U8 >= int.MinValue && U8 <= int.MaxValue) ? new CompiledValue((int)I8) : this,
                RuntimeType.I8 => (I8 >= int.MinValue && I8 <= int.MaxValue) ? new CompiledValue((int)I8) : this,
                RuntimeType.U16 => (U16 >= int.MinValue && U16 <= int.MaxValue) ? new CompiledValue((int)U16) : this,
                RuntimeType.I16 => (I16 >= int.MinValue && I16 <= int.MaxValue) ? new CompiledValue((int)I16) : this,
                RuntimeType.U32 => (U32 >= int.MinValue && U32 <= int.MaxValue) ? new CompiledValue((int)U32) : this,
                RuntimeType.I32 => (I32 >= int.MinValue && I32 <= int.MaxValue) ? new CompiledValue((int)I32) : this,
                _ => this,
            },
            RuntimeType.F32 => this.Type switch
            {
                RuntimeType.U8 => new CompiledValue((float)I8),
                RuntimeType.I8 => new CompiledValue((float)I8),
                RuntimeType.U16 => new CompiledValue((float)U16),
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
    public static bool operator true(CompiledValue v) => (bool)v;
    /// <inheritdoc/>
    public static bool operator false(CompiledValue v) => !(bool)v;

    /// <inheritdoc/>
    public static explicit operator bool(CompiledValue v) => v.Type switch
    {
        RuntimeType.Null => false,
        RuntimeType.U8 => v.U8 != default,
        RuntimeType.I8 => v.I8 != default,
        RuntimeType.U16 => v.U16 != default,
        RuntimeType.I16 => v.I16 != default,
        RuntimeType.I32 => v.I32 != default,
        RuntimeType.U32 => v.U32 != default,
        RuntimeType.F32 => v.F32 != default,
        _ => throw new UnreachableException(),
    };
    public static implicit operator CompiledValue(bool v) => new(v);

    /// <inheritdoc/>
    public static explicit operator byte(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (byte)v.U8,
        RuntimeType.I8 => (byte)v.I8,
        RuntimeType.U16 => (byte)v.U16,
        RuntimeType.I16 => (byte)v.I16,
        RuntimeType.I32 => (byte)v.I32,
        RuntimeType.F32 => (byte)v.F32,
        _ => throw new InvalidCastException($"Can't cast \"{v.Type}\" to \"{"u8"}\""),
    };
    public static implicit operator CompiledValue(byte v) => new(v);

    /// <inheritdoc/>
    public static explicit operator ushort(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (ushort)v.U8,
        RuntimeType.I8 => (ushort)v.U8,
        RuntimeType.U16 => (ushort)v.U16,
        RuntimeType.I16 => (ushort)v.I16,
        RuntimeType.U32 => (ushort)v.U32,
        RuntimeType.I32 => (ushort)v.I32,
        RuntimeType.F32 => (ushort)v.F32,
        _ => throw new InvalidCastException($"Can't cast \"{v.Type}\" to \"{"u16"}\""),
    };

    /// <inheritdoc/>
    public static explicit operator int(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (int)v.U8,
        RuntimeType.I8 => (int)v.U8,
        RuntimeType.U16 => (int)v.U16,
        RuntimeType.I16 => (int)v.I16,
        RuntimeType.U32 => (int)v.U32,
        RuntimeType.I32 => (int)v.I32,
        RuntimeType.F32 => (int)v.F32,
        _ => throw new InvalidCastException($"Can't cast \"{v.Type}\" to \"{"i32"}\""),
    };
    public static implicit operator CompiledValue(int v) => new(v);

    /// <inheritdoc/>
    public static explicit operator float(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (float)v.U8,
        RuntimeType.I8 => (float)v.U8,
        RuntimeType.U16 => (float)v.U16,
        RuntimeType.I16 => (float)v.I16,
        RuntimeType.U32 => (float)v.U32,
        RuntimeType.I32 => (float)v.I32,
        RuntimeType.F32 => (float)v.F32,
        _ => throw new InvalidCastException($"Can't cast \"{v.Type}\" to \"{"f32"}\""),
    };
    public static implicit operator CompiledValue(float v) => new(v);

    /// <inheritdoc/>
    public static explicit operator char(CompiledValue v) => v.Type switch
    {
        RuntimeType.U8 => (char)v.U8,
        RuntimeType.I8 => (char)v.U8,
        RuntimeType.U16 => (char)v.U16,
        RuntimeType.I16 => (char)v.I16,
        RuntimeType.U32 => (char)v.U32,
        RuntimeType.I32 => (char)v.I32,
        RuntimeType.F32 => (char)v.F32,
        _ => throw new InvalidCastException($"Can't cast \"{v.Type}\" to \"{"u16"}\""),
    };
    public static implicit operator CompiledValue(char v) => new(v);
}
