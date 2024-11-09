using LanguageCore.Parser.Statement;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public enum BasicType : byte
{
    Void,
    Any,
    U8,
    I8,
    Char,
    I16,
    U32,
    I32,
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
                numericType = NumericType.UnsignedInteger;
                return true;
            case BasicType.I8:
                numericType = NumericType.SignedInteger;
                return true;
            case BasicType.Char:
                numericType = NumericType.UnsignedInteger;
                return true;
            case BasicType.I16:
                numericType = NumericType.SignedInteger;
                return true;
            case BasicType.U32:
                numericType = NumericType.UnsignedInteger;
                return true;
            case BasicType.I32:
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
}
