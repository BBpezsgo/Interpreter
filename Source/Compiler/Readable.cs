using LanguageCore.Compiler;
using LanguageCore.Parser.Statements;

namespace LanguageCore;

public delegate bool FindStatementType(StatementWithValue statement, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics);

public interface IReadable
{
    string ToReadable(FindStatementType typeSearch);
}

public interface ISimpleReadable : IReadable
{
    string ToReadable();

    string IReadable.ToReadable(FindStatementType typeSearch) => ToReadable();
}

[Flags]
public enum ToReadableFlags
{
    None = 0b_0000,
    ParameterIdentifiers = 0b_0001,
    Modifiers = 0b_0010,
}
