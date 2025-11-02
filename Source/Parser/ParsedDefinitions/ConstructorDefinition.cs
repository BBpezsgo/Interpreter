using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class ConstructorDefinition : FunctionThingDefinition,
    ISimpleReadable,
    IInContext<StructDefinition?>,
    IIdentifiable<TypeInstance>
{
    /// <summary>
    /// Set by the <see cref="StructDefinition"/>
    /// </summary>
    [NotNull] public StructDefinition? Context { get; set; }

    public TypeInstance Type { get; }
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
    TypeInstance IIdentifiable<TypeInstance>.Identifier => Type;

    public ConstructorDefinition(ConstructorDefinition other) : base(other)
    {
        Type = other.Type;
        Context = other.Context;
        Attributes = other.Attributes;
    }

    public ConstructorDefinition(
        TypeInstance type,
        ImmutableArray<Token> modifiers,
        ParameterDefinitionCollection parameters,
        Uri file)
        : base(modifiers, Token.CreateAnonymous(type.ToString(), TokenType.Identifier, type.Position), parameters, null, file)
    {
        Type = type;
        Attributes = ImmutableArray<AttributeUsage>.Empty;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExported)
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
