using System.Collections.Generic;
using System.Collections.Immutable;

namespace LanguageCore.Parser;

using Statement;
using Tokenizing;

public class AttributeUsage : IPositioned
{
    public readonly Token Identifier;
    public readonly ImmutableArray<Literal> Parameters;

    public Position Position =>
        new Position(Parameters)
        .Union(Identifier);

    public AttributeUsage(Token identifier, IEnumerable<Literal> parameters)
    {
        Identifier = identifier;
        Parameters = parameters.ToImmutableArray();
    }
}
