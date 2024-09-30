using LanguageCore.Parser.Statement;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public enum BasicType
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
