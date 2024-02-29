﻿using System.Collections;

namespace LanguageCore.Parser;

using Tokenizing;

public class ParameterDefinitionCollection :
    IPositioned,
    IReadOnlyList<ParameterDefinition>,
    IDuplicatable<ParameterDefinitionCollection>,
    Compiler.IInContext<FunctionThingDefinition>
{
    public Token LeftParenthesis { get; }
    public Token RightParenthesis { get; }
    public int Count => _parameters.Length;
    [NotNull] public FunctionThingDefinition? Context { get; set; }
    public Position Position =>
        new Position(LeftParenthesis, RightParenthesis)
        .Union(_parameters);

    public ParameterDefinition this[int index] => _parameters[index];
    public ParameterDefinition this[Index index] => _parameters[index];
    public ImmutableArray<ParameterDefinition> this[System.Range index] => _parameters[index];

    readonly ImmutableArray<ParameterDefinition> _parameters;

    public ParameterDefinitionCollection(ParameterDefinitionCollection other)
    {
        this._parameters = other._parameters;
        this.LeftParenthesis = other.LeftParenthesis;
        this.RightParenthesis = other.RightParenthesis;
        this.Context = other.Context;
    }

    public ParameterDefinitionCollection(IEnumerable<ParameterDefinition> parameterDefinitions, Token leftParenthesis, Token rightParenthesis)
    {
        this._parameters = parameterDefinitions.ToImmutableArray();
        this.LeftParenthesis = leftParenthesis;
        this.RightParenthesis = rightParenthesis;
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
        => new(parameterDefinitions, Token.CreateAnonymous("(", TokenType.Operator), Token.CreateAnonymous(")", TokenType.Operator));

    public ParameterDefinitionCollection Duplicate() => new(this);
}
