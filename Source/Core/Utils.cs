namespace LanguageCore;

using Compiler;
using Parser;
using Parser.Statement;

public interface IReadable
{
    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch);
}

public interface ISimpleReadable : IReadable
{
    public string ToReadable();
    string IReadable.ToReadable(Func<StatementWithValue, GeneralType> typeSearch) => ToReadable();
}

[Flags]
public enum ToReadableFlags
{
    None = 0b_0000,
    ParameterIdentifiers = 0b_0001,
    Modifiers = 0b_0010,
}

public static partial class Utils
{
    public static bool TryConvertType(Type type, out LiteralType result)
    {
        if (type == typeof(int))
        {
            result = LiteralType.Integer;
            return true;
        }

        if (type == typeof(float))
        {
            result = LiteralType.Float;
            return true;
        }

        if (type == typeof(string))
        {
            result = LiteralType.String;
            return true;
        }

        result = default;
        return false;
    }
}
