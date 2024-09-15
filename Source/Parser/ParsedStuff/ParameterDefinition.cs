using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class ParameterDefinition :
    IPositioned,
    IHaveType,
    IInContext<FunctionThingDefinition>,
    IIdentifiable<Token>,
    IInFile
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
    public Uri File => Context?.File ?? throw new NullReferenceException($"{nameof(Context.File)} is null");

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

    public override string ToString() => $"{string.Join(' ', Modifiers)} {Type} {Identifier}".TrimStart();
}
