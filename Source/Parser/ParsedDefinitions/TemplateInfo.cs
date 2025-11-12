using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class TemplateInfo : IPositioned
{
    public TokenPair Brackets { get; }
    public ImmutableArray<Token> Parameters { get; }

    public Position Position =>
        new Position(Parameters.As<IPositioned>().DefaultIfEmpty(Brackets))
        .Union(Brackets);

    public TemplateInfo(TokenPair brackets, ImmutableArray<Token> typeParameters)
    {
        Brackets = brackets;
        Parameters = typeParameters;
    }

    public override string ToString() => $"{Brackets.Start}{string.Join(", ", Parameters)}{Brackets.End}";
}
