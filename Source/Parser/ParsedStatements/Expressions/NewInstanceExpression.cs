using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class NewInstanceExpression : Expression, IHaveType, IInFile, IReferenceableTo<ICompiledFunctionDefinition>
{
    public Token Keyword { get; }
    public TypeInstance Type { get; }
    public ICompiledFunctionDefinition? Reference { get; set; }

    public override Position Position => new(Keyword, Type);

    public NewInstanceExpression(
        Token keyword,
        TypeInstance type,
        Uri file) : base(file)
    {
        Keyword = keyword;
        Type = type;
    }

    public override string ToString()
        => $"{SurroundingBrackets?.Start}{Keyword} {Type}{SurroundingBrackets?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;
    }
}
