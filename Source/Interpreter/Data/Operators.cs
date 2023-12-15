﻿using System.Numerics;

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
        IBitwiseOperators<DataItem, DataItem, DataItem>
    {
        /// <exception cref="RuntimeException"/>
        public static DataItem operator +(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value + right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value + right.Value); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value + right.Value); }
            }

            throw new RuntimeException("Can't do + operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator -(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value - right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value - right.Value); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value - right.Value); }
            }

            throw new RuntimeException("Can't do - operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem BitshiftLeft(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value << right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value << right.Value); }
            }

            throw new RuntimeException("Can't do << operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem BitshiftRight(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value >> right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value >> right.Value); }
            }

            throw new RuntimeException("Can't do >> operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem operator *(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value * right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value * right.Value); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value * right.Value); }
            }

            throw new RuntimeException("Can't do * operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator /(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value / right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value / right.Value); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value / right.Value); }
            }

            throw new RuntimeException("Can't do / operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator %(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value % right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value % right.Value); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value % right.Value); }
            }

            throw new RuntimeException("Can't do % operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static bool operator <(DataItem leftSide, DataItem rightSide)
        {
            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return left.Value < right.Value; }
            }

            throw new RuntimeException("Can't do < operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static bool operator >(DataItem leftSide, DataItem rightSide)
        {
            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return left.Value > right.Value; }
            }

            throw new RuntimeException("Can't do > operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static bool operator <=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return (leftSide < rightSide) || (leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do <= operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString(), ex); }
        }
        /// <exception cref="RuntimeException"/>
        public static bool operator >=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return (leftSide > rightSide) || (leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do >= operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString(), ex); }
        }

        /// <exception cref="RuntimeException"/>
        public static bool operator ==(DataItem leftSide, DataItem rightSide)
        {
            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return left.Value == right.Value; }
            }

            throw new RuntimeException("Can't do == operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static bool operator !=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return !(leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do != operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString(), ex); }
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem operator !(DataItem leftSide)
        {
            {
                int? left = leftSide.Integer;

                if (left.HasValue)
                { return new DataItem((left.Value == 0) ? 1 : 0); }
            }

            {
                float? left = leftSide.Float;

                if (left.HasValue)
                { return new DataItem((left.Value == 0f) ? 1f : 0f); }
            }

            throw new RuntimeException($"Can't do ! operation with type {leftSide.Type}");
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem operator +(DataItem value) => value.type switch
        {
            RuntimeType.Null => value,
            RuntimeType.UInt8 => new DataItem((byte)(+value.valueUInt8)),
            RuntimeType.SInt32 => new DataItem((int)(+value.valueSInt32)),
            RuntimeType.Single => new DataItem((float)(+value.valueSingle)),
            RuntimeType.UInt16 => new DataItem((char)(+value.valueUInt16)),
            _ => throw new RuntimeException($"Can't do + operation with type {value.Type}"),
        };

        /// <exception cref="RuntimeException"/>
        public static DataItem operator -(DataItem value) => value.type switch
        {
            RuntimeType.Null => value,
            RuntimeType.UInt8 => new DataItem((byte)(-value.valueUInt8)),
            RuntimeType.SInt32 => new DataItem((int)(-value.valueSInt32)),
            RuntimeType.Single => new DataItem((float)(-value.valueSingle)),
            RuntimeType.UInt16 => new DataItem((char)(-value.valueUInt16)),
            _ => throw new RuntimeException($"Can't do - operation with type {value.Type}"),
        };

        /// <exception cref="RuntimeException"/>
        public static DataItem operator |(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value | right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value | right.Value); }
            }

            throw new RuntimeException($"Can't do | operation with type {leftSide.Type} and {rightSide.Type}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator &(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value & right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value & right.Value); }
            }

            throw new RuntimeException($"Can't do & operation with type {leftSide.Type} and {rightSide.Type}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator ^(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.UInt8 && rightSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value ^ right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r); }
                    else
                    { return new DataItem((byte)r); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value ^ right.Value); }
            }

            throw new RuntimeException($"Can't do ^ operation with type {leftSide.Type} and {rightSide.Type}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator ~(DataItem leftSide)
        {
            if (leftSide.Type == RuntimeType.UInt8)
            {
                byte? left = leftSide.Byte;

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
                int? left = leftSide.Integer;

                if (left.HasValue)
                { return new DataItem(~left.Value); }
            }

            throw new RuntimeException($"Can't do ~ operation with type {leftSide.Type}");
        }
    }
}
