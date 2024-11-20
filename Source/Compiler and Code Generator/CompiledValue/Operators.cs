using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public partial struct CompiledValue :
    IAdditionOperators<CompiledValue, CompiledValue, CompiledValue>,
    ISubtractionOperators<CompiledValue, CompiledValue, CompiledValue>,
    IMultiplyOperators<CompiledValue, CompiledValue, CompiledValue>,
    IDivisionOperators<CompiledValue, CompiledValue, CompiledValue>,
    IModulusOperators<CompiledValue, CompiledValue, CompiledValue>,
    IComparisonOperators<CompiledValue, CompiledValue, bool>,
    IUnaryPlusOperators<CompiledValue, CompiledValue>,
    IUnaryNegationOperators<CompiledValue, CompiledValue>,
    IBitwiseOperators<CompiledValue, CompiledValue, CompiledValue>,
    IShiftOperators<CompiledValue, CompiledValue, CompiledValue>,
    IShiftOperators<CompiledValue, int, CompiledValue>
{
    /// <inheritdoc/>
    public static CompiledValue operator +(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.U8 => new CompiledValue((byte)(a.U8 + b.U8)),
            RuntimeType.I8 => new CompiledValue((sbyte)(a.I8 + b.I8)),
            RuntimeType.Char => new CompiledValue((ushort)(a.Char + b.Char)),
            RuntimeType.I16 => new CompiledValue((short)(a.I16 + b.I16)),
            RuntimeType.U32 => new CompiledValue((uint)(a.U32 + b.U32)),
            RuntimeType.I32 => new CompiledValue((int)(a.I32 + b.I32)),
            RuntimeType.F32 => new CompiledValue((float)(a.F32 + b.F32)),
            _ => throw new RuntimeException($"Can't do + operation with type \"{a_}\" and \"{b_}\""),
        };
    }
    /// <inheritdoc/>
    public static CompiledValue operator -(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.U8 => new CompiledValue((byte)(a.U8 - b.U8)),
            RuntimeType.I8 => new CompiledValue((sbyte)(a.I8 - b.I8)),
            RuntimeType.Char => new CompiledValue((ushort)(a.Char - b.Char)),
            RuntimeType.I16 => new CompiledValue((short)(a.I16 - b.I16)),
            RuntimeType.U32 => new CompiledValue((uint)(a.U32 - b.U32)),
            RuntimeType.I32 => new CompiledValue((int)(a.I32 - b.I32)),
            RuntimeType.F32 => new CompiledValue((float)(a.F32 - b.F32)),
            _ => throw new RuntimeException($"Can't do - operation with type \"{a_}\" and \"{b_}\""),
        };
    }

    /// <inheritdoc/>
    public static CompiledValue operator <<(CompiledValue leftSide, CompiledValue rightSide)
        => leftSide << rightSide.I32;
    /// <inheritdoc/>
    public static CompiledValue operator >>(CompiledValue leftSide, CompiledValue rightSide)
        => leftSide >> rightSide.I32;
    /// <inheritdoc/>
    public static CompiledValue operator >>>(CompiledValue leftSide, CompiledValue rightSide)
        => leftSide >>> rightSide.I32;

    /// <inheritdoc/>
    public static CompiledValue operator <<(CompiledValue leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.U8 => new CompiledValue(unchecked((byte)(leftSide.U8 << rightSide))),
        RuntimeType.I8 => new CompiledValue(unchecked((sbyte)(leftSide.I8 << rightSide))),
        RuntimeType.Char => new CompiledValue(unchecked((char)(leftSide.Char << rightSide))),
        RuntimeType.I16 => new CompiledValue(unchecked((short)(leftSide.I16 << rightSide))),
        RuntimeType.U32 => new CompiledValue(unchecked((uint)(leftSide.U32 << rightSide))),
        RuntimeType.I32 => new CompiledValue(unchecked((int)(leftSide.I32 << rightSide))),
        _ => throw new RuntimeException($"Can't do << operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    public static CompiledValue operator >>(CompiledValue leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.U8 => new CompiledValue(unchecked((byte)(leftSide.U8 >> rightSide))),
        RuntimeType.I8 => new CompiledValue(unchecked((sbyte)(leftSide.I8 >> rightSide))),
        RuntimeType.Char => new CompiledValue(unchecked((char)(leftSide.Char >> rightSide))),
        RuntimeType.I16 => new CompiledValue(unchecked((short)(leftSide.I16 >> rightSide))),
        RuntimeType.U32 => new CompiledValue(unchecked((uint)(leftSide.U32 >> rightSide))),
        RuntimeType.I32 => new CompiledValue(unchecked((int)(leftSide.I32 >> rightSide))),
        _ => throw new RuntimeException($"Can't do >> operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    public static CompiledValue operator >>>(CompiledValue leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.U8 => new CompiledValue(unchecked((byte)(leftSide.U8 >>> rightSide))),
        RuntimeType.I8 => new CompiledValue(unchecked((sbyte)(leftSide.I8 >>> rightSide))),
        RuntimeType.Char => new CompiledValue(unchecked((char)(leftSide.Char >>> rightSide))),
        RuntimeType.I16 => new CompiledValue(unchecked((short)(leftSide.I16 >>> rightSide))),
        RuntimeType.U32 => new CompiledValue(unchecked((uint)(leftSide.U32 >>> rightSide))),
        RuntimeType.I32 => new CompiledValue(unchecked((int)(leftSide.I32 >>> rightSide))),
        _ => throw new RuntimeException($"Can't do >>> operation with type {leftSide.Type}"),
    };

    public override bool Equals(object? obj) => obj is CompiledValue value && this.Equals(value);

    public bool Equals(CompiledValue other)
    {
        if (Type != other.Type) return false;
        return Type switch
        {
            RuntimeType.Null => true,
            RuntimeType.U8 => U8 == other.U8,
            RuntimeType.I8 => I8 == other.I8,
            RuntimeType.Char => Char == other.Char,
            RuntimeType.I16 => I16 == other.I16,
            RuntimeType.U32 => U32 == other.U32,
            RuntimeType.I32 => I32 == other.I32,
            RuntimeType.F32 => F32 == other.F32,
            _ => false,
        };
    }

    public static void MakeSameType(ref CompiledValue x, ref CompiledValue y)
    {
        if (x.Type == y.Type) return;

        CompiledValue xBefore = x;
        CompiledValue yBefore = y;

        x.TryCast(y.Type, out x);
        y.TryCast(x.Type, out y);

        if (x.IsNull || y.IsNull)
        { throw new InternalExceptionWithoutContext(); }

        if (!xBefore.Equals(x) && !yBefore.Equals(y))
        { throw new InternalExceptionWithoutContext(); }

        if (x.Type != y.Type)
        { throw new InternalExceptionWithoutContext(); }
    }

    public static (RuntimeType, RuntimeType) MakeSameTypeAndKeep(ref CompiledValue x, ref CompiledValue y)
    {
        (RuntimeType, RuntimeType) result = (x.Type, y.Type);
        CompiledValue.MakeSameType(ref x, ref y);
        return result;
    }

    /// <inheritdoc/>
    public static CompiledValue operator *(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.U8 => new CompiledValue((byte)(a.U8 * b.U8)),
            RuntimeType.I8 => new CompiledValue((sbyte)(a.I8 * b.I8)),
            RuntimeType.Char => new CompiledValue((ushort)(a.Char * b.Char)),
            RuntimeType.I16 => new CompiledValue((short)(a.I16 * b.I16)),
            RuntimeType.U32 => new CompiledValue((uint)(a.U32 * b.U32)),
            RuntimeType.I32 => new CompiledValue((int)(a.I32 * b.I32)),
            RuntimeType.F32 => new CompiledValue((float)(a.F32 * b.F32)),
            _ => throw new RuntimeException($"Can't do * operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    public static CompiledValue operator /(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.U8 => new CompiledValue((byte)(a.U8 / b.U8)),
            RuntimeType.I8 => new CompiledValue((sbyte)(a.I8 / b.I8)),
            RuntimeType.Char => new CompiledValue((ushort)(a.Char / b.Char)),
            RuntimeType.I16 => new CompiledValue((short)(a.I16 / b.I16)),
            RuntimeType.U32 => new CompiledValue((uint)(a.U32 / b.U32)),
            RuntimeType.I32 => new CompiledValue((int)(a.I32 / b.I32)),
            RuntimeType.F32 => new CompiledValue((float)(a.F32 / b.F32)),
            _ => throw new RuntimeException($"Can't do / operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    public static CompiledValue operator %(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.U8 => new CompiledValue((byte)(a.U8 % b.U8)),
            RuntimeType.I8 => new CompiledValue((sbyte)(a.I8 % b.I8)),
            RuntimeType.Char => new CompiledValue((ushort)(a.Char % b.Char)),
            RuntimeType.I16 => new CompiledValue((short)(a.I16 % b.I16)),
            RuntimeType.U32 => new CompiledValue((uint)(a.U32 % b.U32)),
            RuntimeType.I32 => new CompiledValue((int)(a.I32 % b.I32)),
            RuntimeType.F32 => new CompiledValue((float)(a.F32 % b.F32)),
            _ => throw new RuntimeException($"Can't do % operation with type {a_} and {b_}"),
        };
    }

    public static bool operator <(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.U8 => a.U8 < b.U8,
            RuntimeType.I8 => a.I8 < b.I8,
            RuntimeType.Char => a.Char < b.Char,
            RuntimeType.I16 => a.I16 < b.I16,
            RuntimeType.U32 => a.U32 < b.U32,
            RuntimeType.I32 => a.I32 < b.I32,
            RuntimeType.F32 => a.F32 < b.F32,
            _ => false,
        };
    }
    public static bool operator >(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.U8 => a.U8 > b.U8,
            RuntimeType.I8 => a.I8 > b.I8,
            RuntimeType.Char => a.Char > b.Char,
            RuntimeType.I16 => a.I16 > b.I16,
            RuntimeType.U32 => a.U32 > b.U32,
            RuntimeType.I32 => a.I32 > b.I32,
            RuntimeType.F32 => a.F32 > b.F32,
            _ => false,
        };
    }

    public static bool operator <=(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.U8 => a.U8 <= b.U8,
            RuntimeType.I8 => a.I8 <= b.I8,
            RuntimeType.Char => a.Char <= b.Char,
            RuntimeType.I16 => a.I16 <= b.I16,
            RuntimeType.U32 => a.U32 <= b.U32,
            RuntimeType.I32 => a.I32 <= b.I32,
            RuntimeType.F32 => a.F32 <= b.F32,
            _ => false,
        };
    }
    public static bool operator >=(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.U8 => a.U8 >= b.U8,
            RuntimeType.I8 => a.I8 >= b.I8,
            RuntimeType.Char => a.Char >= b.Char,
            RuntimeType.I16 => a.I16 >= b.I16,
            RuntimeType.U32 => a.U32 >= b.U32,
            RuntimeType.I32 => a.I32 >= b.I32,
            RuntimeType.F32 => a.F32 >= b.F32,
            _ => false,
        };
    }

    public static bool operator ==(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull && b.IsNull) return true;
        if (a.IsNull && CompiledValue.IsZero(b)) return true;
        if (b.IsNull && CompiledValue.IsZero(a)) return true;

        return a.Type switch
        {
            RuntimeType.Null => b.IsNull,
            RuntimeType.U8 => a.U8 == b.U8,
            RuntimeType.I8 => a.I8 == b.I8,
            RuntimeType.Char => a.Char == b.Char,
            RuntimeType.I16 => a.I16 == b.I16,
            RuntimeType.U32 => a.U32 == b.U32,
            RuntimeType.I32 => a.I32 == b.I32,
            RuntimeType.F32 => a.F32 == b.F32,
            _ => false,
        };
    }
    public static bool operator !=(CompiledValue a, CompiledValue b)
        => !(a == b);

    /// <inheritdoc/>
    public static CompiledValue operator !(CompiledValue value) => value.Type switch
    {
        RuntimeType.U8 => new CompiledValue(!(bool)value),
        RuntimeType.I8 => new CompiledValue(!(bool)value),
        RuntimeType.Char => new CompiledValue(!(bool)value),
        RuntimeType.I16 => new CompiledValue(!(bool)value),
        RuntimeType.U32 => new CompiledValue(!(bool)value),
        RuntimeType.I32 => new CompiledValue(!(bool)value),
        RuntimeType.F32 => new CompiledValue(!(bool)value),
        _ => throw new RuntimeException($"Can't do ! operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    public static CompiledValue operator +(CompiledValue value) => value.Type switch
    {
        RuntimeType.U8 => new CompiledValue((byte)+value.U8),
        RuntimeType.I8 => new CompiledValue((sbyte)+value.I8),
        RuntimeType.Char => new CompiledValue((char)+value.Char),
        RuntimeType.I16 => new CompiledValue((short)+value.I16),
        RuntimeType.U32 => new CompiledValue((uint)+value.U32),
        RuntimeType.I32 => new CompiledValue((int)+value.I32),
        RuntimeType.F32 => new CompiledValue((float)+value.F32),
        _ => throw new RuntimeException($"Can't do + operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    public static CompiledValue operator -(CompiledValue value) => value.Type switch
    {
        RuntimeType.U8 => new CompiledValue((byte)-value.U8),
        RuntimeType.I8 => new CompiledValue((sbyte)-value.I8),
        RuntimeType.Char => new CompiledValue((char)-value.Char),
        RuntimeType.I16 => new CompiledValue((short)-value.I16),
        RuntimeType.U32 => new CompiledValue((uint)-value.U32),
        RuntimeType.I32 => new CompiledValue((int)-value.I32),
        RuntimeType.F32 => new CompiledValue((float)-value.F32),
        _ => throw new RuntimeException($"Can't do - operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    public static CompiledValue operator |(CompiledValue a, CompiledValue b)
    {
        if (a.Type != b.Type)
        { throw new RuntimeException($"Can't do | operation with type {a.Type} and {b.Type}"); }

        Flags flags = default;
        int result = ALU.BitwiseOr(a.I32, b.I32, ref flags, a.BitWidth);
        return new CompiledValue(result, a.Type);
    }
    /// <inheritdoc/>
    public static CompiledValue operator &(CompiledValue a, CompiledValue b)
    {
        if (a.Type != b.Type)
        { throw new RuntimeException($"Can't do & operation with type {a.Type} and {b.Type}"); }

        Flags flags = default;
        int result = ALU.BitwiseAnd(a.I32, b.I32, ref flags, a.BitWidth);
        return new CompiledValue(result, a.Type);
    }
    /// <inheritdoc/>
    public static CompiledValue operator ^(CompiledValue a, CompiledValue b)
    {
        if (a.Type != b.Type)
        { throw new RuntimeException($"Can't do ^ operation with type {a.Type} and {b.Type}"); }

        Flags flags = default;
        int result = ALU.BitwiseXor(a.I32, b.I32, ref flags, a.BitWidth);
        return new CompiledValue(result, a.Type);
    }
    /// <inheritdoc/>
    public static CompiledValue operator ~(CompiledValue value)
    {
        Flags flags = default;
        int result = ALU.BitwiseNot(value.I32, ref flags, value.BitWidth);
        return new CompiledValue(result, value.Type);
    }
}
