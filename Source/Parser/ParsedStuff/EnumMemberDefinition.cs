namespace LanguageCore.Parser;

using Statement;
using Tokenizing;

public class EnumMemberDefinition : IPositioned
{
    public readonly Token Identifier;
    public readonly StatementWithValue? Value;

    public Position Position => new(Identifier, Value);

    public EnumMemberDefinition(EnumMemberDefinition other)
    {
        Identifier = other.Identifier;
        Value = other.Value;
    }

    public EnumMemberDefinition(Token identifier, StatementWithValue? value)
    {
        Identifier = identifier;
        Value = value;
    }
}
