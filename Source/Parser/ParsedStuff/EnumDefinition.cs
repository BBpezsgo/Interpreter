namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class EnumDefinition :
    IInFile,
    IPositioned,
    IIdentifiable<Token>
{
    public Token Identifier { get; }
    public ImmutableArray<EnumMemberDefinition> Members { get; }
    public ImmutableArray<AttributeUsage> Attributes { get; }
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
        foreach (EnumMemberDefinition member in members) member.Context = this;
        Members = members.ToImmutableArray();
    }

    public override string ToString() => $"enum {Identifier}";
}
