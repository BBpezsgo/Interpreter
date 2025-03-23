using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public enum BasicType : byte
{
    Void,
    Any,
    U8,
    I8,
    U16,
    I16,
    U32,
    I32,
    U64,
    I64,
    F32,
}

public delegate bool ComputeValue(StatementWithValue value, out CompiledValue computedValue);
public delegate bool FindType(Token token, Uri relevantFile, [NotNullWhen(true)] out GeneralType? computedValue);

public static class TypeExtensions
{
    public static bool TryGetNumericType(this GeneralType type, out NumericType numericType)
    {
        numericType = default;
        return type.FinalValue switch
        {
            BuiltinType v => v.TryGetNumericType(out numericType),
            PointerType v => v.TryGetNumericType(out numericType),
            _ => false,
        };
    }

    public static bool TryGetNumericType(this BuiltinType type, out NumericType numericType)
    {
        numericType = default;
        switch (type.Type)
        {
            case BasicType.U8:
            case BasicType.U16:
            case BasicType.U32:
            case BasicType.U64:
                numericType = NumericType.UnsignedInteger;
                return true;
            case BasicType.I8:
            case BasicType.I16:
            case BasicType.I32:
            case BasicType.I64:
                numericType = NumericType.SignedInteger;
                return true;
            case BasicType.F32:
                numericType = NumericType.Float;
                return true;
            default:
                return false;
        }
    }

    public static bool TryGetNumericType(this PointerType type, out NumericType numericType)
    {
        numericType = NumericType.SignedInteger;
        return true;
    }

    public static bool TryGetBitWidth(this GeneralType type, out BitWidth bitWidth)
    {
        bitWidth = default;
        return type.FinalValue switch
        {
            BuiltinType v => v.TryGetBitWidth(out bitWidth),
            _ => false,
        };
    }

    public static bool TryGetBitWidth(this BuiltinType type, out BitWidth bitWidth)
    {
        bitWidth = default;
        switch (type.Type)
        {
            case BasicType.U8:
            case BasicType.I8:
                bitWidth = BitWidth._8;
                return true;
            case BasicType.U16:
            case BasicType.I16:
                bitWidth = BitWidth._16;
                return true;
            case BasicType.U32:
            case BasicType.I32:
            case BasicType.F32:
                bitWidth = BitWidth._32;
                return true;
            case BasicType.U64:
            case BasicType.I64:
                bitWidth = BitWidth._64;
                return true;
            default:
                return false;
        }
    }
}
