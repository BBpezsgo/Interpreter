namespace LanguageCore.Parser.Statements;

public class FieldExpression : Expression, IReferenceableTo
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public object? Reference { get; set; }

    public Expression Object { get; }
    public IdentifierExpression Identifier { get; }

    public override Position Position => new(Object, Identifier);

    public FieldExpression(
        Expression @object,
        IdentifierExpression identifier,
        Uri file) : base(file)
    {
        Object = @object;
        Identifier = identifier;
    }

    public override string ToString()
        => $"{SurroundingBrackets?.Start}{Object}.{Identifier}{SurroundingBrackets?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;

        foreach (Statement statement in Object.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis))
        { yield return statement; }
    }
}
