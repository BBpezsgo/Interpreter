namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class FunctionDefinition : FunctionThingDefinition,
    IHaveType,
    IInContext<StructDefinition?>
{
    public ImmutableArray<AttributeUsage> Attributes { get; }
    public TypeInstance Type { get; }
    public override Position Position => base.Position.Union(Type);
    public StructDefinition? Context { get; set; }

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
        TemplateInfo? templateInfo)
        : base(modifiers, identifier, parameters, templateInfo)
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

    public FunctionDefinition Duplicate() => new(Attributes, Modifiers, Type, Identifier, Parameters.Duplicate(), TemplateInfo)
    {
        Block = Block,
        FilePath = FilePath,
        Context = Context,
    };
}
