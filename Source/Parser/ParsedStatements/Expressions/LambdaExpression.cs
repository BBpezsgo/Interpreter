using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class LambdaExpression : Expression
{
    public ICompiledFunctionDefinition? AllocatorReference { get; internal set; }

    public ParameterDefinitionCollection Parameters { get; }
    public Token Arrow { get; }
    public Statement Body { get; }

    public override Position Position => new(
        Parameters,
        Arrow,
        Body
    );

    public LambdaExpression(
        ParameterDefinitionCollection parameters,
        Token arrow,
        Statement body,
        Uri file) : base(file)
    {
        Parameters = parameters;
        Arrow = arrow;
        Body = body;
    }

    public override string ToString()
        => $"{Parameters} {Arrow} {Body}";

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;
        if (!flags.HasFlag(StatementWalkFlags.FrameOnly))
        {
            foreach (Statement v in Body.GetStatementsRecursively(flags | StatementWalkFlags.IncludeThis)) yield return v;
        }
    }
}
