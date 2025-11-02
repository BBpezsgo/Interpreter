using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class StructDefinition :
    IExportable,
    IPositioned,
    IIdentifiable<Token>,
    IHaveAttributes
{
    public ImmutableArray<AttributeUsage> Attributes { get; }
    public Token Identifier { get; }
    public Token BracketStart { get; }
    public Token BracketEnd { get; }
    public Uri File { get; }
    public ImmutableArray<FieldDefinition> Fields { get; }
    public ImmutableArray<Token> Modifiers { get; }
    public TemplateInfo? Template { get; init; }
    public ImmutableArray<FunctionDefinition> Functions { get; }
    public ImmutableArray<GeneralFunctionDefinition> GeneralFunctions { get; }
    public ImmutableArray<FunctionDefinition> Operators { get; }
    public ImmutableArray<ConstructorDefinition> Constructors { get; }

    public bool IsExported => Modifiers.Contains(ProtectionKeywords.Export);
    public virtual Position Position => new(Identifier, BracketStart, BracketEnd);

    public StructDefinition(StructDefinition other)
    {
        Attributes = other.Attributes;
        Identifier = other.Identifier;
        BracketStart = other.BracketStart;
        BracketEnd = other.BracketEnd;
        File = other.File;
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
        ImmutableArray<AttributeUsage> attributes,
        ImmutableArray<Token> modifiers,
        ImmutableArray<FieldDefinition> fields,
        ImmutableArray<FunctionDefinition> methods,
        ImmutableArray<GeneralFunctionDefinition> generalMethods,
        ImmutableArray<FunctionDefinition> operators,
        ImmutableArray<ConstructorDefinition> constructors,
        Uri file)
    {
        foreach (FunctionDefinition method in methods) method.Context = this;
        foreach (GeneralFunctionDefinition generalMethod in generalMethods) generalMethod.Context = this;
        foreach (FunctionDefinition @operator in operators) @operator.Context = this;
        foreach (ConstructorDefinition constructor in constructors) constructor.Context = this;
        foreach (FieldDefinition field in fields) field.Context = this;

        Identifier = name;
        BracketStart = bracketStart;
        BracketEnd = bracketEnd;
        Fields = fields;
        Functions = methods;
        GeneralFunctions = generalMethods;
        Attributes = attributes;
        Operators = operators;
        Constructors = constructors;
        Modifiers = modifiers;

        File = file;
    }

    public override string ToString() => $"struct {Identifier.Content}";
}
