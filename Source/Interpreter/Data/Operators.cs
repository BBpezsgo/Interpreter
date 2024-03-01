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

        return a.type switch
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

        return a.type switch
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
    public static DataItem operator <<(DataItem leftSide, int rightSide) => leftSide.type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._byte << rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer << rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._char << rightSide))),
        _ => throw new RuntimeException($"Can't do << operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>(DataItem leftSide, int rightSide) => leftSide.type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._byte >> rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer >> rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._char >> rightSide))),
        _ => throw new RuntimeException($"Can't do >> operation with type {leftSide.Type}"),
    };
    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator >>>(DataItem leftSide, int rightSide) => leftSide.type switch
    {
        RuntimeType.Byte => new DataItem(unchecked((byte)(leftSide._byte >>> rightSide))),
        RuntimeType.Integer => new DataItem(unchecked((int)(leftSide._integer >>> rightSide))),
        RuntimeType.Char => new DataItem(unchecked((char)(leftSide._char >>> rightSide))),
        _ => throw new RuntimeException($"Can't do >>> operation with type {leftSide.Type}"),
    };

    public override readonly bool Equals(object? obj) => obj is DataItem value && this.Equals(value);

    public readonly bool Equals(DataItem other)
    {
        if (type != other.type) return false;
        return type switch
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
        if (x.type == y.type) return;

        DataItem xBefore = x;
        DataItem yBefore = y;

        switch (x.type)
        {
            case RuntimeType.Byte:
                switch (y.type)
                {
                    case RuntimeType.Integer:
                        x = new DataItem((int)x._byte);
                        break;
                    case RuntimeType.Single:
                        x = new DataItem((float)x._byte);
                        break;
                    case RuntimeType.Char:
                        x = new DataItem((char)x._byte);
                        break;
                    default: break;
                }
                break;
            case RuntimeType.Integer:
                switch (y.type)
                {
                    case RuntimeType.Byte:
                        y = new DataItem((int)y._byte);
                        break;
                    case RuntimeType.Single:
                        x = new DataItem((float)x._integer);
                        break;
                    case RuntimeType.Char:
                        y = new DataItem((int)y._char);
                        break;
                    default: break;
                }
                break;
            case RuntimeType.Single:
                switch (y.type)
                {
                    case RuntimeType.Byte:
                        y = new DataItem((float)y._byte);
                        break;
                    case RuntimeType.Integer:
                        y = new DataItem((float)y._integer);
                        break;
                    case RuntimeType.Char:
                        y = new DataItem((float)y._char);
                        break;
                    default: break;
                }
                break;
            case RuntimeType.Char:
                switch (y.type)
                {
                    case RuntimeType.Byte:
                        y = new DataItem((char)y._byte);
                        break;
                    case RuntimeType.Integer:
                        x = new DataItem((int)x._char);
                        break;
                    case RuntimeType.Single:
                        x = new DataItem((float)x._char);
                        break;
                    default: break;
                }
                break;
        }

        if (!xBefore.Equals(x) &&
            !yBefore.Equals(y))
        { throw new InternalException(); }

        if (x.type != y.type)
        { throw new InternalException(); }
    }

    /// <exception cref="InternalException"/>
    public static (RuntimeType, RuntimeType) MakeSameTypeAndKeep(ref DataItem x, ref DataItem y)
    {
        (RuntimeType, RuntimeType) result = (x.type, y.type);
        DataItem.MakeSameType(ref x, ref y);
        return result;
    }

    /// <inheritdoc/>
    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator *(DataItem a, DataItem b)
    {
        (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

        return a.type switch
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

        return a.type switch
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

        return a.type switch
        {
            RuntimeType.Byte => new DataItem((byte)(a._byte % b._byte)),
            RuntimeType.Integer => new DataItem((int)(a._integer % b._integer)),
            RuntimeType.Single => new DataItem((float)(a._single % b._single)),
            RuntimeType.Char => new DataItem((ushort)(a._char % b._char)),
            _ => throw new RuntimeException($"Can't do % operation with type {a_} and {b_}"),
        };
    }

    /// <inheritdoc/>
    public static bool operator <(DataItem a, DataItem b)
    {
        if (a.type == RuntimeType.Null || b.type == RuntimeType.Null) return false;

        return a.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => a._byte < b._byte,
            RuntimeType.Integer => a._integer < b._integer,
            RuntimeType.Single => a._single < b._single,
            RuntimeType.Char => a._char < b._char,
            _ => false,
        };
    }
    /// <inheritdoc/>
    public static bool operator >(DataItem a, DataItem b)
    {
        if (a.type == RuntimeType.Null || b.type == RuntimeType.Null) return false;

        return a.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Byte => a._byte > b._byte,
            RuntimeType.Integer => a._integer > b._integer,
            RuntimeType.Single => a._single > b._single,
            RuntimeType.Char => a._char > b._char,
            _ => false,
        };
    }

    /// <inheritdoc/>
    public static bool operator <=(DataItem a, DataItem b)
        => (a < b) || (a == b);
    /// <inheritdoc/>
    public static bool operator >=(DataItem a, DataItem b)
        => (a > b) || (a == b);

    /// <inheritdoc/>
    public static bool operator ==(DataItem a, DataItem b)
    {
        if (a.type == RuntimeType.Null && b.type == RuntimeType.Null) return true;
        if (a.type == RuntimeType.Null && DataItem.IsZero(b)) return true;
        if (b.type == RuntimeType.Null && DataItem.IsZero(a)) return true;

        return a.type switch
        {
            RuntimeType.Null => b.type == RuntimeType.Null,
            RuntimeType.Byte => a._byte == b._byte,
            RuntimeType.Integer => a._integer == b._integer,
            RuntimeType.Single => a._single == b._single,
            RuntimeType.Char => a._char == b._char,
            _ => false,
        };
    }
    /// <inheritdoc/>
    public static bool operator !=(DataItem a, DataItem b)
        => !(a == b);

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator !(DataItem value) => value.type switch
    {
        RuntimeType.Byte => new DataItem((byte)((value._byte == 0) ? (byte)1 : (byte)0)),
        RuntimeType.Integer => new DataItem((int)((value._integer == 0) ? (int)1 : (int)0)),
        RuntimeType.Single => new DataItem((float)((value._single == 0) ? (float)1f : (float)0f)),
        RuntimeType.Char => new DataItem((char)((value._single == 0) ? (ushort)1 : (ushort)0)),
        _ => throw new RuntimeException($"Can't do ! operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator +(DataItem value) => value.type switch
    {
        RuntimeType.Byte => new DataItem((byte)(+value._byte)),
        RuntimeType.Integer => new DataItem((int)(+value._integer)),
        RuntimeType.Single => new DataItem((float)(+value._single)),
        RuntimeType.Char => new DataItem((char)(+value._char)),
        _ => throw new RuntimeException($"Can't do + operation with type {value.Type}"),
    };

    /// <inheritdoc/>
    /// <exception cref="RuntimeException"/>
    public static DataItem operator -(DataItem value) => value.type switch
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

        return a.type switch
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

        return a.type switch
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

        return a.type switch
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
