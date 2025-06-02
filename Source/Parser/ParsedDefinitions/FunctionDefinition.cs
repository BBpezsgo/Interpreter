using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class FunctionDefinition : FunctionThingDefinition,
    IHaveType,
    IInContext<StructDefinition?>,
    IHaveAttributes
{
    /// <summary>
    /// Set by the <see cref="StructDefinition"/>
    /// </summary>
    public StructDefinition? Context { get; set; }

    public override ImmutableArray<AttributeUsage> Attributes { get; }
    public TypeInstance Type { get; }

    public override Position Position => base.Position.Union(Type);

    public string? ExternalFunctionName => Attributes.TryGetAttribute(AttributeConstants.ExternalIdentifier, out AttributeUsage? attribute) && attribute.TryGetValue(out string? name) ? name : null;

    [MemberNotNullWhen(true, nameof(BuiltinFunctionName))]
    public bool IsBuiltin => Attributes.TryGetAttribute(AttributeConstants.BuiltinIdentifier, out _);
    public string? BuiltinFunctionName => Attributes.TryGetAttribute(AttributeConstants.BuiltinIdentifier, out AttributeUsage? attribute) && attribute.TryGetValue(out string? name) ? name : null;

    public string? ExposedFunctionName =>
        Attributes.TryGetAttribute(AttributeConstants.ExposeIdentifier, out AttributeUsage? attribute)
            ? attribute.TryGetValue(out string? name) ? name : Identifier.Content
            : null;

    public override bool IsTemplate
    {
        get
        {
            if (Template is not null) return true;
            if (Context?.Template is not null) return true;
            return false;
        }
    }

    public FunctionDefinition(FunctionDefinition other) : base(other)
    {
        Attributes = other.Attributes;
        Type = other.Type;
        Context = other.Context;
    }

    public FunctionDefinition(
        IEnumerable<AttributeUsage> attributes,
        IEnumerable<Token> modifiers,
        TypeInstance type,
        Token identifier,
        ParameterDefinitionCollection parameters,
        TemplateInfo? templateInfo,
        Uri file)
        : base(modifiers, identifier, parameters, templateInfo, file)
    {
        Attributes = attributes.ToImmutableArray();
        Type = type;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExported)
        { result.Append("export "); }

        result.Append(Type.ToString());
        result.Append(' ');

        result.Append(Identifier.Content);

        result.Append('(');
        if (Parameters.Count > 0)
        {
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(Parameters[i].Type.ToString());
            }
        }
        result.Append(')');

        result.Append(Block?.ToString() ?? ";");

        return result.ToString();
    }
}
