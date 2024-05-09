namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class StructDefinition :
    IExportable,
    IPositioned,
    IIdentifiable<Token>
{
    public ImmutableArray<AttributeUsage> Attributes { get; }
    public Token Identifier { get; }
    public Token BracketStart { get; }
    public Token BracketEnd { get; }
    public Uri? FilePath { get; }
    public ImmutableArray<FieldDefinition> Fields { get; }
    public ImmutableArray<Token> Modifiers { get; }
    public TemplateInfo? Template { get; init; }
    public ImmutableArray<FunctionDefinition> Functions { get; }
    public ImmutableArray<GeneralFunctionDefinition> GeneralFunctions { get; }
    public ImmutableArray<FunctionDefinition> Operators { get; }
    public ImmutableArray<ConstructorDefinition> Constructors { get; }

    public bool IsExport => Modifiers.Contains(ProtectionKeywords.Export);
    public virtual Position Position => new(Identifier, BracketStart, BracketEnd);

    public StructDefinition(StructDefinition other)
    {
        Attributes = other.Attributes;
        Identifier = other.Identifier;
        BracketStart = other.BracketStart;
        BracketEnd = other.BracketEnd;
        FilePath = other.FilePath;
        Fields = other.Fields;
        Modifiers = other.Modifiers;
        Template = other.Template;
        Functions = other.Functions;
        GeneralFunctions = other.GeneralFunctions;
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
        IEnumerable<ConstructorDefinition> constructors,
        Uri? file)
    {
        foreach (FunctionDefinition method in methods) method.Context = this;
        foreach (GeneralFunctionDefinition generalMethod in generalMethods) generalMethod.Context = this;
        foreach (FunctionDefinition @operator in operators) @operator.Context = this;
        foreach (ConstructorDefinition constructor in constructors) constructor.Context = this;
        foreach (FieldDefinition field in fields) field.Context = this;

        Identifier = name;
        BracketStart = bracketStart;
        BracketEnd = bracketEnd;
        Fields = fields.ToImmutableArray();
        Functions = methods.ToImmutableArray();
        GeneralFunctions = generalMethods.ToImmutableArray();
        Attributes = attributes.ToImmutableArray();
        Operators = operators.ToImmutableArray();
        Constructors = constructors.ToImmutableArray();
        Modifiers = modifiers.ToImmutableArray();

        FilePath = file;
    }

    public override string ToString() => $"struct {Identifier.Content}";
}
