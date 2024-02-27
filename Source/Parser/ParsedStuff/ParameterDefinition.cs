using System.Collections.Generic;
using System.Collections.Immutable;

namespace LanguageCore.Parser;

using Tokenizing;

public class ParameterDefinition : IPositioned
{
    public readonly Token Identifier;
    public readonly TypeInstance Type;
    public readonly ImmutableArray<Token> Modifiers;

    public Position Position =>
        new Position(Identifier, Type)
        .Union(Modifiers);

    public ParameterDefinition(ParameterDefinition other)
    {
        Modifiers = other.Modifiers;
        Type = other.Type;
        Identifier = other.Identifier;
    }

    public ParameterDefinition(IEnumerable<Token> modifiers, TypeInstance type, Token identifier)
    {
        Modifiers = modifiers.ToImmutableArray();
        Type = type;
        Identifier = identifier;
    }

    public override string ToString() => $"{string.Join(", ", Modifiers)} {Type} {Identifier}".TrimStart();
}
