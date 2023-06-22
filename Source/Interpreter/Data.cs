﻿using System;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    public enum RuntimeType : byte
    {
        BYTE,
        INT,
        FLOAT,
        CHAR,
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct DataItem
    {
        public static DataItem Null => new() { };

        public readonly RuntimeType type;

        #region Value Fields

        byte? valueByte;
        int? valueInt;
        float? valueFloat;
        char? valueChar;

        #endregion

        public readonly bool IsNull
        {
            get
            {
                if (valueByte.HasValue) return false;
                if (valueInt.HasValue) return false;
                if (valueFloat.HasValue) return false;
                if (valueChar.HasValue) return false;
                return true;
            }
        }
        /// <summary><b>Only for debugging!</b></summary>
        public string Tag { get; internal set; }

        #region Value Properties

        /// <exception cref="RuntimeException"/>
        public byte ValueByte
        {
            readonly get
            {
                if (type == RuntimeType.BYTE)
                { return valueByte ?? (byte)0; }

                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to byte");
            }
            set
            {
                valueByte = value;
            }
        }
        /// <exception cref="RuntimeException"/>
        public int ValueInt
        {
            readonly get
            {
                if (type == RuntimeType.INT)
                { return valueInt.Value; }

                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to integer");
            }
            set
            {
                valueInt = value;
            }
        }
        /// <exception cref="RuntimeException"/>
        public float ValueFloat
        {
            readonly get
            {
                if (type == RuntimeType.FLOAT)
                {
                    return valueFloat.Value;
                }
                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to float");
            }
            set
            {
                valueFloat = value;
            }
        }
        /// <exception cref="RuntimeException"/>
        public char ValueChar
        {
            readonly get
            {
                if (type == RuntimeType.CHAR)
                {
                    return valueChar.Value;
                }
                if (type == RuntimeType.INT)
                {
                    return (char)valueInt.Value;
                }
                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to char");
            }
            set
            {
                valueChar = value;
            }
        }

        #endregion

        #region Constructors

        DataItem(RuntimeType type, string tag)
        {
            this.type = type;

            this.valueInt = null;
            this.valueByte = null;
            this.valueFloat = null;
            this.valueChar = null;

            this.Tag = tag;
        }
        DataItem(RuntimeType type)
        {
            this.type = type;

            this.valueInt = null;
            this.valueByte = null;
            this.valueFloat = null;
            this.valueChar = null;

            this.Tag = null;
        }

        public DataItem(int value, string tag) : this(RuntimeType.INT, tag)
        { this.valueInt = value; }
        public DataItem(byte value, string tag) : this(RuntimeType.BYTE, tag)
        { this.valueByte = value; }
        public DataItem(float value, string tag) : this(RuntimeType.FLOAT, tag)
        { this.valueFloat = value; }
        public DataItem(char value, string tag) : this(RuntimeType.CHAR, tag)
        { this.valueChar = value; }
        public DataItem(bool value, string tag) : this(value ? 1 : 0, tag)
        { }

        public DataItem(int value) : this(RuntimeType.INT)
        { this.valueInt = value; }
        public DataItem(byte value) : this(RuntimeType.BYTE)
        { this.valueByte = value; }
        public DataItem(float value) : this(RuntimeType.FLOAT)
        { this.valueFloat = value; }
        public DataItem(char value) : this(RuntimeType.CHAR)
        { this.valueChar = value; }
        public DataItem(bool value) : this(value ? 1 : 0)
        { }

        #endregion

        #region Operators

        /// <exception cref="RuntimeException"/>
        public static DataItem operator +(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value + right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value + right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value + right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do + operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator -(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value - right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value - right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value - right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do - operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem BitshiftLeft(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value << right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value << right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do << operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem BitshiftRight(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value >> right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value >> right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do >> operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem operator *(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value * right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value * right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value * right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do * operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator /(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value / right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value / right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value / right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do / operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator %(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value % right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value % right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value % right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do % operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
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

            throw new RuntimeException("Can't do < operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
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

            throw new RuntimeException("Can't do > operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static bool operator <=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return (leftSide < rightSide) || (leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do <= operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString(), ex); }
        }
        /// <exception cref="RuntimeException"/>
        public static bool operator >=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return (leftSide > rightSide) || (leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do >= operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString(), ex); }
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

            throw new RuntimeException("Can't do == operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static bool operator !=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return !(leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do != operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString(), ex); }
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem operator !(DataItem leftSide)
        {
            {
                int? left = leftSide.Integer;

                if (left.HasValue)
                { return new DataItem((left.Value == 0) ? 1 : 0, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;

                if (left.HasValue)
                { return new DataItem((left.Value == 0f) ? 1f : 0f, leftSide.Tag); }
            }

            throw new RuntimeException($"Can't do ! operation with type {leftSide.GetTypeText()}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator |(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value | right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value | right.Value, leftSide.Tag); }
            }

            throw new RuntimeException($"Can't do | operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator &(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value & right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value & right.Value, leftSide.Tag); }
            }

            throw new RuntimeException($"Can't do & operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator ^(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value ^ right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value ^ right.Value, leftSide.Tag); }
            }

            throw new RuntimeException($"Can't do ^ operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }

        public readonly bool IsFalsy() => this.type switch
        {
            RuntimeType.BYTE => this.valueByte == 0,
            RuntimeType.INT => this.valueInt.Value == 0,
            RuntimeType.FLOAT => this.valueFloat.Value == 0f,
            RuntimeType.CHAR => this.valueChar.Value == 0,
            _ => false,
        };

        #endregion

        public readonly int? Integer => type switch
        {
            RuntimeType.BYTE => this.ValueByte,
            RuntimeType.INT => this.ValueInt,
            RuntimeType.CHAR => this.ValueChar,
            _ => null,
        };
        public readonly byte? Byte
        {
            get
            {
                int? integer_ = this.Integer;
                return (!integer_.HasValue || integer_.Value < byte.MinValue || integer_.Value > byte.MaxValue) ?
                    null :
                    (byte)integer_.Value;
            }
        }
        public readonly float? Float => type switch
        {
            RuntimeType.BYTE => this.ValueByte,
            RuntimeType.INT => this.ValueInt,
            RuntimeType.FLOAT => this.ValueFloat,
            RuntimeType.CHAR => this.ValueChar,
            _ => null,
        };

        /// <exception cref="RuntimeException"/>
        public readonly override string ToString() => this.IsNull ? "null" : type switch
        {
            RuntimeType.INT => ValueInt.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.BYTE => ValueByte.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.FLOAT => ValueFloat.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.CHAR => ValueChar.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
        };

        /// <exception cref="RuntimeException"/>
        public readonly string GetDebuggerDisplay()
        {
            if (IsNull) return null;
            string retStr = type switch
            {
                RuntimeType.INT => ValueInt.ToString(),
                RuntimeType.BYTE => ValueByte.ToString(),
                RuntimeType.FLOAT => ValueFloat.ToString().Replace(',', '.') + "f",
                RuntimeType.CHAR => $"'{ValueChar.Escape()}'",
                _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
            };
            if (!string.IsNullOrEmpty(this.Tag))
            {
                retStr = retStr + " #" + this.Tag;
            }
            return retStr;
        }

        public readonly override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(type);
            switch (type)
            {
                case RuntimeType.BYTE:
                    hash.Add(valueByte);
                    break;
                case RuntimeType.INT:
                    hash.Add(valueInt);
                    break;
                case RuntimeType.FLOAT:
                    hash.Add(valueFloat);
                    break;
                case RuntimeType.CHAR:
                    hash.Add(valueChar);
                    break;
                default:
                    break;
            }
            return hash.ToHashCode();
        }

        /// <exception cref="NotImplementedException"/>
        public readonly override bool Equals(object obj)
            => obj is DataItem value &&
            this.type == value.type &&
            this.type switch
            {
                RuntimeType.BYTE => valueByte == value.valueByte,
                RuntimeType.INT => valueInt == value.valueInt,
                RuntimeType.FLOAT => valueFloat == value.valueFloat,
                RuntimeType.CHAR => valueChar == value.valueChar,
                _ => throw new NotImplementedException(),
            };

        public readonly void DebugPrint()
        {
            ConsoleColor savedFgColor = Console.ForegroundColor;
            ConsoleColor savedBgColor = Console.BackgroundColor;
            IFormatProvider ci = System.Globalization.CultureInfo.InvariantCulture;

            if (IsNull)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("null");
            }
            else
            {
                switch (type)
                {
                    case RuntimeType.BYTE:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(this.valueByte?.ToString(ci));
                        break;
                    case RuntimeType.INT:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(this.valueInt?.ToString(ci));
                        break;
                    case RuntimeType.FLOAT:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(this.valueFloat?.ToString(ci));
                        break;
                    case RuntimeType.CHAR:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("'" + this.valueChar?.ToString(ci) + "'");
                        break;
                    default:
                        break;
                }
            }

            Console.ResetColor();
            Console.ForegroundColor = savedFgColor;
            Console.BackgroundColor = savedBgColor;
        }
    }
}
