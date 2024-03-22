namespace LanguageCore.Parser;

using Statement;
using Tokenizing;

public class TemplateInfo : IPositioned
{
    public Token Keyword { get; }
    public TokenPair Brackets { get; }
    public ImmutableArray<Token> TypeParameters { get; }

    public Position Position =>
        new Position(TypeParameters)
        .Union(Keyword, Brackets);

    public TemplateInfo(Token keyword, TokenPair brackets, IEnumerable<Token> typeParameters)
    {
        Keyword = keyword;
        Brackets = brackets;
        TypeParameters = typeParameters.ToImmutableArray();
    }

    public override string ToString() => $"{Keyword} {Brackets.Start}{string.Join(", ", TypeParameters)}{Brackets.End}";
}
