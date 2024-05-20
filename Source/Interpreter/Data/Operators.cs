namespace LanguageCore.Runtime;

public partial struct DataItem :
    IAdditionOperators<DataItem, DataItem, DataItem>,
    ISubtractionOperators<DataItem, DataItem, DataItem>,
    IMultiplyOperators<DataItem, DataItem, DataItem>,
    IDivisionOperators<DataItem, DataItem, DataItem>,
    IModulusOperators<DataItem, DataItem, DataItem>,
    IComparisonOperators<DataItem, DataItem, bool>,
    IUnaryPlusOperators<DataItem, DataItem>,
    IUnaryNegationOperators<DataItem, DataItem>,
    IBitwiseOperators<DataItem, DataItem, DataItem>,
    IShiftOperators<DataItem, DataItem, DataItem>,
    IShiftOperators<DataItem, int, DataItem>
{
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator +(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._integer + b._integer)),
            RuntimeType.Integer => new DataItem((int)(a._integer + b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single + b._single)),
            RuntimeType.Char => new DataItem((ushort)((char)a._integer + (char)b._integer)),
            _ => throw new RuntimeException($"Can't do + operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator -(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._integer - b._integer)),
            RuntimeType.Integer => new DataItem((int)(a._integer - b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single - b._single)),
            RuntimeType.Char => new DataItem((ushort)((char)a._integer - (char)b._integer)),
            _ => throw new RuntimeException($"Can't do - operation with type {a_} and {b_}"),
        };
    }

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator <<(DataItem leftSide, DataItem rightSide)
        => leftSide << rightSide.Int;
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>(DataItem leftSide, DataItem rightSide)
        => leftSide >> rightSide.Int;
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>>(DataItem leftSide, DataItem rightSide)
        => leftSide >>> rightSide.Int;

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator <<(DataItem leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._integer << rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer << rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._integer << rightSide))),
        _ => throw new RuntimeException($"Can't do << operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>(DataItem leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._integer >> rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer >> rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._integer >> rightSide))),
        _ => throw new RuntimeException($"Can't do >> operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>>(DataItem leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._integer >>> rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer >>> rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._integer >>> rightSide))),
        _ => throw new RuntimeException($"Can't do >>> operation with type {leftSide.Type}"),
    };

    public override bool Equals(object? obj) => obj is DataItem value && this.Equals(value);

    public bool Equals(DataItem other)
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
    public static void MakeSameType(ref DataItem x, ref DataItem y)
    {
        if (x.Type == y.Type) return;

        DataItem xBefore = x;
        DataItem yBefore = y;

        DataItem.TryCast(ref x, y.Type);
        DataItem.TryCast(ref y, x.Type);

        if (x.IsNull || y.IsNull)
        { throw new InternalException(); }

        if (!xBefore.Equals(x) && !yBefore.Equals(y))
        { throw new InternalException(); }

        if (x.Type != y.Type)
        { throw new InternalException(); }
    }

    /// <exception cref="InternalException"/>
    public static (RuntimeType, RuntimeType) MakeSameTypeAndKeep(ref DataItem x, ref DataItem y)
    {
        (RuntimeType, RuntimeType) result = (x.Type, y.Type);
        DataItem.MakeSameType(ref x, ref y);
        return result;
    }

    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator *(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._integer * b._integer)),
            RuntimeType.Integer => new DataItem((int)(a._integer * b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single * b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._integer * b._integer)),
            _ => throw new RuntimeException($"Can't do * operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator /(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._integer / b._integer)),
            RuntimeType.Integer => new DataItem((int)(a._integer / b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single / b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._integer / b._integer)),
            _ => throw new RuntimeException($"Can't do / operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator %(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._integer % b._integer)),
            RuntimeType.Integer => new DataItem((int)(a._integer % b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single % b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._integer % b._integer)),
            _ => throw new RuntimeException($"Can't do % operation with type {a_} and {b_}"),
        };
    }

    public static bool operator <(DataItem a, DataItem b)
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
    public static bool operator >(DataItem a, DataItem b)
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

    public static bool operator <=(DataItem a, DataItem b)
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
    public static bool operator >=(DataItem a, DataItem b)
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

    public static bool operator ==(DataItem a, DataItem b)
    {
        if (a.IsNull && b.IsNull) return true;
        if (a.IsNull && DataItem.IsZero(b)) return true;
        if (b.IsNull && DataItem.IsZero(a)) return true;

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
    public static bool operator !=(DataItem a, DataItem b)
        => !(a == b);

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator !(DataItem value) => value.Type switch
    {
        RuntimeType.Byte => new DataItem((byte)((value._integer == 0) ? (byte)1 : (byte)0)),
        RuntimeType.Integer => new DataItem((int)((value._integer == 0) ? (int)1 : (int)0)),
        RuntimeType.Single => new DataItem((float)((value._single == 0) ? (float)1f : (float)0f)),
        RuntimeType.Char => new DataItem((char)((value._integer == 0) ? (ushort)1 : (ushort)0)),
        _ => throw new RuntimeException($"Can't do ! operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator +(DataItem value) => value.Type switch
    {
        RuntimeType.Byte => new DataItem((byte)+value._integer),
        RuntimeType.Integer => new DataItem((int)+value._integer),
        RuntimeType.Single => new DataItem((float)+value._single),
        RuntimeType.Char => new DataItem((char)+value._integer),
        _ => throw new RuntimeException($"Can't do + operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator -(DataItem value) => value.Type switch
    {
        RuntimeType.Byte => new DataItem((byte)-value._integer),
        RuntimeType.Integer => new DataItem((int)-value._integer),
        RuntimeType.Single => new DataItem((float)-value._single),
        RuntimeType.Char => new DataItem((char)-value._integer),
        _ => throw new RuntimeException($"Can't do - operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator |(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._integer | b._integer)),
            RuntimeType.Integer => new DataItem((int)(a._integer | b._integer)),
            RuntimeType.Char => new DataItem((ushort)(a._integer | b._integer)),
            _ => throw new RuntimeException($"Can't do | operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator &(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._integer & b._integer)),
            RuntimeType.Integer => new DataItem((int)(a._integer & b._integer)),
            RuntimeType.Char => new DataItem((ushort)(a._integer & b._integer)),
            _ => throw new RuntimeException($"Can't do & operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator ^(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.Type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._integer ^ b._integer)),
            RuntimeType.Integer => new DataItem((int)(a._integer ^ b._integer)),
            RuntimeType.Char => new DataItem((ushort)(a._integer ^ b._integer)),
            _ => throw new RuntimeException($"Can't do ^ operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator ~(DataItem value) => value.Type switch
    {
        RuntimeType.Byte => new DataItem((byte)~value._integer),
        RuntimeType.Integer => new DataItem(~value._integer),
        RuntimeType.Char => new DataItem((char)~value._integer),
        _ => throw new RuntimeException($"Can't do ~ operation with type {value.Type}"),
    };
}
