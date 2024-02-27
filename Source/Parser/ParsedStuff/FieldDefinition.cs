namespace LanguageCore.Parser;

using Tokenizing;

public class FieldDefinition : IPositioned
{
    public readonly Token Identifier;
    public readonly TypeInstance Type;
    public readonly Token? ProtectionToken;
    public Token? Semicolon;

    public Position Position => new(Identifier, Type, ProtectionToken);

    public FieldDefinition(FieldDefinition other)
    {
        Identifier = other.Identifier;
        Type = other.Type;
        ProtectionToken = other.ProtectionToken;
        Semicolon = other.Semicolon;
    }

    public FieldDefinition(Token identifier, TypeInstance type, Token? protectionToken)
    {
        Identifier = identifier;
        Type = type;
        ProtectionToken = protectionToken;
    }

    public override string ToString() => $"{(ProtectionToken is not null ? ProtectionToken.Content + " " : string.Empty)}{Type} {Identifier}";
}
