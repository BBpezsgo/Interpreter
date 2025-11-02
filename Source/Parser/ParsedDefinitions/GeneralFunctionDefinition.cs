using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class GeneralFunctionDefinition : FunctionThingDefinition,
    IInContext<StructDefinition>
{
    /// <summary>
    /// Set by the <see cref="StructDefinition"/>
    /// </summary>
    [NotNull] public StructDefinition? Context { get; set; }
    public override ImmutableArray<AttributeUsage> Attributes { get; }

    public override bool IsTemplate
    {
        get
        {
            if (Template is not null) return true;
            if (Context.Template is not null) return true;
            return false;
        }
    }

    public GeneralFunctionDefinition(GeneralFunctionDefinition other) : base(other)
    {
        Context = other.Context;
        Attributes = other.Attributes;
    }

    public GeneralFunctionDefinition(
        Token identifier,
        ImmutableArray<Token> modifiers,
        ParameterDefinitionCollection parameters,
        Uri file)
        : base(modifiers, identifier, parameters, null, file)
    {
        Attributes = ImmutableArray<AttributeUsage>.Empty;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExported)
        { result.Append("export "); }
        result.Append(Identifier.Content);

        result.Append('(');
        if (Parameters.Count > 0)
        {
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(Parameters[i].Type);
            }
        }
        result.Append(')');

        result.Append(Block?.ToString() ?? ";");

        return result.ToString();
    }
}
