using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace LanguageCore.Runtime
{
    public partial struct DataItem :
        INumberBase<DataItem>,
        IBinaryNumber<DataItem>,
        INumber<DataItem>,
        IUnsignedNumber<DataItem>,
        ISignedNumber<DataItem>,
        IComparable,
        IConvertible,
        ISpanFormattable,
        IComparable<DataItem>,
        IEquatable<DataItem>
    {
        public static DataItem One => new(1);
        public static DataItem NegativeOne => new(-1);
        public static DataItem Zero => new(0);
        public static int Radix => throw new NotImplementedException();

        public static DataItem AdditiveIdentity => throw new NotImplementedException();
        public static DataItem MultiplicativeIdentity => throw new NotImplementedException();

        public static DataItem Abs(DataItem value) => value.type switch
        {
            RuntimeType.Null => value,
            RuntimeType.UInt8 => value,
            RuntimeType.SInt32 => new DataItem(Math.Abs(value.valueSInt32)),
            RuntimeType.Single => new DataItem(Math.Abs(value.valueSingle)),
            RuntimeType.UInt16 => value,
            _ => throw new ImpossibleException(),
        };

        public static bool IsCanonical(DataItem value) => throw new NotImplementedException();
        public static bool IsComplexNumber(DataItem value) => false;
        public static bool IsImaginaryNumber(DataItem value) => false;
        public static bool IsNormal(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsNormal(value.valueSingle),
            _ => true,
        };
        public static bool IsRealNumber(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsRealNumber(value.valueSingle),
            _ => true,
        };
        public static bool IsSubnormal(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsSubnormal(value.valueSingle),
            _ => throw new NotImplementedException(),
        };

        public static bool IsEvenInteger(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.UInt8 => byte.IsEvenInteger(value.valueUInt8),
            RuntimeType.SInt32 => int.IsEvenInteger(value.valueSInt32),
            RuntimeType.Single => float.IsEvenInteger(value.valueSingle),
            RuntimeType.UInt16 => ushort.IsEvenInteger(value.valueUInt16),
            _ => throw new ImpossibleException(),
        };
        public static bool IsOddInteger(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.UInt8 => byte.IsOddInteger(value.valueUInt8),
            RuntimeType.SInt32 => int.IsOddInteger(value.valueSInt32),
            RuntimeType.Single => float.IsOddInteger(value.valueSingle),
            RuntimeType.UInt16 => ushort.IsOddInteger(value.valueUInt16),
            _ => throw new ImpossibleException(),
        };

        public static bool IsFinite(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsFinite(value.valueSingle),
            _ => true,
        };
        public static bool IsInfinity(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsInfinity(value.valueSingle),
            _ => false,
        };

        public static bool IsInteger(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsInfinity(value.valueSingle),
            _ => true,
        };

        public static bool IsNaN(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsNaN(value.valueSingle),
            _ => false,
        };
        public static bool IsZero(DataItem value) => value.type switch
        {
            RuntimeType.Null => true,
            RuntimeType.UInt8 => value.valueUInt8 == 0,
            RuntimeType.SInt32 => value.valueSInt32 == 0,
            RuntimeType.Single => value.valueSingle == 0,
            RuntimeType.UInt16 => value.valueUInt16 == 0,
            _ => throw new ImpossibleException(),
        };

        public static bool IsNegative(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.SInt32 => int.IsNegative(value.valueSInt32),
            RuntimeType.Single => float.IsNegative(value.valueSingle),
            _ => false,
        };
        public static bool IsPositive(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.SInt32 => int.IsPositive(value.valueSInt32),
            RuntimeType.Single => float.IsPositive(value.valueSingle),
            _ => true,
        };

        public static bool IsNegativeInfinity(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsNegativeInfinity(value.valueSingle),
            _ => false,
        };
        public static bool IsPositiveInfinity(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.Single => float.IsPositiveInfinity(value.valueSingle),
            _ => false,
        };

        public static bool IsPow2(DataItem value) => value.type switch
        {
            RuntimeType.Null => false,
            RuntimeType.UInt8 => byte.IsPow2(value.valueUInt8),
            RuntimeType.SInt32 => int.IsPow2(value.valueSInt32),
            RuntimeType.Single => float.IsPow2(value.valueSingle),
            RuntimeType.UInt16 => ushort.IsPow2(value.valueUInt16),
            _ => throw new ImpossibleException(),
        };

        public static DataItem Log2(DataItem value) => value.type switch
        {
            RuntimeType.Null => value,
            RuntimeType.UInt8 => new DataItem(byte.Log2(value.valueUInt8)),
            RuntimeType.SInt32 => new DataItem(int.Log2(value.valueSInt32)),
            RuntimeType.Single => new DataItem(float.Log2(value.valueSingle)),
            RuntimeType.UInt16 => new DataItem(ushort.Log2(value.valueUInt16)),
            _ => throw new ImpossibleException(),
        };

        /// <inheritdoc/>
        /// <exception cref="InternalException"/>
        public readonly int CompareTo(object? obj) => obj is not DataItem other ? 0 : CompareTo(other);
        /// <inheritdoc/>
        /// <exception cref="InternalException"/>
        public readonly int CompareTo(DataItem other)
        {
            DataItem self = this;
            DataItem.MakeSameType(ref self, ref other);
            return self.type switch
            {
                RuntimeType.Null => 0,
                RuntimeType.UInt8 => self.valueUInt8.CompareTo(other.valueUInt8),
                RuntimeType.SInt32 => self.valueSInt32.CompareTo(other.valueSInt32),
                RuntimeType.Single => self.valueSingle.CompareTo(other.valueSingle),
                RuntimeType.UInt16 => self.valueUInt16.CompareTo(other.valueUInt16),
                _ => 0,
            };
        }

        /// <inheritdoc/>
        /// <exception cref="InternalException"/>
        public static DataItem MaxMagnitude(DataItem x, DataItem y)
        {
            DataItem.MakeSameType(ref x, ref y);
            return x.type switch
            {
                RuntimeType.Null => DataItem.Null,
                RuntimeType.UInt8 => new DataItem(byte.Max(x.valueUInt8, y.valueUInt8)),
                RuntimeType.SInt32 => new DataItem(int.MaxMagnitude(x.valueSInt32, y.valueSInt32)),
                RuntimeType.Single => new DataItem(float.MaxMagnitude(x.valueSingle, y.valueSingle)),
                RuntimeType.UInt16 => new DataItem(ushort.Max(x.valueUInt16, y.valueUInt16)),
                _ => throw new ImpossibleException(),
            };
        }
        /// <inheritdoc/>
        /// <exception cref="InternalException"/>
        public static DataItem MaxMagnitudeNumber(DataItem x, DataItem y)
        {
            DataItem.MakeSameType(ref x, ref y);
            return x.type switch
            {
                RuntimeType.Null => DataItem.Null,
                RuntimeType.UInt8 => new DataItem(byte.Max(x.valueUInt8, y.valueUInt8)),
                RuntimeType.SInt32 => new DataItem(int.MaxMagnitude(x.valueSInt32, y.valueSInt32)),
                RuntimeType.Single => new DataItem(float.MaxMagnitudeNumber(x.valueSingle, y.valueSingle)),
                RuntimeType.UInt16 => new DataItem(ushort.Max(x.valueUInt16, y.valueUInt16)),
                _ => throw new ImpossibleException(),
            };
        }

        /// <inheritdoc/>
        /// <exception cref="InternalException"/>
        public static DataItem MinMagnitude(DataItem x, DataItem y)
        {
            DataItem.MakeSameType(ref x, ref y);
            return x.type switch
            {
                RuntimeType.Null => DataItem.Null,
                RuntimeType.UInt8 => new DataItem(byte.Min(x.valueUInt8, y.valueUInt8)),
                RuntimeType.SInt32 => new DataItem(int.MinMagnitude(x.valueSInt32, y.valueSInt32)),
                RuntimeType.Single => new DataItem(float.MinMagnitude(x.valueSingle, y.valueSingle)),
                RuntimeType.UInt16 => new DataItem(ushort.Min(x.valueUInt16, y.valueUInt16)),
                _ => throw new ImpossibleException(),
            };
        }
        /// <inheritdoc/>
        /// <exception cref="InternalException"/>
        public static DataItem MinMagnitudeNumber(DataItem x, DataItem y)
        {
            DataItem.MakeSameType(ref x, ref y);
            return x.type switch
            {
                RuntimeType.Null => DataItem.Null,
                RuntimeType.UInt8 => new DataItem(byte.Min(x.valueUInt8, y.valueUInt8)),
                RuntimeType.SInt32 => new DataItem(int.MinMagnitude(x.valueSInt32, y.valueSInt32)),
                RuntimeType.Single => new DataItem(float.MinMagnitudeNumber(x.valueSingle, y.valueSingle)),
                RuntimeType.UInt16 => new DataItem(ushort.Min(x.valueUInt16, y.valueUInt16)),
                _ => throw new ImpossibleException(),
            };
        }

        public static DataItem Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
        {
            if (int.TryParse(s, style, provider, out int integer))
            { return new DataItem(integer); }

            if (float.TryParse(s, style, provider, out float @float))
            { return new DataItem(@float); }

            if (s.Length == 1)
            { return new DataItem(s[0]); }

            throw new FormatException();
        }
        public static DataItem Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            if (int.TryParse(s, provider, out int integer))
            { return new DataItem(integer); }

            if (float.TryParse(s, provider, out float @float))
            { return new DataItem(@float); }

            if (s.Length == 1)
            { return new DataItem(s[0]); }

            throw new FormatException();
        }

        public static DataItem Parse(string s, NumberStyles style, IFormatProvider? provider)
        {
            if (int.TryParse(s, style, provider, out int integer))
            { return new DataItem(integer); }

            if (float.TryParse(s, style, provider, out float @float))
            { return new DataItem(@float); }

            if (char.TryParse(s, out char character))
            { return new DataItem(character); }

            throw new FormatException();
        }
        public static DataItem Parse(string s, IFormatProvider? provider)
        {
            if (int.TryParse(s, provider, out int integer))
            { return new DataItem(integer); }

            if (float.TryParse(s, provider, out float @float))
            { return new DataItem(@float); }

            if (char.TryParse(s, out char character))
            { return new DataItem(character); }

            throw new FormatException();
        }

        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out DataItem result)
        {
            if (int.TryParse(s, style, provider, out int integer))
            {
                result = new DataItem(integer);
                return true;
            }

            if (float.TryParse(s, style, provider, out float @float))
            {
                result = new DataItem(@float);
                return true;
            }

            if (s.Length == 1)
            {
                result = new DataItem(s[0]);
                return true;
            }

            result = default;
            return false;
        }
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out DataItem result)
        {
            if (int.TryParse(s, provider, out int integer))
            {
                result = new DataItem(integer);
                return true;
            }

            if (float.TryParse(s, provider, out float @float))
            {
                result = new DataItem(@float);
                return true;
            }

            if (s.Length == 1)
            {
                result = new DataItem(s[0]);
                return true;
            }

            result = default;
            return false;
        }

        public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out DataItem result)
        {
            if (int.TryParse(s, style, provider, out int integer))
            {
                result = new DataItem(integer);
                return true;
            }

            if (float.TryParse(s, style, provider, out float @float))
            {
                result = new DataItem(@float);
                return true;
            }

            if (char.TryParse(s, out char character))
            {
                result = new DataItem(character);
                return true;
            }

            result = default;
            return false;
        }
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out DataItem result)
        {
            if (int.TryParse(s, provider, out int integer))
            {
                result = new DataItem(integer);
                return true;
            }

            if (float.TryParse(s, provider, out float @float))
            {
                result = new DataItem(@float);
                return true;
            }

            if (char.TryParse(s, out char character))
            {
                result = new DataItem(character);
                return true;
            }

            result = default;
            return false;
        }

        public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            charsWritten = default;
            return type switch
            {
                RuntimeType.Null => false,
                RuntimeType.UInt8 => valueUInt8.TryFormat(destination, out charsWritten, format, provider),
                RuntimeType.SInt32 => valueSInt32.TryFormat(destination, out charsWritten, format, provider),
                RuntimeType.Single => valueSingle.TryFormat(destination, out charsWritten, format, provider),
                RuntimeType.UInt16 => false,
                _ => false,
            };
        }

        static bool INumberBase<DataItem>.TryConvertFromChecked<TOther>(TOther value, out DataItem result) => throw new NotImplementedException();
        static bool INumberBase<DataItem>.TryConvertFromSaturating<TOther>(TOther value, out DataItem result) => throw new NotImplementedException();
        static bool INumberBase<DataItem>.TryConvertFromTruncating<TOther>(TOther value, out DataItem result) => throw new NotImplementedException();
        static bool INumberBase<DataItem>.TryConvertToChecked<TOther>(DataItem value, out TOther result) => throw new NotImplementedException();
        static bool INumberBase<DataItem>.TryConvertToSaturating<TOther>(DataItem value, out TOther result) => throw new NotImplementedException();
        static bool INumberBase<DataItem>.TryConvertToTruncating<TOther>(DataItem value, out TOther result) => throw new NotImplementedException();

        public readonly TypeCode GetTypeCode() => type switch
        {
            RuntimeType.Null => TypeCode.Empty,
            RuntimeType.UInt8 => TypeCode.Byte,
            RuntimeType.SInt32 => TypeCode.Int32,
            RuntimeType.Single => TypeCode.Single,
            RuntimeType.UInt16 => TypeCode.UInt16,
            _ => throw new ImpossibleException(),
        };

        public readonly bool ToBoolean(IFormatProvider? provider) => Type switch
        {
            RuntimeType.Null => false,
            RuntimeType.UInt8 => valueUInt8 != 0,
            RuntimeType.SInt32 => valueSInt32 != 0,
            RuntimeType.Single => valueSingle != 0f,
            RuntimeType.UInt16 => valueUInt16 != 0,
            _ => throw new ImpossibleException(),
        };

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly byte ToByte(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return (byte)valueUInt8;
                case RuntimeType.SInt32:
                    return checked((byte)valueSInt32);
                case RuntimeType.UInt16:
                    return checked((byte)valueUInt16);
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return checked((byte)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.Byte)}");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly char ToChar(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return (char)(ushort)valueUInt8;
                case RuntimeType.SInt32:
                    return (char)checked((ushort)valueSInt32);
                case RuntimeType.UInt16:
                    return valueUInt16;
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return (char)checked((ushort)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.Char)}");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidCastException"/>
        [DoesNotReturn]
        public readonly DateTime ToDateTime(IFormatProvider? provider) => throw new InvalidCastException($"Can't cast {type} to {nameof(System.DateTime)}");

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly decimal ToDecimal(IFormatProvider? provider) => type switch
        {
            RuntimeType.UInt8 => (decimal)valueUInt8,
            RuntimeType.SInt32 => (decimal)valueSInt32,
            RuntimeType.UInt16 => (decimal)valueUInt16,
            RuntimeType.Single => checked((decimal)valueSingle),
            _ => throw new InvalidCastException($"Can't cast {type} to {nameof(System.Decimal)}"),
        };

        /// <inheritdoc/>
        /// <exception cref="InvalidCastException"/>
        public readonly double ToDouble(IFormatProvider? provider) => type switch
        {
            RuntimeType.UInt8 => (double)valueUInt8,
            RuntimeType.SInt32 => (double)valueSInt32,
            RuntimeType.UInt16 => (double)valueUInt16,
            RuntimeType.Single => (double)valueSingle,
            _ => throw new InvalidCastException($"Can't cast {type} to {nameof(System.Double)}"),
        };

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly short ToInt16(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return (short)valueUInt8;
                case RuntimeType.SInt32:
                    return checked((short)valueSInt32);
                case RuntimeType.UInt16:
                    return checked((short)valueUInt16);
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return checked((short)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.Int16)}");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly int ToInt32(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return (int)valueUInt8;
                case RuntimeType.SInt32:
                    return (int)valueSInt32;
                case RuntimeType.UInt16:
                    return (int)valueUInt16;
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return checked((int)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.Int32)}");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly long ToInt64(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return (long)valueUInt8;
                case RuntimeType.SInt32:
                    return (long)valueSInt32;
                case RuntimeType.UInt16:
                    return (long)valueUInt16;
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return checked((long)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.Int64)}");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly sbyte ToSByte(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return checked((sbyte)valueUInt8);
                case RuntimeType.SInt32:
                    return checked((sbyte)valueSInt32);
                case RuntimeType.UInt16:
                    return checked((sbyte)valueUInt16);
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return checked((sbyte)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.SByte)}");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidCastException"/>
        public readonly float ToSingle(IFormatProvider? provider) => type switch
        {
            RuntimeType.UInt8 => (float)valueUInt8,
            RuntimeType.SInt32 => (float)valueSInt32,
            RuntimeType.UInt16 => (float)valueUInt16,
            RuntimeType.Single => (float)valueSingle,
            _ => throw new InvalidCastException($"Can't cast {type} to {nameof(System.Single)}"),
        };

        public readonly override string ToString() => this.ToString(CultureInfo.InvariantCulture);

        public readonly string ToString(IFormatProvider? provider) => type switch
        {
            RuntimeType.Null => "null",
            RuntimeType.UInt8 => valueUInt8.ToString(provider),
            RuntimeType.SInt32 => valueSInt32.ToString(provider),
            RuntimeType.UInt16 => valueUInt16.ToString(provider),
            RuntimeType.Single => valueSingle.ToString(provider),
            _ => throw new ImpossibleException(),
        };

        public readonly string ToString(string? format, IFormatProvider? formatProvider) => type switch
        {
            RuntimeType.Null => "null",
            RuntimeType.UInt8 => valueUInt8.ToString(format, formatProvider),
            RuntimeType.SInt32 => valueSInt32.ToString(format, formatProvider),
            RuntimeType.Single => valueSingle.ToString(format, formatProvider),
            RuntimeType.UInt16 => valueUInt16.ToString(formatProvider),
            _ => throw new ImpossibleException(),
        };

        /// <inheritdoc/>
        /// <exception cref="InvalidCastException"/>
        [DoesNotReturn]
        public readonly object ToType(Type conversionType, IFormatProvider? provider) => throw new InvalidCastException($"Can't cast {type} to {conversionType}");

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly ushort ToUInt16(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return (ushort)valueUInt8;
                case RuntimeType.SInt32:
                    return checked((ushort)valueSInt32);
                case RuntimeType.UInt16:
                    return (ushort)valueUInt16;
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return checked((ushort)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.UInt16)}");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly uint ToUInt32(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return (uint)valueUInt8;
                case RuntimeType.SInt32:
                    return checked((uint)valueSInt32);
                case RuntimeType.UInt16:
                    return (uint)valueUInt16;
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return checked((uint)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.UInt32)}");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="InvalidCastException"/>
        public readonly ulong ToUInt64(IFormatProvider? provider)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    return (ulong)valueUInt8;
                case RuntimeType.SInt32:
                    return checked((ulong)valueSInt32);
                case RuntimeType.UInt16:
                    return (ulong)valueUInt16;
                case RuntimeType.Single:
                    if (float.IsInteger(valueSingle))
                    { return checked((ulong)valueSingle); }
                    throw new OverflowException();
                default:
                    throw new InvalidCastException($"Can't cast {type} to {nameof(System.UInt64)}");
            }
        }
    }
}
