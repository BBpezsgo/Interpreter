using LanguageCore.Compiler;
using LanguageCore.Parser.Statements;

namespace LanguageCore;

public interface IReadable
{
    string ToReadable(Func<StatementWithValue, GeneralType> typeSearch);
}

public interface ISimpleReadable : IReadable
{
    string ToReadable();

    string IReadable.ToReadable(Func<StatementWithValue, GeneralType> typeSearch) => ToReadable();
}

[Flags]
public enum ToReadableFlags
{
    None = 0b_0000,
    ParameterIdentifiers = 0b_0001,
    Modifiers = 0b_0010,
}
