namespace LanguageCore.Compiler;

using Runtime;

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
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator +(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new CompiledValue((byte)(a._integer + b._integer)),
            RuntimeType.Integer => new CompiledValue((int)(a._integer + b._integer)),
            RuntimeType.Single => new CompiledValue((float)(a._single + b._single)),
            RuntimeType.Char => new CompiledValue((ushort)((char)a._integer + (char)b._integer)),
            _ => throw new RuntimeException($"Can't do + operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator -(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new CompiledValue((byte)(a._integer - b._integer)),
            RuntimeType.Integer => new CompiledValue((int)(a._integer - b._integer)),
            RuntimeType.Single => new CompiledValue((float)(a._single - b._single)),
            RuntimeType.Char => new CompiledValue((ushort)((char)a._integer - (char)b._integer)),
            _ => throw new RuntimeException($"Can't do - operation with type {a_} and {b_}"),
        };
    }

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator <<(CompiledValue leftSide, CompiledValue rightSide)
        => leftSide << rightSide.Int;
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator >>(CompiledValue leftSide, CompiledValue rightSide)
        => leftSide >> rightSide.Int;
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator >>>(CompiledValue leftSide, CompiledValue rightSide)
        => leftSide >>> rightSide.Int;

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator <<(CompiledValue leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new CompiledValue(unchecked((byte)(leftSide._integer << rightSide))),
        RuntimeType.Integer => new CompiledValue(unchecked((int)(leftSide._integer << rightSide))),
        RuntimeType.Char => new CompiledValue(unchecked((char)(leftSide._integer << rightSide))),
        _ => throw new RuntimeException($"Can't do << operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator >>(CompiledValue leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new CompiledValue(unchecked((byte)(leftSide._integer >> rightSide))),
        RuntimeType.Integer => new CompiledValue(unchecked((int)(leftSide._integer >> rightSide))),
        RuntimeType.Char => new CompiledValue(unchecked((char)(leftSide._integer >> rightSide))),
        _ => throw new RuntimeException($"Can't do >> operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator >>>(CompiledValue leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new CompiledValue(unchecked((byte)(leftSide._integer >>> rightSide))),
        RuntimeType.Integer => new CompiledValue(unchecked((int)(leftSide._integer >>> rightSide))),
        RuntimeType.Char => new CompiledValue(unchecked((char)(leftSide._integer >>> rightSide))),
        _ => throw new RuntimeException($"Can't do >>> operation with type {leftSide.Type}"),
    };

    public override bool Equals(object? obj) => obj is CompiledValue value && this.Equals(value);

    public bool Equals(CompiledValue other)
    {
        if (Type != other.Type) return false;
        return Type switch
        {
            RuntimeType.Null => true,
            RuntimeType.Byte => (byte)_integer == (byte)other._integer,
            RuntimeType.Integer => _integer == other._integer,
            RuntimeType.Single => _single == other._single,
            RuntimeType.Char => (char)_integer == (char)other._integer,
            _ => false,
        };
    }

    /// <exception cref="InternalException"/>
    public static void MakeSameType(ref CompiledValue x, ref CompiledValue y)
    {
        if (x.Type == y.Type) return;

        CompiledValue xBefore = x;
        CompiledValue yBefore = y;

        CompiledValue.TryCast(ref x, y.Type);
        CompiledValue.TryCast(ref y, x.Type);

        if (x.IsNull || y.IsNull)
        { throw new InternalException(); }

        if (!xBefore.Equals(x) && !yBefore.Equals(y))
        { throw new InternalException(); }

        if (x.Type != y.Type)
        { throw new InternalException(); }
    }

    /// <exception cref="InternalException"/>
    public static (RuntimeType, RuntimeType) MakeSameTypeAndKeep(ref CompiledValue x, ref CompiledValue y)
    {
        (RuntimeType, RuntimeType) result = (x.Type, y.Type);
        CompiledValue.MakeSameType(ref x, ref y);
        return result;
    }

    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator *(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new CompiledValue((byte)(a._integer * b._integer)),
            RuntimeType.Integer => new CompiledValue((int)(a._integer * b._integer)),
            RuntimeType.Single => new CompiledValue((float)(a._single * b._single)),
            RuntimeType.Char => new CompiledValue((ushort)(a._integer * b._integer)),
            _ => throw new RuntimeException($"Can't do * operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator /(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new CompiledValue((byte)(a._integer / b._integer)),
            RuntimeType.Integer => new CompiledValue((int)(a._integer / b._integer)),
            RuntimeType.Single => new CompiledValue((float)(a._single / b._single)),
            RuntimeType.Char => new CompiledValue((ushort)(a._integer / b._integer)),
            _ => throw new RuntimeException($"Can't do / operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator %(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new CompiledValue((byte)(a._integer % b._integer)),
            RuntimeType.Integer => new CompiledValue((int)(a._integer % b._integer)),
            RuntimeType.Single => new CompiledValue((float)(a._single % b._single)),
            RuntimeType.Char => new CompiledValue((ushort)(a._integer % b._integer)),
            _ => throw new RuntimeException($"Can't do % operation with type {a_} and {b_}"),
        };
    }

    public static bool operator <(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => (byte)a._integer < (byte)b._integer,
            RuntimeType.Integer => a._integer < b._integer,
            RuntimeType.Single => a._single < b._single,
            RuntimeType.Char => (char)a._integer < (char)b._integer,
            _ => false,
        };
    }
    public static bool operator >(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => (byte)a._integer > (byte)b._integer,
            RuntimeType.Integer => a._integer > b._integer,
            RuntimeType.Single => a._single > b._single,
            RuntimeType.Char => (char)a._integer > (char)b._integer,
            _ => false,
        };
    }

    public static bool operator <=(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => (byte)a._integer <= (byte)b._integer,
            RuntimeType.Integer => a._integer <= b._integer,
            RuntimeType.Single => a._single <= b._single,
            RuntimeType.Char => (char)a._integer <= (char)b._integer,
            _ => false,
        };
    }
    public static bool operator >=(CompiledValue a, CompiledValue b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => (byte)a._integer >= (byte)b._integer,
            RuntimeType.Integer => a._integer >= b._integer,
            RuntimeType.Single => a._single >= b._single,
            RuntimeType.Char => (char)a._integer >= (char)b._integer,
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
            RuntimeType.Byte => (byte)a._integer == (byte)b._integer,
            RuntimeType.Integer => a._integer == b._integer,
            RuntimeType.Single => a._single == b._single,
            RuntimeType.Char => (char)a._integer == (char)b._integer,
            _ => false,
        };
    }
    public static bool operator !=(CompiledValue a, CompiledValue b)
        => !(a == b);

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator !(CompiledValue value) => value.Type switch
    {
        RuntimeType.Byte => new CompiledValue((byte)((value._integer == 0) ? (byte)1 : (byte)0)),
        RuntimeType.Integer => new CompiledValue((int)((value._integer == 0) ? (int)1 : (int)0)),
        RuntimeType.Single => new CompiledValue((float)((value._single == 0) ? (float)1f : (float)0f)),
        RuntimeType.Char => new CompiledValue((char)((value._integer == 0) ? (ushort)1 : (ushort)0)),
        _ => throw new RuntimeException($"Can't do ! operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator +(CompiledValue value) => value.Type switch
    {
        RuntimeType.Byte => new CompiledValue((byte)+value._integer),
        RuntimeType.Integer => new CompiledValue((int)+value._integer),
        RuntimeType.Single => new CompiledValue((float)+value._single),
        RuntimeType.Char => new CompiledValue((char)+value._integer),
        _ => throw new RuntimeException($"Can't do + operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator -(CompiledValue value) => value.Type switch
    {
        RuntimeType.Byte => new CompiledValue((byte)-value._integer),
        RuntimeType.Integer => new CompiledValue((int)-value._integer),
        RuntimeType.Single => new CompiledValue((float)-value._single),
        RuntimeType.Char => new CompiledValue((char)-value._integer),
        _ => throw new RuntimeException($"Can't do - operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator |(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new CompiledValue((byte)(a._integer | b._integer)),
            RuntimeType.Integer => new CompiledValue((int)(a._integer | b._integer)),
            RuntimeType.Char => new CompiledValue((ushort)(a._integer | b._integer)),
            _ => throw new RuntimeException($"Can't do | operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator &(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new CompiledValue((byte)(a._integer & b._integer)),
            RuntimeType.Integer => new CompiledValue((int)(a._integer & b._integer)),
            RuntimeType.Char => new CompiledValue((ushort)(a._integer & b._integer)),
            _ => throw new RuntimeException($"Can't do & operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator ^(CompiledValue a, CompiledValue b)
    {
        (RuntimeType a_, RuntimeType b_) = CompiledValue.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new CompiledValue((byte)(a._integer ^ b._integer)),
            RuntimeType.Integer => new CompiledValue((int)(a._integer ^ b._integer)),
            RuntimeType.Char => new CompiledValue((ushort)(a._integer ^ b._integer)),
            _ => throw new RuntimeException($"Can't do ^ operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static CompiledValue operator ~(CompiledValue value) => value.Type switch
    {
        RuntimeType.Byte => new CompiledValue((byte)~value._integer),
        RuntimeType.Integer => new CompiledValue(~value._integer),
        RuntimeType.Char => new CompiledValue((char)~value._integer),
        _ => throw new RuntimeException($"Can't do ~ operation with type {value.Type}"),
    };
}
