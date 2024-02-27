using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LanguageCore.Parser;

using Tokenizing;

public class ParameterDefinitionCollection :
    IPositioned,
    IReadOnlyCollection<ParameterDefinition>,
    IDuplicatable<ParameterDefinitionCollection>
{
    public readonly Token LeftParenthesis;
    public readonly Token RightParenthesis;
    readonly ImmutableArray<ParameterDefinition> Parameters;

    public Position Position =>
        new Position(LeftParenthesis, RightParenthesis)
        .Union(Parameters);

    public int Count => Parameters.Length;

    public ParameterDefinition this[int index] => Parameters[index];

    public ParameterDefinitionCollection(ParameterDefinitionCollection other)
    {
        this.Parameters = other.Parameters;
        this.LeftParenthesis = other.LeftParenthesis;
        this.RightParenthesis = other.RightParenthesis;
    }

    public ParameterDefinitionCollection(IEnumerable<ParameterDefinition> parameterDefinitions, Token leftParenthesis, Token rightParenthesis)
    {
        this.Parameters = parameterDefinitions.ToImmutableArray();
        this.LeftParenthesis = leftParenthesis;
        this.RightParenthesis = rightParenthesis;
    }

    public IEnumerator<ParameterDefinition> GetEnumerator() => (Parameters as IEnumerable<ParameterDefinition>).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => (Parameters as IEnumerable).GetEnumerator();

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
        => new(parameterDefinitions, Token.CreateAnonymous("(", TokenType.Operator), Token.CreateAnonymous(")", TokenType.Operator));

    public ParameterDefinitionCollection Duplicate() => new(Parameters, LeftParenthesis, RightParenthesis);
}
