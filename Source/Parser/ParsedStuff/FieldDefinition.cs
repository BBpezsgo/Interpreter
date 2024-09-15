using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class FieldDefinition :
    IPositioned,
    IInContext<StructDefinition>,
    IIdentifiable<Token>
{
    /// <summary>
    /// Set by the <see cref="StructDefinition"/>
    /// </summary>
    [NotNull] public StructDefinition? Context { get; set; }

    public ImmutableArray<Token> Modifiers { get; }
    public TypeInstance Type { get; }
    public Token Identifier { get; }
    public Token? Semicolon { get; set; }

    public Position Position => new Position(Identifier, Type).Union(Modifiers);
    public Protection Protection
    {
        get
        {
            foreach (Token modifier in Modifiers)
            {
                switch (modifier.Content)
                {
                    case ProtectionKeywords.Private: return Protection.Private;
                    default: break;
                }
            }
            return Protection.Public;
        }
    }

    public FieldDefinition(FieldDefinition other)
    {
        Identifier = other.Identifier;
        Type = other.Type;
        Modifiers = other.Modifiers;
        Semicolon = other.Semicolon;
        Context = other.Context;
    }

    public FieldDefinition(Token identifier, TypeInstance type, IEnumerable<Token> modifiers)
    {
        Identifier = identifier;
        Type = type;
        Modifiers = modifiers.ToImmutableArray();
    }

    public override string ToString() => $"{(Modifiers.IsEmpty ? string.Empty : $"{string.Join(' ', Modifiers)} ")}{Type} {Identifier}";
}
