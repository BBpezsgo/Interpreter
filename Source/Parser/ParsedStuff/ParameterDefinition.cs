namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class ParameterDefinition :
    IPositioned,
    IHaveType,
    IInContext<FunctionThingDefinition>,
    IIdentifiable<Token>
{
    /// <summary>
    /// Set by the <see cref="FunctionThingDefinition"/>
    /// </summary>
    [NotNull] public FunctionThingDefinition? Context { get; set; }

    public Token Identifier { get; }
    public TypeInstance Type { get; }
    public ImmutableArray<Token> Modifiers { get; }

    public bool IsRef => Modifiers.Contains(ModifierKeywords.Ref);
    public Position Position =>
        new Position(Identifier, Type)
        .Union(Modifiers);

    public ParameterDefinition(ParameterDefinition other)
    {
        Modifiers = other.Modifiers;
        Type = other.Type;
        Identifier = other.Identifier;
        Context = other.Context;
    }

    public ParameterDefinition(IEnumerable<Token> modifiers, TypeInstance type, Token identifier, FunctionThingDefinition? context = null)
    {
        Modifiers = modifiers.ToImmutableArray();
        Type = type;
        Identifier = identifier;
        Context = context;
    }

    public override string ToString() => $"{string.Join(", ", Modifiers)} {Type} {Identifier}".TrimStart();
}
