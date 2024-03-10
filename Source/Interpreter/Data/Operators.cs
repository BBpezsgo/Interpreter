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
            RuntimeType.Byte => new DataItem((byte)(a._byte + b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer + b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single + b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._char + b._char)),
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
            RuntimeType.Byte => new DataItem((byte)(a._byte - b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer - b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single - b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._char - b._char)),
            _ => throw new RuntimeException($"Can't do - operation with type {a_} and {b_}"),
        };
    }

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator <<(DataItem leftSide, DataItem rightSide)
        => leftSide << (rightSide.Integer ?? throw new RuntimeException($"Can't do << operation with type {leftSide.Type} and {rightSide.Type}"));
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>(DataItem leftSide, DataItem rightSide)
        => leftSide >> (rightSide.Integer ?? throw new RuntimeException($"Can't do >> operation with type {leftSide.Type} and {rightSide.Type}"));
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>>(DataItem leftSide, DataItem rightSide)
        => leftSide >>> (rightSide.Integer ?? throw new RuntimeException($"Can't do >>> operation with type {leftSide.Type} and {rightSide.Type}"));

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator <<(DataItem leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._byte << rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer << rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._char << rightSide))),
        _ => throw new RuntimeException($"Can't do << operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>(DataItem leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._byte >> rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer >> rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._char >> rightSide))),
        _ => throw new RuntimeException($"Can't do >> operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>>(DataItem leftSide, int rightSide) => leftSide.Type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._byte >>> rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer >>> rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._char >>> rightSide))),
        _ => throw new RuntimeException($"Can't do >>> operation with type {leftSide.Type}"),
    };

    public override bool Equals(object? obj) => obj is DataItem value && this.Equals(value);

    public bool Equals(DataItem other)
    {
        if (Type != other.Type) return false;
        return Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => _byte == other._byte,
            RuntimeType.Integer => _integer == other._integer,
            RuntimeType.Single => _single == other._single,
            RuntimeType.Char => _char == other._char,
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

        // if (!xBefore.Equals(x) &&
        //     !yBefore.Equals(y))
        // { throw new InternalException(); }

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
            RuntimeType.Byte => new DataItem((byte)(a._byte * b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer * b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single * b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._char * b._char)),
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
            RuntimeType.Byte => new DataItem((byte)(a._byte / b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer / b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single / b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._char / b._char)),
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
            RuntimeType.Byte => new DataItem((byte)(a._byte % b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer % b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single % b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._char % b._char)),
            _ => throw new RuntimeException($"Can't do % operation with type {a_} and {b_}"),
        };
    }

    public static bool operator <(DataItem a, DataItem b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => a._byte < b._byte,
            RuntimeType.Integer => a._integer < b._integer,
            RuntimeType.Single => a._single < b._single,
            RuntimeType.Char => a._char < b._char,
            _ => false,
        };
    }
    public static bool operator >(DataItem a, DataItem b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => a._byte > b._byte,
            RuntimeType.Integer => a._integer > b._integer,
            RuntimeType.Single => a._single > b._single,
            RuntimeType.Char => a._char > b._char,
            _ => false,
        };
    }

    public static bool operator <=(DataItem a, DataItem b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => a._byte <= b._byte,
            RuntimeType.Integer => a._integer <= b._integer,
            RuntimeType.Single => a._single <= b._single,
            RuntimeType.Char => a._char <= b._char,
            _ => false,
        };
    }
    public static bool operator >=(DataItem a, DataItem b)
    {
        if (a.IsNull || b.IsNull) return false;

        return a.Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => a._byte >= b._byte,
            RuntimeType.Integer => a._integer >= b._integer,
            RuntimeType.Single => a._single >= b._single,
            RuntimeType.Char => a._char >= b._char,
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
            RuntimeType.Byte => a._byte == b._byte,
            RuntimeType.Integer => a._integer == b._integer,
            RuntimeType.Single => a._single == b._single,
            RuntimeType.Char => a._char == b._char,
            _ => false,
        };
    }
    public static bool operator !=(DataItem a, DataItem b)
        => !(a == b);

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator !(DataItem value) => value.Type switch
    {
        RuntimeType.Byte => new DataItem((byte)((value._byte == 0) ? (byte)1 : (byte)0)),
        RuntimeType.Integer => new DataItem((int)((value._integer == 0) ? (int)1 : (int)0)),
        RuntimeType.Single => new DataItem((float)((value._single == 0) ? (float)1f : (float)0f)),
        RuntimeType.Char => new DataItem((char)((value._single == 0) ? (ushort)1 : (ushort)0)),
        _ => throw new RuntimeException($"Can't do ! operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator +(DataItem value) => value.Type switch
    {
        RuntimeType.Byte => new DataItem((byte)(+value._byte)),
        RuntimeType.Integer => new DataItem((int)(+value._integer)),
        RuntimeType.Single => new DataItem((float)(+value._single)),
        RuntimeType.Char => new DataItem((char)(+value._char)),
        _ => throw new RuntimeException($"Can't do + operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator -(DataItem value) => value.Type switch
    {
        RuntimeType.Byte => new DataItem((byte)(-value._byte)),
        RuntimeType.Integer => new DataItem((int)(-value._integer)),
        RuntimeType.Single => new DataItem((float)(-value._single)),
        RuntimeType.Char => new DataItem((char)(-value._char)),
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
            RuntimeType.Byte => new DataItem((byte)(a._byte | b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer | b._integer)),
            RuntimeType.Char => new DataItem((ushort)(a._char | b._char)),
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
            RuntimeType.Byte => new DataItem((byte)(a._byte & b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer & b._integer)),
            RuntimeType.Char => new DataItem((ushort)(a._char & b._char)),
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
            RuntimeType.Byte => new DataItem((byte)(a._byte ^ b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer ^ b._integer)),
            RuntimeType.Char => new DataItem((ushort)(a._char ^ b._char)),
            _ => throw new RuntimeException($"Can't do ^ operation with type {a_} and {b_}"),
        };
    }
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator ~(DataItem value)
    {
        if (value.Type == RuntimeType.Byte)
        {
            byte? left = value.Byte;

            if (left.HasValue)
            {
                int r = ~left.Value;
                if (r < byte.MinValue || r > byte.MaxValue)
                { return new DataItem(r); }
                else
                { return new DataItem((byte)r); }
            }
        }

        {
            int? left = value.Integer;

            if (left.HasValue)
            { return new DataItem(~left.Value); }
        }

        throw new RuntimeException($"Can't do ~ operation with type {value.Type}");
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException"/>
    [DoesNotReturn]
    public static DataItem operator --(DataItem value) => throw new NotImplementedException();

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException"/>
    [DoesNotReturn]
    public static DataItem operator ++(DataItem value) => throw new NotImplementedException();
}
