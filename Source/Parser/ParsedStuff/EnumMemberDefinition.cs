namespace LanguageCore.Parser;

using Compiler;
using Statement;
using Tokenizing;

public class EnumMemberDefinition :
    IPositioned,
    IInContext<EnumDefinition>,
    IIdentifiable<Token>
{
    /// <summary>
    /// Set by the <see cref="EnumDefinition"/>
    /// </summary>
    [NotNull] public EnumDefinition? Context { get; set; }

    public Token Identifier { get; }
    public StatementWithValue? Value { get; }

    public Position Position => new(Identifier, Value);

    public EnumMemberDefinition(EnumMemberDefinition other)
    {
        Identifier = other.Identifier;
        Value = other.Value;
        Context = other.Context;
    }

    public EnumMemberDefinition(Token identifier, StatementWithValue? value)
    {
        Identifier = identifier;
        Value = value;
    }
}
