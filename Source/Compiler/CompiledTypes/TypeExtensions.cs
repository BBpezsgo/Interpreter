using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

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
}
