using System.Collections;

namespace LanguageCore.Parser;

using Compiler;
using LanguageCore.Parser.Statement;

public class ParameterDefinitionCollection :
    IPositioned,
    IReadOnlyList<ParameterDefinition>,
    IDuplicatable<ParameterDefinitionCollection>,
    IInContext<FunctionThingDefinition>
{
    public TokenPair Brackets { get; }
    public int Count => _parameters.Length;
    [NotNull] public FunctionThingDefinition? Context { get; set; }

    public Position Position =>
        new Position(Brackets)
        .Union(_parameters);

    public ParameterDefinition this[int index] => _parameters[index];
    public ParameterDefinition this[Index index] => _parameters[index];
    public ImmutableArray<ParameterDefinition> this[System.Range index] => _parameters[index];

    readonly ImmutableArray<ParameterDefinition> _parameters;

    public ParameterDefinitionCollection(ParameterDefinitionCollection other)
    {
        this._parameters = other._parameters;
        this.Brackets = other.Brackets;
        this.Context = other.Context;
    }

    public ParameterDefinitionCollection(IEnumerable<ParameterDefinition> parameterDefinitions, TokenPair brackets)
    {
        this._parameters = parameterDefinitions.ToImmutableArray();
        this.Brackets = brackets;
    }

    public IEnumerator<ParameterDefinition> GetEnumerator() => (_parameters as IEnumerable<ParameterDefinition>).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => (_parameters as IEnumerable).GetEnumerator();

    public bool TypeEquals(ParameterDefinitionCollection? other)
    {
        if (other is null) return false;
        if (_parameters.Length != other._parameters.Length) return false;
        for (int i = 0; i < _parameters.Length; i++)
        { if (!_parameters[i].Type.Equals(other._parameters[i].Type)) return false; }
        return true;
    }

    public ParameterDefinition[] ToArray() => _parameters.ToArray();

    public static ParameterDefinitionCollection CreateAnonymous(IEnumerable<ParameterDefinition> parameterDefinitions)
        => new(parameterDefinitions, TokenPair.CreateAnonymous(new Position(parameterDefinitions), "(", ")"));

    public ParameterDefinitionCollection Duplicate() => new(this);

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Brackets.Start);

        for (int i = 0; i < _parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            if (_parameters[i].Modifiers.Length > 0)
            {
                result.AppendJoin(", ", _parameters[i].Modifiers);
                result.Append(' ');
            }
            result.Append(_parameters[i].Type);
        }

        result.Append(Brackets.End);

        return result.ToString();
    }

    public string ToString(IEnumerable<GeneralType> types)
        => ToString(types.ToImmutableArray());
    public string ToString(ImmutableArray<GeneralType> types)
    {
        StringBuilder result = new();

        result.Append(Brackets.Start);

        for (int i = 0; i < _parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            if (_parameters[i].Modifiers.Length > 0)
            {
                result.AppendJoin(", ", _parameters[i].Modifiers);
                result.Append(' ');
            }
            result.Append(types[i].ToString());
        }

        result.Append(Brackets.End);

        return result.ToString();
    }
}
