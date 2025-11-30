using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class KeywordCallStatement : Statement, IReadable, IReferenceableTo<CompiledCleanup>
{
    public CompiledCleanup? Reference { get; set; }

    public Token Keyword { get; }
    public ImmutableArray<Expression> Arguments { get; }

    public IdentifierExpression Identifier => new(Keyword, File);

    public override Position Position =>
        new Position(Keyword)
        .Union(Arguments);

    public KeywordCallStatement(
        Token keyword,
        ImmutableArray<Expression> arguments,
        Uri file) : base(file)
    {
        Keyword = keyword;
        Arguments = arguments;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Keyword);

        if (Arguments.Length > 0)
        {
            result.Append(' ');
            for (int i = 0; i < Arguments.Length; i++)
            {
                if (i > 0)
                { result.Append(", "); }
                if (result.Length >= Stringify.CozyLength)
                { result.Append("..."); break; }

                result.Append(Arguments[i]);
            }
        }

        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(FindStatementType typeSearch)
    {
        StringBuilder result = new();
        result.Append(Keyword.Content);
        result.Append('(');
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(typeSearch.Invoke(Arguments[i], out GeneralType? type, new()) ? type.ToString() : '?');
        }
        result.Append(')');

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Expression argument in Arguments)
        {
            foreach (Statement statement in argument.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }
    }
}
