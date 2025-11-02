using LanguageCore.Compiler;
using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class ParameterDefinition :
    IPositioned,
    IHaveType,
    IInContext<FunctionThingDefinition>,
    IIdentifiable<Token>,
    IInFile,
    ILocated
{
    /// <summary>
    /// Set by the <see cref="FunctionThingDefinition"/>
    /// </summary>
    [NotNull] public FunctionThingDefinition? Context { get; set; }

    public Token Identifier { get; }
    public TypeInstance Type { get; }
    public ImmutableArray<Token> Modifiers { get; }
    public StatementWithValue? DefaultValue { get; }

    public bool IsRef => Modifiers.Contains(ModifierKeywords.Ref);
    public Position Position =>
        new Position(Identifier, Type)
        .Union(Modifiers);
    public Uri File => Context?.File ?? throw new NullReferenceException($"{nameof(Context.File)} is null");

    public Location Location => new(Position, File);

    public ParameterDefinition(ParameterDefinition other)
    {
        Modifiers = other.Modifiers;
        Type = other.Type;
        Identifier = other.Identifier;
        Context = other.Context;
        DefaultValue = other.DefaultValue;
    }

    public ParameterDefinition(ImmutableArray<Token> modifiers, TypeInstance type, Token identifier, StatementWithValue? defaultValue, FunctionThingDefinition? context = null)
    {
        Modifiers = modifiers;
        Type = type;
        Identifier = identifier;
        Context = context;
        DefaultValue = defaultValue;
    }

    public override string ToString() => $"{string.Join(' ', Modifiers)} {Type} {Identifier}".TrimStart();
}
