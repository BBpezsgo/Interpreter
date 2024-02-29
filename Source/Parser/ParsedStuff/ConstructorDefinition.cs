namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class ConstructorDefinition : FunctionThingDefinition, ISimpleReadable
{
    public new TypeInstance Identifier { get; }

    public ConstructorDefinition(ConstructorDefinition other) : base(other)
    {
        Identifier = other.Identifier;
    }

    public ConstructorDefinition(
        TypeInstance type,
        IEnumerable<Token> modifiers,
        ParameterDefinitionCollection parameters)
        : base(modifiers, null!, parameters, null)
    {
        Identifier = type;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExport)
        { result.Append("export "); }
        result.Append(Identifier);

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

    string ISimpleReadable.ToReadable() => ToReadable();
    public new string ToReadable(ToReadableFlags flags = ToReadableFlags.None)
    {
        StringBuilder result = new();
        result.Append(Identifier.ToString());
        result.Append('(');
        for (int j = 0; j < Parameters.Count; j++)
        {
            if (j > 0) result.Append(", ");
            if (flags.HasFlag(ToReadableFlags.Modifiers) && Parameters[j].Modifiers.Length > 0)
            {
                result.Append(string.Join<Token>(' ', Parameters[j].Modifiers));
                result.Append(' ');
            }

            result.Append(Parameters[j].Type.ToString());

            if (flags.HasFlag(ToReadableFlags.ParameterIdentifiers))
            {
                result.Append(' ');
                result.Append(Parameters[j].Identifier.ToString());
            }
        }
        result.Append(')');
        return result.ToString();
    }

    public new string ToReadable(IReadOnlyDictionary<string, GeneralType>? typeArguments, ToReadableFlags flags = ToReadableFlags.None)
    {
        if (typeArguments == null) return ToReadable(flags);
        StringBuilder result = new();
        result.Append(Identifier.ToString(typeArguments));

        result.Append('(');
        for (int j = 0; j < Parameters.Count; j++)
        {
            if (j > 0) { result.Append(", "); }
            if (flags.HasFlag(ToReadableFlags.Modifiers) && Parameters[j].Modifiers.Length > 0)
            {
                result.Append(string.Join<Token>(' ', Parameters[j].Modifiers));
                result.Append(' ');
            }

            result.Append(Parameters[j].Type.ToString(typeArguments));

            if (flags.HasFlag(ToReadableFlags.ParameterIdentifiers))
            {
                result.Append(' ');
                result.Append(Parameters[j].Identifier.ToString());
            }
        }
        result.Append(')');
        return result.ToString();
    }
}
