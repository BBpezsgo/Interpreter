using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LanguageCore.Parser;

using Tokenizing;

public class TemplateInfo : IPositioned
{
    public Token Keyword;
    public Token LeftP;
    public ImmutableArray<Token> TypeParameters;
    public Token RightP;

    public string[] TypeParameterNames => TypeParameters.Select(v => v.Content).ToArray();

    public Position Position =>
        new Position(TypeParameters)
        .Union(Keyword, LeftP, RightP);

    public TemplateInfo(TemplateInfo other)
    {
        Keyword = other.Keyword;
        LeftP = other.LeftP;
        TypeParameters = other.TypeParameters;
        RightP = other.RightP;
    }

    public TemplateInfo(Token keyword, Token leftP, IEnumerable<Token> typeParameters, Token rightP)
    {
        Keyword = keyword;
        LeftP = leftP;
        TypeParameters = typeParameters.ToImmutableArray();
        RightP = rightP;
    }

    public Dictionary<string, Token> ToDictionary() => TypeParameters.ToDictionary(v => v.Content);

    public Dictionary<string, T> ToDictionary<T>(T[] typeArgumentValues)
    {
        Dictionary<string, T> result = new();

        if (TypeParameters.Length != typeArgumentValues.Length)
        { throw new NotImplementedException(); }

        for (int i = 0; i < TypeParameters.Length; i++)
        {
            result.Add(TypeParameters[i].Content, typeArgumentValues[i]);
        }
        return result;
    }

    public Dictionary<string, T> ToDictionary<T>(ImmutableArray<T> typeArgumentValues)
    {
        Dictionary<string, T> result = new();

        if (TypeParameters.Length != typeArgumentValues.Length)
        { throw new NotImplementedException(); }

        for (int i = 0; i < TypeParameters.Length; i++)
        {
            result.Add(TypeParameters[i].Content, typeArgumentValues[i]);
        }
        return result;
    }
}
