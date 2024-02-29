namespace LanguageCore.Parser;

using Tokenizing;

public class StructDefinition : IExportable, IInFile, IPositioned
{
    public ImmutableArray<AttributeUsage> Attributes { get; }
    public Token Identifier { get; }
    public Token BracketStart { get; }
    public Token BracketEnd { get; }
    public Uri? FilePath { get; set; }
    public ImmutableArray<FieldDefinition> Fields { get; }
    public ImmutableArray<Token> Modifiers { get; }
    public TemplateInfo? TemplateInfo { get; init; }
    public bool IsExport => Modifiers.Contains("export");
    public virtual Position Position => new(Identifier, BracketStart, BracketEnd);
    public ImmutableArray<FunctionDefinition> Methods { get; }
    public ImmutableArray<GeneralFunctionDefinition> GeneralMethods { get; }
    public ImmutableArray<FunctionDefinition> Operators { get; }
    public ImmutableArray<ConstructorDefinition> Constructors { get; }

    public StructDefinition(StructDefinition other)
    {
        Attributes = other.Attributes;
        Identifier = other.Identifier;
        BracketStart = other.BracketStart;
        BracketEnd = other.BracketEnd;
        FilePath = other.FilePath;
        Fields = other.Fields;
        Modifiers = other.Modifiers;
        TemplateInfo = other.TemplateInfo;
        Methods = other.Methods;
        GeneralMethods = other.GeneralMethods;
        Operators = other.Operators;
        Constructors = other.Constructors;
    }

    public StructDefinition(
        Token name,
        Token bracketStart,
        Token bracketEnd,
        IEnumerable<AttributeUsage> attributes,
        IEnumerable<Token> modifiers,
        IEnumerable<FieldDefinition> fields,
        IEnumerable<FunctionDefinition> methods,
        IEnumerable<GeneralFunctionDefinition> generalMethods,
        IEnumerable<FunctionDefinition> operators,
        IEnumerable<ConstructorDefinition> constructors)
    {
        foreach (FunctionDefinition method in methods) method.Context = this;
        foreach (GeneralFunctionDefinition generalMethod in generalMethods) generalMethod.Context = this;
        foreach (FunctionDefinition @operator in operators) @operator.Context = this;
        foreach (ConstructorDefinition constructor in constructors) constructor.Context = this;

        Identifier = name;
        BracketStart = bracketStart;
        BracketEnd = bracketEnd;
        Fields = fields.ToImmutableArray();
        Methods = methods.ToImmutableArray();
        GeneralMethods = generalMethods.ToImmutableArray();
        Attributes = attributes.ToImmutableArray();
        Operators = operators.ToImmutableArray();
        Constructors = constructors.ToImmutableArray();
        Modifiers = modifiers.ToImmutableArray();
    }

    public override string ToString() => $"struct {Identifier.Content}";
}
