namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class FunctionDefinition : FunctionThingDefinition,
    IHaveType,
    IInContext<StructDefinition?>
{
    public ImmutableArray<AttributeUsage> Attributes { get; }
    public TypeInstance Type { get; }
    public StructDefinition? Context { get; set; }

    public override Position Position => base.Position.Union(Type);

    [MemberNotNullWhen(true, nameof(ExternalFunctionName))]
    public bool IsExternal => Attributes.TryGetAttribute<string>(AttributeConstants.ExternalIdentifier, out _);
    public string? ExternalFunctionName => Attributes.TryGetAttribute<string>(AttributeConstants.ExternalIdentifier, out string? name) ? name : null;

    [MemberNotNullWhen(true, nameof(BuiltinFunctionName))]
    public bool IsBuiltin => Attributes.TryGetAttribute<string>(AttributeConstants.BuiltinIdentifier, out _);
    public string? BuiltinFunctionName => Attributes.TryGetAttribute<string>(AttributeConstants.BuiltinIdentifier, out string? name) ? name : null;

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
        Uri? file)
        : base(modifiers, identifier, parameters, templateInfo, file)
    {
        Attributes = attributes.ToImmutableArray();
        Type = type;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExport)
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

    public bool IsSame(FunctionDefinition other)
    {
        if (this.Identifier.Content != other.Identifier.Content) return false;
        if (!this.Parameters.TypeEquals(other.Parameters)) return false;
        return true;
    }

    public FunctionDefinition Duplicate() => new(Attributes, Modifiers, Type, Identifier, Parameters.Duplicate(), Template, FilePath)
    {
        Block = Block,
        Context = Context,
    };
}
