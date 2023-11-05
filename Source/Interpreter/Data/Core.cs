using System;
using System.Runtime.InteropServices;

namespace LanguageCore.Runtime
{
    public enum RuntimeType : byte
    {
        Null,
        UInt8,
        SInt32,
        Single,
        UInt16,
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
        byte valueUInt8;
        [FieldOffset(1)]
        int valueSInt32;
        [FieldOffset(1)]
        float valueSingle;
        [FieldOffset(1)]
        char valueUInt16;

        #endregion

        public readonly bool IsNull
        {
            get
            {
                if (valueUInt8 != 0) return false;
                if (valueSInt32 != 0) return false;
                if (valueSingle != 0) return false;
                if (valueUInt16 != 0) return false;
                return type == RuntimeType.Null;
            }
        }

        #region Value Properties

        /// <exception cref="RuntimeException"/>
        public byte ValueUInt8
        {
            readonly get
            {
                if (Type == RuntimeType.UInt8)
                { return valueUInt8; }

                throw new RuntimeException("Can't cast " + Type.ToString().ToLower() + " to byte");
            }
            set
            {
                valueUInt8 = value;
            }
        }
        /// <exception cref="RuntimeException"/>
        public int ValueSInt32
        {
            readonly get
            {
                if (Type == RuntimeType.SInt32)
                { return valueSInt32; }

                throw new RuntimeException("Can't cast " + Type.ToString().ToLower() + " to integer");
            }
            set
            {
                valueSInt32 = value;
            }
        }
        /// <exception cref="RuntimeException"/>
        public float ValueSingle
        {
            readonly get
            {
                if (Type == RuntimeType.Single)
                { return valueSingle; }

                throw new RuntimeException("Can't cast " + Type.ToString().ToLower() + " to float");
            }
            set
            {
                valueSingle = value;
            }
        }
        /// <exception cref="RuntimeException"/>
        public char ValueUInt16
        {
            readonly get
            {
                if (Type == RuntimeType.UInt16)
                { return valueUInt16; }

                if (Type == RuntimeType.SInt32)
                { return (char)valueSInt32; }

                throw new RuntimeException($"Can't cast {Type.ToString().ToLower()} to char");
            }
            set
            {
                valueUInt16 = value;
            }
        }

        #endregion

        #region Constructors

        DataItem(RuntimeType type)
        {
            this.type = type;

            this.valueSInt32 = default;
            this.valueUInt8 = default;
            this.valueSingle = default;
            this.valueUInt16 = default;

            // this.Tag = tag;
        }

        public DataItem(int value) : this(RuntimeType.SInt32)
        { this.valueSInt32 = value; }
        public DataItem(byte value) : this(RuntimeType.UInt8)
        { this.valueUInt8 = value; }
        public DataItem(float value) : this(RuntimeType.Single)
        { this.valueSingle = value; }
        public DataItem(char value) : this(RuntimeType.UInt16)
        { this.valueUInt16 = value; }
        public DataItem(bool value) : this(value ? 1 : 0)
        { }

        #endregion

        /// <exception cref="ImpossibleException"></exception>
        public readonly bool Boolean => this.Type switch
        {
            RuntimeType.UInt8 => this.valueUInt8 != 0,
            RuntimeType.SInt32 => this.valueSInt32 != 0,
            RuntimeType.Single => this.valueSingle != 0f,
            RuntimeType.UInt16 => this.valueUInt16 != 0,
            RuntimeType.Null => false,
            _ => throw new ImpossibleException(),
        };
        /// <exception cref="ImpossibleException"></exception>
        public readonly int? Integer => Type switch
        {
            RuntimeType.UInt8 => this.ValueUInt8,
            RuntimeType.SInt32 => this.ValueSInt32,
            RuntimeType.UInt16 => this.ValueUInt16,
            RuntimeType.Single => null,
            RuntimeType.Null => null,
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
            RuntimeType.UInt8 => this.ValueUInt8,
            RuntimeType.SInt32 => this.ValueSInt32,
            RuntimeType.Single => this.ValueSingle,
            RuntimeType.UInt16 => this.ValueUInt16,
            RuntimeType.Null => 0f,
            _ => throw new ImpossibleException(),
        };

        public readonly object? GetValue() => type switch
        {
            RuntimeType.UInt8 => (object)valueUInt8,
            RuntimeType.SInt32 => (object)valueSInt32,
            RuntimeType.Single => (object)valueSingle,
            RuntimeType.UInt16 => (object)valueUInt16,
            RuntimeType.Null => (object?)null,
            _ => throw new ImpossibleException(),
        };

        /// <exception cref="ImpossibleException"/>
        public readonly override string ToString() => this.IsNull ? "null" : Type switch
        {
            RuntimeType.SInt32 => ValueSInt32.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.UInt8 => ValueUInt8.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.Single => ValueSingle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.UInt16 => ValueUInt16.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.Null => "null",
            _ => throw new ImpossibleException(),
        };

        /// <exception cref="ImpossibleException"/>
        public readonly string GetDebuggerDisplay()
        {
            if (IsNull) return "null";
            return Type switch
            {
                RuntimeType.SInt32 => ValueSInt32.ToString(),
                RuntimeType.UInt8 => ValueUInt8.ToString(),
                RuntimeType.Single => ValueSingle.ToString().Replace(',', '.') + "f",
                RuntimeType.UInt16 => $"'{ValueUInt16.Escape()}'",
                RuntimeType.Null => "null",
                _ => throw new ImpossibleException(),
            };
        }

        public readonly override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Type);
            switch (Type)
            {
                case RuntimeType.UInt8:
                    hash.Add(valueUInt8);
                    break;
                case RuntimeType.SInt32:
                    hash.Add(valueSInt32);
                    break;
                case RuntimeType.Single:
                    hash.Add(valueSingle);
                    break;
                case RuntimeType.UInt16:
                    hash.Add(valueUInt16);
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
                RuntimeType.UInt8 => valueUInt8 == value.valueUInt8,
                RuntimeType.SInt32 => valueSInt32 == value.valueSInt32,
                RuntimeType.Single => valueSingle == value.valueSingle,
                RuntimeType.UInt16 => valueUInt16 == value.valueUInt16,
                RuntimeType.Null => false,
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
                    case RuntimeType.UInt8:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(this.valueUInt8.ToString(ci));
                        break;
                    case RuntimeType.SInt32:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(this.valueSInt32.ToString(ci));
                        break;
                    case RuntimeType.Single:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(this.valueSingle.ToString(ci));
                        break;
                    case RuntimeType.UInt16:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("'" + this.valueUInt16.ToString(ci) + "'");
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
                RuntimeType.UInt8 => new DataItem((byte)0),
                RuntimeType.SInt32 => new DataItem((int)0),
                RuntimeType.Single => new DataItem((float)0f),
                RuntimeType.UInt16 => new DataItem((char)'\0'),
                _ => DataItem.Null,
            };

        /// <exception cref="InternalException"/>
        public static DataItem GetDefaultValue(BBCode.Compiler.Type type)
            => type switch
            {
                BBCode.Compiler.Type.Byte => new DataItem((byte)0),
                BBCode.Compiler.Type.Integer => new DataItem((int)0),
                BBCode.Compiler.Type.Float => new DataItem((float)0f),
                BBCode.Compiler.Type.Char => new DataItem((char)'\0'),
                BBCode.Compiler.Type.NotBuiltin => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                BBCode.Compiler.Type.Void => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                BBCode.Compiler.Type.Unknown => throw new InternalException($"Type \"{type.ToString().ToLower()}\" does not have a default value"),
                _ => DataItem.Null,
            };

        public static bool TryShrinkToByte(DataItem value, out DataItem result)
        {
            switch (value.type)
            {
                case RuntimeType.UInt8:
                    result = value;
                    return true;
                case RuntimeType.SInt32:
                    if (value.valueSInt32 < byte.MinValue || value.valueSInt32 > byte.MaxValue)
                    {
                        result = default;
                        return false;
                    }
                    result = new DataItem((byte)value.valueSInt32);
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
                case RuntimeType.UInt8:
                    return true;
                case RuntimeType.SInt32:
                    if (value.valueSInt32 < byte.MinValue || value.valueSInt32 > byte.MaxValue)
                    {
                        return false;
                    }
                    value = new DataItem((byte)value.valueSInt32);
                    return true;
                default:
                    return false;
            }
        }
    }
}
