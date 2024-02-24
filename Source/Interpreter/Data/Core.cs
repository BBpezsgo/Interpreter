using System;
using System.Diagnostics;
using System.Globalization;
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

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    [StructLayout(LayoutKind.Explicit)]
    public partial struct DataItem
    {
        public static DataItem Null => default;

        public readonly RuntimeType Type => type;

        [FieldOffset(0)] RuntimeType type;

        #region Value Fields

        [FieldOffset(1)] byte valueUInt8;
        [FieldOffset(1)] int valueSInt32;
        [FieldOffset(1)] float valueSingle;
        [FieldOffset(1)] char valueUInt16;

        #endregion

        public readonly bool IsNull => type == RuntimeType.Null;

        #region Value Properties

        /// <exception cref="RuntimeException"/>
        public byte ValueUInt8
        {
            readonly get
            {
                if (Type == RuntimeType.UInt8)
                { return valueUInt8; }

                throw new RuntimeException($"Can't cast {Type} to byte");
            }
            set => valueUInt8 = value;
        }
        /// <exception cref="RuntimeException"/>
        public int ValueSInt32
        {
            readonly get
            {
                if (Type == RuntimeType.SInt32)
                { return valueSInt32; }

                throw new RuntimeException($"Can't cast {Type} to integer");
            }
            set => valueSInt32 = value;
        }
        /// <exception cref="RuntimeException"/>
        public float ValueSingle
        {
            readonly get
            {
                if (Type == RuntimeType.Single)
                { return valueSingle; }

                throw new RuntimeException($"Can't cast {Type} to float");
            }
            set => valueSingle = value;
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

                throw new RuntimeException($"Can't cast {Type} to char");
            }
            set => valueUInt16 = value;
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

        public readonly int? Integer => Type switch
        {
            RuntimeType.UInt8 => this.ValueUInt8,
            RuntimeType.SInt32 => this.ValueSInt32,
            RuntimeType.UInt16 => this.ValueUInt16,
            RuntimeType.Single => null,
            RuntimeType.Null => null,
            _ => throw new UnreachableException(),
        };

        public readonly byte? Byte
        {
            get
            {
                switch (type)
                {
                    case RuntimeType.UInt8:
                        return valueUInt8;
                    case RuntimeType.SInt32:
                        if (valueSInt32 is >= byte.MinValue and <= byte.MaxValue)
                        { return (byte)valueSInt32; }
                        return null;
                    case RuntimeType.Single:
                        if (!float.IsInteger(valueSingle)) return null;
                        if (valueSingle is >= byte.MinValue and <= byte.MaxValue)
                        { return (byte)valueSingle; }
                        return null;
                    case RuntimeType.UInt16:
                        if ((ushort)valueUInt16 is >= byte.MinValue and <= byte.MaxValue)
                        { return (byte)valueUInt16; }
                        return null;
                    default:
                        return null;
                }
            }
        }

        public readonly object? GetValue() => type switch
        {
            RuntimeType.Null => (object?)null,
            RuntimeType.UInt8 => (object)valueUInt8,
            RuntimeType.SInt32 => (object)valueSInt32,
            RuntimeType.Single => (object)valueSingle,
            RuntimeType.UInt16 => (object)valueUInt16,
            _ => throw new UnreachableException(),
        };

        public readonly string GetDebuggerDisplay() => Type switch
        {
            RuntimeType.Null => "null",
            RuntimeType.SInt32 => ValueSInt32.ToString(CultureInfo.InvariantCulture),
            RuntimeType.UInt8 => ValueUInt8.ToString(CultureInfo.InvariantCulture),
            RuntimeType.Single => ValueSingle.ToString(CultureInfo.InvariantCulture) + "f",
            RuntimeType.UInt16 => $"'{ValueUInt16.Escape()}'",
            _ => throw new UnreachableException(),
        };

        public override readonly int GetHashCode() => Type switch
        {
            RuntimeType.UInt8 => HashCode.Combine(Type, valueUInt8),
            RuntimeType.SInt32 => HashCode.Combine(Type, valueSInt32),
            RuntimeType.Single => HashCode.Combine(Type, valueSingle),
            RuntimeType.UInt16 => HashCode.Combine(Type, valueUInt16),
            _ => throw new UnreachableException(),
        };

        public readonly void DebugPrint()
        {
            ConsoleColor savedFgColor = Console.ForegroundColor;
            ConsoleColor savedBgColor = Console.BackgroundColor;

            switch (Type)
            {
                case RuntimeType.UInt8:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(valueUInt8);
                    break;
                case RuntimeType.SInt32:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(valueSInt32);
                    break;
                case RuntimeType.Single:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(valueSingle);
                    break;
                case RuntimeType.UInt16:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"\'{valueUInt16.Escape()}\'");
                    break;
                case RuntimeType.Null:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("null");
                    break;
                default: throw new UnreachableException();
            }

            Console.ResetColor();
            Console.ForegroundColor = savedFgColor;
            Console.BackgroundColor = savedBgColor;
        }

        /// <exception cref="NotImplementedException"/>
        /// <exception cref="ArgumentNullException"/>
        public static DataItem GetValue(object? value)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));

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
                    TypeCode.Boolean => (bool)value,
                    TypeCode.Char => (char)value,
                    TypeCode.SByte => (sbyte)value,
                    TypeCode.Byte => (byte)value,
                    TypeCode.Int16 => (short)value,
                    TypeCode.UInt16 => (ushort)value,
                    TypeCode.Int32 => (int)value,
                    TypeCode.UInt32 => (uint)value,
                    TypeCode.Int64 => (long)value,
                    TypeCode.UInt64 => (ulong)value,
                    TypeCode.Single => (float)value,
                    TypeCode.Double => (double)value,
                    TypeCode.Decimal => (decimal)value,
                    TypeCode.DateTime => (DateTime)value,
                    TypeCode.String => (string)value,
                    _ => throw new UnreachableException(),
                };
                return DataItem.GetValue(enumValue);
            }

            throw new NotImplementedException($"Type conversion for type {value.GetType()} not implemented");
        }

        public static DataItem GetDefaultValue(RuntimeType type) => type switch
        {
            RuntimeType.UInt8 => new DataItem((byte)0),
            RuntimeType.SInt32 => new DataItem((int)0),
            RuntimeType.Single => new DataItem((float)0f),
            RuntimeType.UInt16 => new DataItem((char)'\0'),
            _ => DataItem.Null,
        };

        /// <exception cref="InternalException"/>
        public static DataItem GetDefaultValue(Compiler.Type type) => type switch
        {
            Compiler.Type.Byte => new DataItem((byte)0),
            Compiler.Type.Integer => new DataItem((int)0),
            Compiler.Type.Float => new DataItem((float)0f),
            Compiler.Type.Char => new DataItem((char)'\0'),
            Compiler.Type.NotBuiltin => throw new InternalException($"Type \"{type.ToString().ToLowerInvariant()}\" does not have a default value"),
            Compiler.Type.Void => throw new InternalException($"Type \"{type.ToString().ToLowerInvariant()}\" does not have a default value"),
            Compiler.Type.Unknown => throw new InternalException($"Type \"{type.ToString().ToLowerInvariant()}\" does not have a default value"),
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
