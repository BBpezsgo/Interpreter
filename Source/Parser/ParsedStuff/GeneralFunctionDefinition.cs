namespace LanguageCore.Parser;

using LanguageCore.Compiler;
using Tokenizing;

public class GeneralFunctionDefinition : FunctionThingDefinition,
    IInContext<StructDefinition>
{
    [NotNull] public StructDefinition? Context { get; set; }

    public GeneralFunctionDefinition(GeneralFunctionDefinition other) : base(other)
    {
        Context = other.Context;
    }

    public GeneralFunctionDefinition(
        Token identifier,
        IEnumerable<Token> modifiers,
        ParameterDefinitionCollection parameters)
        : base(modifiers, identifier, parameters, null)
    { }

    public GeneralFunctionDefinition Duplicate() => new(Identifier, Modifiers, Parameters.Duplicate())
    {
        Block = Block,
        FilePath = FilePath,
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
