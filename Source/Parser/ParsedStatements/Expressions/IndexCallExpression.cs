using LanguageCore.Compiler;

namespace LanguageCore.Parser.Statements;

public class IndexCallExpression : Expression, IReadable, IReferenceableTo<CompiledFunctionDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledFunctionDefinition? Reference { get; set; }

    public Expression Object { get; }
    public ArgumentExpression Index { get; }
    public TokenPair Brackets { get; }

    public override Position Position => new(Object, Index);

    public IndexCallExpression(
        Expression @object,
        ArgumentExpression indexStatement,
        TokenPair brackets,
        Uri file) : base(file)
    {
        Object = @object;
        Index = indexStatement;
        Brackets = brackets;
    }

    public override string ToString()
        => $"{SurroundingBrackets?.Start}{Object}{Brackets.Start}{Index}{Brackets.End}{SurroundingBrackets?.End}{Semicolon}";

    public string ToReadable(FindStatementType typeSearch)
    {
        StringBuilder result = new();

        if (Object != null)
        { result.Append(typeSearch.Invoke(Object, out GeneralType? type1, new()) ? type1.ToString() : '?'); }
        else
        { result.Append('?'); }

        result.Append(Brackets.Start);
        result.Append(typeSearch.Invoke(Index, out GeneralType? type2, new()) ? type2.ToString() : '?');
        result.Append(Brackets.End);

        return result.ToString();
    }
}
