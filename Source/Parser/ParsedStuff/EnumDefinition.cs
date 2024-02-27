using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LanguageCore.Parser;

using Tokenizing;

public class EnumDefinition : IInFile, IPositioned
{
    public readonly Token Identifier;
    public readonly ImmutableArray<EnumMemberDefinition> Members;
    public readonly ImmutableArray<AttributeUsage> Attributes;

    public Uri? FilePath { get; set; }

    public Position Position =>
        new Position(Identifier)
        .Union(Members);

    public EnumDefinition(EnumDefinition other)
    {
        Identifier = other.Identifier;
        Attributes = other.Attributes;
        Members = other.Members;
        FilePath = other.FilePath;
    }

    public EnumDefinition(Token identifier, IEnumerable<AttributeUsage> attributes, IEnumerable<EnumMemberDefinition> members)
    {
        Identifier = identifier;
        Attributes = attributes.ToImmutableArray();
        Members = members.ToImmutableArray();
    }

    public override string ToString() => $"enum {Identifier}";
}
