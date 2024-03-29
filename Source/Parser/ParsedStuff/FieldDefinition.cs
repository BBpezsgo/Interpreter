namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class FieldDefinition :
    IPositioned,
    IInContext<StructDefinition>,
    IIdentifiable<Token>
{
    public Token Identifier { get; }
    public TypeInstance Type { get; }
    public Token? ProtectionToken { get; }
    public Token? Semicolon { get; set; }
    [NotNull] public StructDefinition? Context { get; set; }

    public Position Position => new(Identifier, Type, ProtectionToken);

    public FieldDefinition(FieldDefinition other)
    {
        Identifier = other.Identifier;
        Type = other.Type;
        ProtectionToken = other.ProtectionToken;
        Semicolon = other.Semicolon;
        Context = other.Context;
    }

    public FieldDefinition(Token identifier, TypeInstance type, Token? protectionToken)
    {
        Identifier = identifier;
        Type = type;
        ProtectionToken = protectionToken;
    }

    public override string ToString() => $"{(ProtectionToken is not null ? ProtectionToken.Content + " " : string.Empty)}{Type} {Identifier}";
}
