namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class GeneralFunctionDefinition : FunctionThingDefinition,
    IInContext<StructDefinition>
{
    /// <summary>
    /// Set by the <see cref="StructDefinition"/>
    /// </summary>
    [NotNull] public StructDefinition? Context { get; set; }

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
    }

    public GeneralFunctionDefinition(
        Token identifier,
        IEnumerable<Token> modifiers,
        ParameterDefinitionCollection parameters,
        Uri file)
        : base(modifiers, identifier, parameters, null, file)
    { }

    public GeneralFunctionDefinition Duplicate() => new(Identifier, Modifiers, Parameters.Duplicate(), File)
    {
        Block = Block,
        Context = Context,
    };

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExport)
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
