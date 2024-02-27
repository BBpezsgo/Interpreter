using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace LanguageCore.Parser;

using Tokenizing;

public class FunctionDefinition : FunctionThingDefinition
{
    public readonly ImmutableArray<AttributeUsage> Attributes;
    public readonly TypeInstance Type;

    public override Position Position => base.Position.Union(Type);

    public FunctionDefinition(FunctionDefinition other) : base(other)
    {
        Attributes = other.Attributes;
        Type = other.Type;
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

    public AttributeUsage? GetAttribute(string identifier)
    {
        for (int i = 0; i < Attributes.Length; i++)
        {
            if (Attributes[i].Identifier.Content == identifier)
            { return Attributes[i]; }
        }
        return null;
    }

    public FunctionDefinition Duplicate() => new(Attributes, Modifiers, Type, Identifier, Parameters.Duplicate(), TemplateInfo)
    {
        Block = Block,
        FilePath = FilePath,
    };
}
