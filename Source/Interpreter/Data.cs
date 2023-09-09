using System;

namespace ProgrammingLanguage.Bytecode
{
    using DataUtilities.ReadableFileFormat;
    using DataUtilities.Serializer;
    using ProgrammingLanguage.Core;
    using ProgrammingLanguage.Errors;

    public enum RuntimeType : byte
    {
        BYTE,
        INT,
        FLOAT,
        CHAR,
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct DataItem : ISerializable<DataItem>, IFullySerializableText
    {
        public static DataItem Null => new() { };

        public readonly RuntimeType Type => type;

        RuntimeType type;

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
                if (Type == RuntimeType.BYTE)
                { return valueByte ?? (byte)0; }

                throw new RuntimeException("Can't cast " + Type.ToString().ToLower() + " to byte");
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
                if (Type == RuntimeType.INT)
                { return valueInt.Value; }

                throw new RuntimeException("Can't cast " + Type.ToString().ToLower() + " to integer");
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
                if (Type == RuntimeType.FLOAT)
                {
                    return valueFloat.Value;
                }
                throw new RuntimeException("Can't cast " + Type.ToString().ToLower() + " to float");
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
                if (Type == RuntimeType.CHAR)
                {
                    return valueChar.Value;
                }
                if (Type == RuntimeType.INT)
                {
                    return (char)valueInt.Value;
                }
                throw new RuntimeException("Can't cast " + Type.ToString().ToLower() + " to char");
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
        DataItem(RuntimeType type) : this(type, null)
        { }

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
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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

            throw new RuntimeException("Can't do + operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator -(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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

            throw new RuntimeException("Can't do - operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem BitshiftLeft(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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

            throw new RuntimeException("Can't do << operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem BitshiftRight(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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

            throw new RuntimeException("Can't do >> operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem operator *(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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

            throw new RuntimeException("Can't do * operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator /(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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

            throw new RuntimeException("Can't do / operation with type " + leftSide.Type.ToString() + " and " + rightSide.Type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator %(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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
            if (leftSide.Type == RuntimeType.BYTE && rightSide.Type == RuntimeType.BYTE)
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

        public readonly bool IsFalsy() => this.Type switch
        {
            RuntimeType.BYTE => this.valueByte == 0,
            RuntimeType.INT => this.valueInt.Value == 0,
            RuntimeType.FLOAT => this.valueFloat.Value == 0f,
            RuntimeType.CHAR => this.valueChar.Value == 0,
            _ => false,
        };

        #endregion

        public readonly int? Integer => Type switch
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
        public readonly float? Float => Type switch
        {
            RuntimeType.BYTE => this.ValueByte,
            RuntimeType.INT => this.ValueInt,
            RuntimeType.FLOAT => this.ValueFloat,
            RuntimeType.CHAR => this.ValueChar,
            _ => null,
        };

        public readonly object GetValue() => type switch
        {
            RuntimeType.BYTE => valueByte.Value,
            RuntimeType.INT => valueInt.Value,
            RuntimeType.FLOAT => valueFloat.Value,
            RuntimeType.CHAR => (object)valueChar.Value,
            _ => throw new ImpossibleException(),
        };

        /// <exception cref="RuntimeException"/>
        public readonly override string ToString() => this.IsNull ? "null" : Type switch
        {
            RuntimeType.INT => ValueInt.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.BYTE => ValueByte.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.FLOAT => ValueFloat.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.CHAR => ValueChar.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new RuntimeException("Can't parse " + Type.ToString() + " to STRING"),
        };

        /// <exception cref="RuntimeException"/>
        public readonly string GetDebuggerDisplay()
        {
            if (IsNull) return null;
            string retStr = Type switch
            {
                RuntimeType.INT => ValueInt.ToString(),
                RuntimeType.BYTE => ValueByte.ToString(),
                RuntimeType.FLOAT => ValueFloat.ToString().Replace(',', '.') + "f",
                RuntimeType.CHAR => $"'{ValueChar.Escape()}'",
                _ => throw new RuntimeException("Can't parse " + Type.ToString() + " to STRING"),
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
            hash.Add(Type);
            switch (Type)
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
            this.Type == value.Type &&
            this.Type switch
            {
                RuntimeType.BYTE => valueByte == value.valueByte,
                RuntimeType.INT => valueInt == value.valueInt,
                RuntimeType.FLOAT => valueFloat == value.valueFloat,
                RuntimeType.CHAR => valueChar == value.valueChar,
                _ => throw new ImpossibleException(),
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
                switch (Type)
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

        #region Serialize
        public readonly void Serialize(Serializer serializer)
        {
            serializer.Serialize((byte)type);
            switch (type)
            {
                case RuntimeType.BYTE:
                    if (valueByte.HasValue)
                    { serializer.Serialize(valueByte.Value); }
                    else
                    { serializer.Serialize((byte)0); }
                    break;
                case RuntimeType.INT:
                    serializer.Serialize(valueInt.Value);
                    break;
                case RuntimeType.FLOAT:
                    serializer.Serialize(valueFloat.Value);
                    break;
                case RuntimeType.CHAR:
                    serializer.Serialize(valueChar.Value);
                    break;
                default: throw new ImpossibleException();
            }
            serializer.Serialize(Tag);
        }

        public void Deserialize(Deserializer deserializer)
        {
            type = (RuntimeType)deserializer.DeserializeByte();
            switch (type)
            {
                case RuntimeType.BYTE:
                    valueByte = deserializer.DeserializeByte();
                    break;
                case RuntimeType.INT:
                    valueInt = deserializer.DeserializeInt32();
                    break;
                case RuntimeType.FLOAT:
                    valueFloat = deserializer.DeserializeFloat();
                    break;
                case RuntimeType.CHAR:
                    valueChar = deserializer.DeserializeChar();
                    break;
                default: throw new ImpossibleException();
            }
            Tag = deserializer.DeserializeString();
        }

        public readonly Value SerializeText()
        {
            Value result = Value.Object();

            result["Type"] = Value.Literal(type.ToString());
            result["Value"] = type switch
            {
                RuntimeType.BYTE => Value.Literal(valueByte.Value),
                RuntimeType.INT => Value.Literal(valueInt.Value),
                RuntimeType.FLOAT => Value.Literal(valueFloat.Value),
                RuntimeType.CHAR => Value.Literal(valueChar.Value),
                _ => throw new ImpossibleException(),
            };
            result["Tag"] = Value.Literal(Tag);

            return result;
        }

        public void DeserializeText(Value data)
        {
            if (!Enum.TryParse(data["Type"].String ?? "", out type))
            { return; }

            switch (type)
            {
                case RuntimeType.BYTE:
                    valueByte = (byte)(data["Value"].Int ?? 0);
                    break;
                case RuntimeType.INT:
                    valueInt = data["Value"].Int ?? 0;
                    break;
                case RuntimeType.FLOAT:
                    valueFloat = data["Value"].Float ?? 0f;
                    break;
                case RuntimeType.CHAR:
                    valueChar = (char)(data["Value"].Int ?? 0);
                    break;
                default:
                    break;
            }
            Tag = data["Tag"].String;
        }
        #endregion

        /// <exception cref="NotImplementedException"/>
        /// <exception cref="ArgumentNullException"/>
        public static DataItem GetValue(object value, string tag = null)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            if (value is byte @byte)
            { return new DataItem(@byte, tag); }

            if (value is int @int)
            { return new DataItem(@int, tag); }

            if (value is float @float)
            { return new DataItem(@float, tag); }

            if (value is bool @bool)
            { return new DataItem(@bool, tag); }

            if (value is char @char)
            { return new DataItem(@char, tag); }

            if (value is Enum @enum)
            {
                TypeCode underlyingType = @enum.GetTypeCode();
                object enumValue = underlyingType switch
                {
                    TypeCode.Empty => throw new NotImplementedException(),
                    TypeCode.Object => throw new NotImplementedException(),
                    TypeCode.DBNull => throw new NotImplementedException(),
                    TypeCode.Boolean => (System.Boolean)value,
                    TypeCode.Char => (System.Char)value,
                    TypeCode.SByte => (System.SByte)value,
                    TypeCode.Byte => (System.Byte)value,
                    TypeCode.Int16 => (System.Int16)value,
                    TypeCode.UInt16 => (System.UInt16)value,
                    TypeCode.Int32 => (System.Int32)value,
                    TypeCode.UInt32 => (System.UInt32)value,
                    TypeCode.Int64 => (System.Int64)value,
                    TypeCode.UInt64 => (System.UInt64)value,
                    TypeCode.Single => (System.Single)value,
                    TypeCode.Double => (System.Double)value,
                    TypeCode.Decimal => (System.Decimal)value,
                    TypeCode.DateTime => (System.DateTime)value,
                    TypeCode.String => (System.String)value,
                    _ => throw new ImpossibleException(),
                };
                return DataItem.GetValue(enumValue, tag);
            }

            throw new NotImplementedException($"Type conversion for type {value.GetType()} not implemented");
        }

        public static DataItem GetDefaultValue(RuntimeType type, string tag = null)
            => type switch
            {
                RuntimeType.BYTE => new DataItem((byte)0, tag),
                RuntimeType.INT => new DataItem((int)0, tag),
                RuntimeType.FLOAT => new DataItem((float)0f, tag),
                RuntimeType.CHAR => new DataItem((char)'\0', tag),
                _ => DataItem.Null,
            };

        /// <exception cref="InternalException"/>
        public static DataItem GetDefaultValue(BBCode.Compiler.Type type, string tag = null)
            => type switch
            {
                BBCode.Compiler.Type.BYTE => new DataItem((byte)0, tag),
                BBCode.Compiler.Type.INT => new DataItem((int)0, tag),
                BBCode.Compiler.Type.FLOAT => new DataItem((float)0f, tag),
                BBCode.Compiler.Type.CHAR => new DataItem((char)'\0', tag),
                BBCode.Compiler.Type.NONE => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                BBCode.Compiler.Type.VOID => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                BBCode.Compiler.Type.UNKNOWN => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                _ => DataItem.Null,
            };
    }
}
