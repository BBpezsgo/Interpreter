using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class VariableDefinition : Statement,
    IHaveType,
    IExportable,
    IIdentifiable<IdentifierExpression>,
    IHaveAttributes
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public GeneralType? CompiledType { get; set; }
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledCleanup? CleanupReference { get; set; }

    public ImmutableArray<AttributeUsage> Attributes { get; }
    public TypeInstance Type { get; }
    public IdentifierExpression Identifier { get; }
    public Expression? InitialValue { get; }
    public ImmutableArray<Token> Modifiers { get; }

    public string? ExternalConstantName => Attributes.TryGetAttribute(AttributeConstants.ExternalIdentifier, out AttributeUsage? attribute) && attribute.TryGetValue(out string? name) ? name : null;

    public override Position Position =>
        new Position(Type, Identifier, InitialValue)
        .Union(Modifiers);
    public bool IsExported => Modifiers.Contains(ProtectionKeywords.Export);

    public VariableDefinition(VariableDefinition other) : base(other)
    {
        Attributes = other.Attributes;
        Type = other.Type;
        Identifier = other.Identifier;
        InitialValue = other.InitialValue;
        Modifiers = other.Modifiers;
        CompiledType = other.CompiledType;
    }

    public VariableDefinition(
        ImmutableArray<AttributeUsage> attributes,
        ImmutableArray<Token> modifiers,
        TypeInstance type,
        IdentifierExpression variableName,
        Expression? initialValue,
        Uri file) : base(file)
    {
        Attributes = attributes;
        Type = type;
        Identifier = variableName;
        InitialValue = initialValue;
        Modifiers = modifiers;
    }

    public override string ToString()
        => $"{string.Join(' ', Modifiers)} {Type} {Identifier}{((InitialValue != null) ? " = ..." : string.Empty)}{Semicolon}".TrimStart();
}
