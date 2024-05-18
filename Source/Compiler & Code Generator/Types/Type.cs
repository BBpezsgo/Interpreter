namespace LanguageCore.Compiler;

using Parser.Statement;
using Runtime;
using Tokenizing;

public enum BasicType
{
    Void,
    Byte,
    Integer,
    Float,
    Char,
}

public delegate bool ComputeValue(StatementWithValue value, out DataItem computedValue);
public delegate bool FindType(Token token, Uri relevantFile, [NotNullWhen(true)] out GeneralType? computedValue);
