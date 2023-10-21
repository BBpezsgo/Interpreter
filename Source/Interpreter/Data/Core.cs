using System;
using System.Runtime.InteropServices;

namespace LanguageCore.Runtime
{
    public enum RuntimeType : byte
    {
        NULL,
        BYTE,
        INT,
        FLOAT,
        CHAR,
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    [StructLayout(LayoutKind.Explicit)]
    public partial struct DataItem
    {
        public static DataItem Null => new() { };

        public readonly RuntimeType Type => type;

        [FieldOffset(0)]
        RuntimeType type;

        #region Value Fields

        [FieldOffset(1)]
        byte valueByte;
        [FieldOffset(1)]
        int valueInt;
        [FieldOffset(1)]
        float valueFloat;
        [FieldOffset(1)]
        char valueChar;

        #endregion

        public readonly bool IsNull
        {
            get
            {
                if (valueByte != 0) return false;
                if (valueInt != 0) return false;
                if (valueFloat != 0) return false;
                if (valueChar != 0) return false;
                return type == RuntimeType.NULL;
            }
        }

        #region Value Properties

        /// <exception cref="RuntimeException"/>
        public byte ValueByte
        {
            readonly get
            {
                if (Type == RuntimeType.BYTE)
                { return valueByte; }

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
                { return valueInt; }

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
                { return valueFloat; }

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
                { return valueChar; }

                if (Type == RuntimeType.INT)
                { return (char)valueInt; }

                throw new RuntimeException($"Can't cast {Type.ToString().ToLower()} to char");
            }
            set
            {
                valueChar = value;
            }
        }

        #endregion

        #region Constructors

        DataItem(RuntimeType type)
        {
            this.type = type;

            this.valueInt = default;
            this.valueByte = default;
            this.valueFloat = default;
            this.valueChar = default;

            // this.Tag = tag;
        }

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

        /// <exception cref="ImpossibleException"></exception>
        public readonly bool Boolean => this.Type switch
        {
            RuntimeType.BYTE => this.valueByte != 0,
            RuntimeType.INT => this.valueInt != 0,
            RuntimeType.FLOAT => this.valueFloat != 0f,
            RuntimeType.CHAR => this.valueChar != 0,
            RuntimeType.NULL => false,
            _ => throw new ImpossibleException(),
        };
        /// <exception cref="ImpossibleException"></exception>
        public readonly int? Integer => Type switch
        {
            RuntimeType.BYTE => this.ValueByte,
            RuntimeType.INT => this.ValueInt,
            RuntimeType.CHAR => this.ValueChar,
            RuntimeType.FLOAT => null,
            RuntimeType.NULL => null,
            _ => throw new ImpossibleException(),
        };
        public readonly byte? Byte
        {
            get
            {
                int? integer_ = this.Integer;

                if (!integer_.HasValue)
                { return null; }

                if (integer_.Value < byte.MinValue || integer_.Value > byte.MaxValue)
                { return null; }

                return (byte)integer_.Value;
            }
        }
        /// <exception cref="ImpossibleException"></exception>
        public readonly float Float => Type switch
        {
            RuntimeType.BYTE => this.ValueByte,
            RuntimeType.INT => this.ValueInt,
            RuntimeType.FLOAT => this.ValueFloat,
            RuntimeType.CHAR => this.ValueChar,
            RuntimeType.NULL => 0f,
            _ => throw new ImpossibleException(),
        };

        public readonly object? GetValue() => type switch
        {
            RuntimeType.BYTE => (object)valueByte,
            RuntimeType.INT => (object)valueInt,
            RuntimeType.FLOAT => (object)valueFloat,
            RuntimeType.CHAR => (object)valueChar,
            RuntimeType.NULL => (object?)null,
            _ => throw new ImpossibleException(),
        };

        /// <exception cref="ImpossibleException"/>
        public readonly override string ToString() => this.IsNull ? "null" : Type switch
        {
            RuntimeType.INT => ValueInt.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.BYTE => ValueByte.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.FLOAT => ValueFloat.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.CHAR => ValueChar.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.NULL => "null",
            _ => throw new ImpossibleException(),
        };

        /// <exception cref="ImpossibleException"/>
        public readonly string GetDebuggerDisplay()
        {
            if (IsNull) return "null";
            return Type switch
            {
                RuntimeType.INT => ValueInt.ToString(),
                RuntimeType.BYTE => ValueByte.ToString(),
                RuntimeType.FLOAT => ValueFloat.ToString().Replace(',', '.') + "f",
                RuntimeType.CHAR => $"'{ValueChar.Escape()}'",
                RuntimeType.NULL => "null",
                _ => throw new ImpossibleException(),
            };
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
                default: throw new ImpossibleException();
            }
            return hash.ToHashCode();
        }

        /// <exception cref="NotImplementedException"/>
        public readonly override bool Equals(object? obj)
            => obj is DataItem value &&
            this.Type == value.Type &&
            this.Type switch
            {
                RuntimeType.BYTE => valueByte == value.valueByte,
                RuntimeType.INT => valueInt == value.valueInt,
                RuntimeType.FLOAT => valueFloat == value.valueFloat,
                RuntimeType.CHAR => valueChar == value.valueChar,
                RuntimeType.NULL => false,
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
                        Console.Write(this.valueByte.ToString(ci));
                        break;
                    case RuntimeType.INT:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(this.valueInt.ToString(ci));
                        break;
                    case RuntimeType.FLOAT:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(this.valueFloat.ToString(ci));
                        break;
                    case RuntimeType.CHAR:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("'" + this.valueChar.ToString(ci) + "'");
                        break;
                    default: throw new ImpossibleException();
                }
            }

            Console.ResetColor();
            Console.ForegroundColor = savedFgColor;
            Console.BackgroundColor = savedBgColor;
        }

        /// <exception cref="NotImplementedException"/>
        /// <exception cref="ArgumentNullException"/>
        public static DataItem GetValue(object value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            if (value is byte @byte)
            { return new DataItem(@byte); }

            if (value is int @int)
            { return new DataItem(@int); }

            if (value is float @float)
            { return new DataItem(@float); }

            if (value is bool @bool)
            { return new DataItem(@bool); }

            if (value is char @char)
            { return new DataItem(@char); }

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
                return DataItem.GetValue(enumValue);
            }

            throw new NotImplementedException($"Type conversion for type {value.GetType()} not implemented");
        }

        public static DataItem GetDefaultValue(RuntimeType type)
            => type switch
            {
                RuntimeType.BYTE => new DataItem((byte)0),
                RuntimeType.INT => new DataItem((int)0),
                RuntimeType.FLOAT => new DataItem((float)0f),
                RuntimeType.CHAR => new DataItem((char)'\0'),
                _ => DataItem.Null,
            };

        /// <exception cref="InternalException"/>
        public static DataItem GetDefaultValue(BBCode.Compiler.Type type)
            => type switch
            {
                BBCode.Compiler.Type.BYTE => new DataItem((byte)0),
                BBCode.Compiler.Type.INT => new DataItem((int)0),
                BBCode.Compiler.Type.FLOAT => new DataItem((float)0f),
                BBCode.Compiler.Type.CHAR => new DataItem((char)'\0'),
                BBCode.Compiler.Type.NONE => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                BBCode.Compiler.Type.VOID => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                BBCode.Compiler.Type.UNKNOWN => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                _ => DataItem.Null,
            };

        public static bool TryShrinkToByte(DataItem value, out DataItem result)
        {
            switch (value.type)
            {
                case RuntimeType.BYTE:
                    result = value;
                    return true;
                case RuntimeType.INT:
                    if (value.valueInt < byte.MinValue || value.valueInt > byte.MaxValue)
                    {
                        result = default;
                        return false;
                    }
                    result = new DataItem((byte)value.valueInt);
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        public static bool TryShrinkToByte(ref DataItem value)
        {
            switch (value.type)
            {
                case RuntimeType.BYTE:
                    return true;
                case RuntimeType.INT:
                    if (value.valueInt < byte.MinValue || value.valueInt > byte.MaxValue)
                    {
                        return false;
                    }
                    value = new DataItem((byte)value.valueInt);
                    return true;
                default:
                    return false;
            }
        }
    }
}
