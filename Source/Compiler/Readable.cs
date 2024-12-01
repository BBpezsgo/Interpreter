using LanguageCore.Compiler;
using LanguageCore.Parser.Statement;

namespace LanguageCore;

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
