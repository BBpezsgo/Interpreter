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
}
