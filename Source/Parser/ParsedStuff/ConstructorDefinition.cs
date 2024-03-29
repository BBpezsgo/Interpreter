namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public class ConstructorDefinition : FunctionThingDefinition,
    ISimpleReadable,
    IInContext<StructDefinition?>,
    IIdentifiable<TypeInstance>
{
    public TypeInstance Type { get; }
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
    TypeInstance IIdentifiable<TypeInstance>.Identifier => Type;

    public ConstructorDefinition(ConstructorDefinition other) : base(other)
    {
        Type = other.Type;
        Context = other.Context;
    }

    public ConstructorDefinition(
        TypeInstance type,
        IEnumerable<Token> modifiers,
        ParameterDefinitionCollection parameters)
        : base(modifiers, new Token(TokenType.Identifier, type.ToString(), true, type.Position), parameters, null)
    {
        Type = type;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExport)
        { result.Append("export "); }
        result.Append(Type.ToString());
        result.Append(Parameters.ToString());
        result.Append(Block?.ToString() ?? ";");
        return result.ToString();
    }

    string ISimpleReadable.ToReadable() => ToReadable();
    public new string ToReadable(ToReadableFlags flags = ToReadableFlags.None)
    {
        StringBuilder result = new();
        result.Append(Type.ToString());
        result.Append('(');
        for (int j = 0; j < Parameters.Count; j++)
        {
            if (j > 0) result.Append(", ");
            if (flags.HasFlag(ToReadableFlags.Modifiers) && Parameters[j].Modifiers.Length > 0)
            {
                result.AppendJoin(' ', Parameters[j].Modifiers);
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
        result.Append(Type.ToString(typeArguments));

        result.Append('(');
        for (int j = 0; j < Parameters.Count; j++)
        {
            if (j > 0) { result.Append(", "); }
            if (flags.HasFlag(ToReadableFlags.Modifiers) && Parameters[j].Modifiers.Length > 0)
            {
                result.AppendJoin(' ', Parameters[j].Modifiers);
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
