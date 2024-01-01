using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace LanguageCore.Runtime
{
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
        IShiftOperators<DataItem, DataItem, DataItem>
    {
        /// <inheritdoc/>
        /// <exception cref="InternalException"/>
        /// <exception cref="RuntimeException"/>
        public static DataItem operator +(DataItem a, DataItem b)
        {
            (RuntimeType a_, RuntimeType b_) = DataItem.MakeSameTypeAndKeep(ref a, ref b);

            return a.type switch
            {
                RuntimeType.UInt8 => new DataItem((byte)(a.valueUInt8 + b.valueUInt8)),
                RuntimeType.SInt32 => new DataItem((int)(a.valueSInt32 + b.valueSInt32)),
                RuntimeType.Single => new DataItem((float)(a.valueSingle + b.valueSingle)),
                RuntimeType.UInt16 => new DataItem((ushort)(a.valueUInt16 + b.valueUInt16)),
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
                RuntimeType.UInt8 => new DataItem((byte)(a.valueUInt8 - b.valueUInt8)),
                RuntimeType.SInt32 => new DataItem((int)(a.valueSInt32 - b.valueSInt32)),
                RuntimeType.Single => new DataItem((float)(a.valueSingle - b.valueSingle)),
                RuntimeType.UInt16 => new DataItem((ushort)(a.valueUInt16 - b.valueUInt16)),
                _ => throw new RuntimeException($"Can't do - operation with type {a_} and {b_}"),
            };
        }

        /// <inheritdoc/>
        /// <exception cref="RuntimeException"/>
        public static DataItem operator <<(DataItem leftSide, DataItem rightSide)
        {
            int? _offset = rightSide.Integer;
            if (!_offset.HasValue)
            { throw new RuntimeException($"Can't do << operation with type {leftSide.Type} and {rightSide.Type}"); }
            int offset = _offset.Value;

            return leftSide.type switch
            {
                RuntimeType.UInt8 => new DataItem(unchecked((byte)(leftSide.valueUInt8 << offset))),
                RuntimeType.SInt32 => new DataItem(unchecked((int)(leftSide.valueSInt32 << offset))),
                RuntimeType.UInt16 => new DataItem(unchecked((char)(leftSide.valueUInt16 << offset))),
                _ => throw new RuntimeException($"Can't do << operation with type {leftSide.Type} and {rightSide.Type}"),
            };
        }
        /// <inheritdoc/>
        /// <exception cref="RuntimeException"/>
        public static DataItem operator >>(DataItem leftSide, DataItem rightSide)
        {
            int? _offset = rightSide.Integer;
            if (!_offset.HasValue)
            { throw new RuntimeException($"Can't do >> operation with type {leftSide.Type} and {rightSide.Type}"); }
            int offset = _offset.Value;

            return leftSide.type switch
            {
                RuntimeType.UInt8 => new DataItem(unchecked((byte)(leftSide.valueUInt8 >> offset))),
                RuntimeType.SInt32 => new DataItem(unchecked((int)(leftSide.valueSInt32 >> offset))),
                RuntimeType.UInt16 => new DataItem(unchecked((char)(leftSide.valueUInt16 >> offset))),
                _ => throw new RuntimeException($"Can't do >> operation with type {leftSide.Type} and {rightSide.Type}"),
            };
        }
        /// <inheritdoc/>
        /// <exception cref="RuntimeException"/>
        public static DataItem operator >>>(DataItem leftSide, DataItem rightSide)
        {
            int? _offset = rightSide.Integer;
            if (!_offset.HasValue)
            { throw new RuntimeException($"Can't do >>> operation with type {leftSide.Type} and {rightSide.Type}"); }
            int offset = _offset.Value;

            return leftSide.type switch
            {
                RuntimeType.UInt8 => new DataItem(unchecked((byte)(leftSide.valueUInt8 >>> offset))),
                RuntimeType.SInt32 => new DataItem(unchecked((int)(leftSide.valueSInt32 >>> offset))),
                RuntimeType.UInt16 => new DataItem(unchecked((char)(leftSide.valueUInt16 >>> offset))),
                _ => throw new RuntimeException($"Can't do >>> operation with type {leftSide.Type} and {rightSide.Type}"),
            };
        }

        public override readonly bool Equals(object? obj) => obj is DataItem value && this.Equals(value);

        public readonly bool Equals(DataItem other)
        {
            if (type != other.type) return false;
            return type switch
            {
                RuntimeType.Null => false,
                RuntimeType.UInt8 => valueUInt8 == other.valueUInt8,
                RuntimeType.SInt32 => valueSInt32 == other.valueSInt32,
                RuntimeType.Single => valueSingle == other.valueSingle,
                RuntimeType.UInt16 => valueUInt16 == other.valueUInt16,
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
                case RuntimeType.UInt8:
                    switch (y.type)
                    {
                        case RuntimeType.SInt32:
                            x = new DataItem((int)x.valueUInt8);
                            break;
                        case RuntimeType.Single:
                            x = new DataItem((float)x.valueUInt8);
                            break;
                        case RuntimeType.UInt16:
                            x = new DataItem((char)x.valueUInt8);
                            break;
                        default: break;
                    }
                    break;
                case RuntimeType.SInt32:
                    switch (y.type)
                    {
                        case RuntimeType.UInt8:
                            y = new DataItem((int)y.valueUInt8);
                            break;
                        case RuntimeType.Single:
                            x = new DataItem((float)x.valueSInt32);
                            break;
                        case RuntimeType.UInt16:
                            y = new DataItem((int)y.valueUInt16);
                            break;
                        default: break;
                    }
                    break;
                case RuntimeType.Single:
                    switch (y.type)
                    {
                        case RuntimeType.UInt8:
                            y = new DataItem((float)y.valueUInt8);
                            break;
                        case RuntimeType.SInt32:
                            y = new DataItem((float)y.valueSInt32);
                            break;
                        case RuntimeType.UInt16:
                            y = new DataItem((float)y.valueUInt16);
                            break;
                        default: break;
                    }
                    break;
                case RuntimeType.UInt16:
                    switch (y.type)
                    {
                        case RuntimeType.UInt8:
                            y = new DataItem((char)y.valueUInt8);
                            break;
                        case RuntimeType.SInt32:
                            x = new DataItem((int)x.valueUInt16);
                            break;
                        case RuntimeType.Single:
                            x = new DataItem((float)x.valueUInt16);
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
                RuntimeType.UInt8 => new DataItem((byte)(a.valueUInt8 * b.valueUInt8)),
                RuntimeType.SInt32 => new DataItem((int)(a.valueSInt32 * b.valueSInt32)),
                RuntimeType.Single => new DataItem((float)(a.valueSingle * b.valueSingle)),
                RuntimeType.UInt16 => new DataItem((ushort)(a.valueUInt16 * b.valueUInt16)),
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
                RuntimeType.UInt8 => new DataItem((byte)(a.valueUInt8 / b.valueUInt8)),
                RuntimeType.SInt32 => new DataItem((int)(a.valueSInt32 / b.valueSInt32)),
                RuntimeType.Single => new DataItem((float)(a.valueSingle / b.valueSingle)),
                RuntimeType.UInt16 => new DataItem((ushort)(a.valueUInt16 / b.valueUInt16)),
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
                RuntimeType.UInt8 => new DataItem((byte)(a.valueUInt8 % b.valueUInt8)),
                RuntimeType.SInt32 => new DataItem((int)(a.valueSInt32 % b.valueSInt32)),
                RuntimeType.Single => new DataItem((float)(a.valueSingle % b.valueSingle)),
                RuntimeType.UInt16 => new DataItem((ushort)(a.valueUInt16 % b.valueUInt16)),
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
                RuntimeType.UInt8 => a.valueUInt8 < b.valueUInt8,
                RuntimeType.SInt32 => a.valueSInt32 < b.valueSInt32,
                RuntimeType.Single => a.valueSingle < b.valueSingle,
                RuntimeType.UInt16 => a.valueUInt16 < b.valueUInt16,
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
                RuntimeType.UInt8 => a.valueUInt8 > b.valueUInt8,
                RuntimeType.SInt32 => a.valueSInt32 > b.valueSInt32,
                RuntimeType.Single => a.valueSingle > b.valueSingle,
                RuntimeType.UInt16 => a.valueUInt16 > b.valueUInt16,
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
                RuntimeType.Null => b.IsNull,
                RuntimeType.UInt8 => a.valueUInt8 == b.valueUInt8,
                RuntimeType.SInt32 => a.valueSInt32 == b.valueSInt32,
                RuntimeType.Single => a.valueSingle == b.valueSingle,
                RuntimeType.UInt16 => a.valueUInt16 == b.valueUInt16,
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
            RuntimeType.UInt8 => new DataItem((byte)((value.valueUInt8 == 0) ? (byte)1 : (byte)0)),
            RuntimeType.SInt32 => new DataItem((int)((value.valueSInt32 == 0) ? (int)1 : (int)0)),
            RuntimeType.Single => new DataItem((float)((value.valueSingle == 0) ? (float)1f : (float)0f)),
            RuntimeType.UInt16 => new DataItem((char)((value.valueSingle == 0) ? (ushort)1 : (ushort)0)),
            _ => throw new RuntimeException($"Can't do ! operation with type {value.Type}"),
        };

        /// <inheritdoc/>
        /// <exception cref="RuntimeException"/>
        public static DataItem operator +(DataItem value) => value.type switch
        {
            RuntimeType.UInt8 => new DataItem((byte)(+value.valueUInt8)),
            RuntimeType.SInt32 => new DataItem((int)(+value.valueSInt32)),
            RuntimeType.Single => new DataItem((float)(+value.valueSingle)),
            RuntimeType.UInt16 => new DataItem((char)(+value.valueUInt16)),
            _ => throw new RuntimeException($"Can't do + operation with type {value.Type}"),
        };

        /// <inheritdoc/>
        /// <exception cref="RuntimeException"/>
        public static DataItem operator -(DataItem value) => value.type switch
        {
            RuntimeType.UInt8 => new DataItem((byte)(-value.valueUInt8)),
            RuntimeType.SInt32 => new DataItem((int)(-value.valueSInt32)),
            RuntimeType.Single => new DataItem((float)(-value.valueSingle)),
            RuntimeType.UInt16 => new DataItem((char)(-value.valueUInt16)),
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
                RuntimeType.UInt8 => new DataItem((byte)(a.valueUInt8 | b.valueUInt8)),
                RuntimeType.SInt32 => new DataItem((int)(a.valueSInt32 | b.valueSInt32)),
                RuntimeType.UInt16 => new DataItem((ushort)(a.valueUInt16 | b.valueUInt16)),
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
                RuntimeType.UInt8 => new DataItem((byte)(a.valueUInt8 & b.valueUInt8)),
                RuntimeType.SInt32 => new DataItem((int)(a.valueSInt32 & b.valueSInt32)),
                RuntimeType.UInt16 => new DataItem((ushort)(a.valueUInt16 & b.valueUInt16)),
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
                RuntimeType.UInt8 => new DataItem((byte)(a.valueUInt8 ^ b.valueUInt8)),
                RuntimeType.SInt32 => new DataItem((int)(a.valueSInt32 ^ b.valueSInt32)),
                RuntimeType.UInt16 => new DataItem((ushort)(a.valueUInt16 ^ b.valueUInt16)),
                _ => throw new RuntimeException($"Can't do ^ operation with type {a_} and {b_}"),
            };
        }
        /// <inheritdoc/>
        /// <exception cref="RuntimeException"/>
        public static DataItem operator ~(DataItem value)
        {
            if (value.Type == RuntimeType.UInt8)
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
}
