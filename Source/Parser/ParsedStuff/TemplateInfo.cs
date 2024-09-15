using LanguageCore.Parser.Statement;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class TemplateInfo : IPositioned
{
    public Token Keyword { get; }
    public TokenPair Brackets { get; }
    public ImmutableArray<Token> Parameters { get; }

    public Position Position =>
        new Position(Parameters.As<IPositioned>().Or(Brackets))
        .Union(Keyword, Brackets);

    public TemplateInfo(Token keyword, TokenPair brackets, IEnumerable<Token> typeParameters)
    {
        Keyword = keyword;
        Brackets = brackets;
        Parameters = typeParameters.ToImmutableArray();
    }

    public override string ToString() => $"{Keyword} {Brackets.Start}{string.Join(", ", Parameters)}{Brackets.End}";
}
