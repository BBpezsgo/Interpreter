using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class ConstructorCallExpression : Expression, IReadable, IReferenceableTo<CompiledConstructorDefinition>, IHaveType
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledConstructorDefinition? Reference { get; set; }

    public Token Keyword { get; }
    public TypeInstance Type { get; }
    public ImmutableArray<ArgumentExpression> Arguments { get; }
    public TokenPair Brackets { get; }

    public override Position Position =>
        new Position(Keyword, Type, Brackets)
        .Union(Arguments);

    public ConstructorCallExpression(
        Token keyword,
        TypeInstance typeName,
        ImmutableArray<ArgumentExpression> arguments,
        TokenPair brackets,
        Uri file) : base(file)
    {
        Keyword = keyword;
        Type = typeName;
        Arguments = arguments;
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBrackets?.Start);

        result.Append(Keyword);
        result.Append(' ');
        result.Append(Type);
        result.Append(Brackets.Start);

        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");

            if (result.Length >= Stringify.CozyLength)
            { result.Append("..."); break; }

            result.Append(Arguments[i]);
        }

        result.Append(Brackets.End);

        result.Append(SurroundingBrackets?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(FindStatementType typeSearch)
    {
        StringBuilder result = new();
        result.Append(Type.ToString());
        result.Append('.');
        result.Append(Keyword.Content);
        result.Append(Brackets.Start);
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(typeSearch.Invoke(Arguments[i], out GeneralType? type, new()) ? type.ToString() : '?');
        }
        result.Append(Brackets.End);

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
