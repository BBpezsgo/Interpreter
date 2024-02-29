namespace LanguageCore.Parser;

using Statement;
using Tokenizing;

public class AttributeUsage : IPositioned
{
    public Token Identifier { get; }
    public ImmutableArray<Literal> Parameters { get; }
    public Position Position =>
        new Position(Parameters)
        .Union(Identifier);

    public AttributeUsage(Token identifier, IEnumerable<Literal> parameters)
    {
        Identifier = identifier;
        Parameters = parameters.ToImmutableArray();
    }
}
