namespace LanguageCore.Parser;

using Compiler;
using Statement;

public class ParameterDefinitionCollection :
    IPositioned,
    IReadOnlyList<ParameterDefinition>,
    IDuplicatable<ParameterDefinitionCollection>,
    IInContext<FunctionThingDefinition>,
    IInFile
{
    /// <summary>
    /// Set by the <see cref="FunctionThingDefinition"/>
    /// </summary>
    [NotNull] public FunctionThingDefinition? Context { get; set; }

    public TokenPair Brackets { get; }

    public int Count => Parameters.Length;
    public Position Position =>
        new Position(Brackets)
        .Union(Parameters);
    public Uri File => Context?.File ?? throw new NullReferenceException($"{nameof(Context.File)} is null");

    public ParameterDefinition this[int index] => Parameters[index];
    public ParameterDefinition this[Index index] => Parameters[index];

    readonly ImmutableArray<ParameterDefinition> Parameters;

    public ParameterDefinitionCollection(ParameterDefinitionCollection other)
    {
        Parameters = other.Parameters;
        Brackets = other.Brackets;
        Context = other.Context;
    }

    public ParameterDefinitionCollection(IEnumerable<ParameterDefinition> parameterDefinitions, TokenPair brackets)
    {
        Parameters = parameterDefinitions.ToImmutableArray();
        Brackets = brackets;
    }

    public IEnumerator<ParameterDefinition> GetEnumerator() => ((IEnumerable<ParameterDefinition>)Parameters).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Parameters).GetEnumerator();

    public bool TypeEquals(ParameterDefinitionCollection? other)
    {
        if (other is null) return false;
        if (Parameters.Length != other.Parameters.Length) return false;
        for (int i = 0; i < Parameters.Length; i++)
        { if (!Parameters[i].Type.Equals(other.Parameters[i].Type)) return false; }
        return true;
    }

    public ParameterDefinition[] ToArray() => Parameters.ToArray();

    public static ParameterDefinitionCollection CreateAnonymous(IEnumerable<ParameterDefinition> parameterDefinitions)
        => new(parameterDefinitions, TokenPair.CreateAnonymous(new Position(parameterDefinitions), "(", ")"));

    public ParameterDefinitionCollection Duplicate() => new(this);

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Brackets.Start);

        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            if (Parameters[i].Modifiers.Length > 0)
            {
                result.AppendJoin(", ", Parameters[i].Modifiers);
                result.Append(' ');
            }
            result.Append(Parameters[i].Type);
        }

        result.Append(Brackets.End);

        return result.ToString();
    }

    public string ToString(ImmutableArray<GeneralType> types)
    {
        StringBuilder result = new();

        result.Append(Brackets.Start);

        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            if (Parameters[i].Modifiers.Length > 0)
            {
                result.AppendJoin(", ", Parameters[i].Modifiers);
                result.Append(' ');
            }
            result.Append(types[i].ToString());
        }

        result.Append(Brackets.End);

        return result.ToString();
    }

    public static implicit operator ImmutableArray<ParameterDefinition>(ParameterDefinitionCollection parameters) => parameters.Parameters;
}
