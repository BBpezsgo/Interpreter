namespace LanguageCore.Parser;

using System;
using Compiler;
using Tokenizing;

public class AliasDefinition :
    IPositioned,
    IIdentifiable<Token>,
    IDefinition<CompiledAlias>,
    IExportable
{
    public ImmutableArray<Token> Modifiers { get; }
    public Token Keyword { get; }
    public Token Identifier { get; }
    public TypeInstance Value { get; }
    public Uri File { get; }

    public Position Position => new(Keyword, Identifier, Value);
    public bool IsExported => Modifiers.Contains(ProtectionKeywords.Export);

    public AliasDefinition(IEnumerable<Token> modifiers, Token keyword, Token identifier, TypeInstance value, Uri file)
    {
        Modifiers = modifiers.ToImmutableArray();
        Keyword = keyword;
        Identifier = identifier;
        Value = value;
        File = file;
    }
    public AliasDefinition(AliasDefinition other)
    {
        Modifiers = other.Modifiers;
        Keyword = other.Keyword;
        Identifier = other.Identifier;
        Value = other.Value;
        File = other.File;
    }

    public bool DefinitionEquals(CompiledAlias other) => Identifier.Content.Equals(other.Identifier.Content);
}
