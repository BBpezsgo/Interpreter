namespace LanguageCore.Parser.Statements;

public static class StatementExtensions
{
    public static Statement? GetStatementAt(this ParserResult parserResult, SinglePosition position)
        => parserResult.GetStatementsRecursively()
        .LastOrDefault(statement => statement.Position.Range.Contains(position));

    public static Statement? GetStatement(this ParserResult parserResult, Func<Statement, bool> condition)
        => parserResult.GetStatementsRecursively()
        .LastOrDefault(condition);

    public static T? GetStatement<T>(this Statement statement, Func<T, bool> condition)
        => statement.GetStatementsRecursively(StatementWalkFlags.IncludeThis)
        .OfType<T>()
        .LastOrDefault(condition);

    public static bool GetStatementAt(this ParserResult parserResult, SinglePosition position, [NotNullWhen(true)] out Statement? statement)
        => (statement = GetStatement(parserResult, statement => statement.Position.Range.Contains(position))) is not null;

    public static bool GetStatement<T>(this Statement statement, [NotNullWhen(true)] out T? result, Func<T, bool> condition)
        => (result = GetStatement(statement, condition)) is not null;
}
