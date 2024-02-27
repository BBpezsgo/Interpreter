using System.Collections.Generic;
using System.Text;

namespace LanguageCore.Parser;

using Tokenizing;

public class GeneralFunctionDefinition : FunctionThingDefinition
{
    public GeneralFunctionDefinition(GeneralFunctionDefinition other) : base(other)
    {

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
