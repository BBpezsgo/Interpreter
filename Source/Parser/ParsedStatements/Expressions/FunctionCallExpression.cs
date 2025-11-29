using LanguageCore.Compiler;

namespace LanguageCore.Parser.Statements;

public class FunctionCallExpression : Expression, IReadable, IReferenceableTo<CompiledFunctionDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledFunctionDefinition? Reference { get; set; }

    public IdentifierExpression Identifier { get; }
    public ImmutableArray<ArgumentExpression> Arguments { get; }
    public ArgumentExpression? Object { get; }
    public TokenPair Brackets { get; }

    public bool IsMethodCall => Object != null;
    public ImmutableArray<ArgumentExpression> MethodArguments
    {
        get
        {
            if (Object == null) return Arguments;
            return Arguments.Insert(0, Object);
        }
    }
    public override Position Position =>
        new Position(Brackets, Identifier)
        .Union(MethodArguments);

    public FunctionCallExpression(
        ArgumentExpression? @object,
        IdentifierExpression identifier,
        ImmutableArray<ArgumentExpression> arguments,
        TokenPair brackets,
        Uri file) : base(file)
    {
        Object = @object;
        Identifier = identifier;
        Arguments = arguments;
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBrackets?.Start);

        if (Object != null)
        {
            result.Append(Object);
            result.Append('.');
        }
        result.Append(Identifier);
        result.Append(Brackets.Start);
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(Arguments[i]);

            if (result.Length >= 10 && i + 1 != Arguments.Length)
            {
                result.Append(", ...");
                break;
            }
        }
        result.Append(Brackets.End);

        result.Append(SurroundingBrackets?.End);
        result.Append(Semicolon);

        return result.ToString();
    }

    public string ToReadable(FindStatementType typeSearch)
    {
        StringBuilder result = new();
        if (Object != null)
        {
            result.Append(typeSearch.Invoke(Object, out GeneralType? type, new()) ? type.ToString() : '?');
            result.Append('.');
        }
        result.Append(Identifier.ToString());
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

        if (Object != null)
        {
            foreach (Statement statement in Object.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }

        foreach (Expression argument in Arguments)
        {
            foreach (Statement statement in argument.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }
    }
}
