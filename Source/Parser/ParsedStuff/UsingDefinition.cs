using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class UsingDefinition : IPositioned
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public string? CompiledUri { get; set; }

    public ImmutableArray<Token> Path { get; }
    public Token Keyword { get; }

    public string PathString
    {
        get
        {
            StringBuilder result = new();
            for (int i = 0; i < Path.Length; i++)
            {
                if (i > 0) result.Append('.');
                result.Append(Path[i].Content);
            }
            return result.ToString();
        }
    }
    public Position Position =>
        new Position(Path.Or(Keyword))
        .Union(Keyword);

    public UsingDefinition(Token keyword, IEnumerable<Token> path)
    {
        Path = path.ToImmutableArray();
        Keyword = keyword;
    }

    public override string ToString() => $"{Keyword} {string.Join('.', Path.Select(token => token.ToOriginalString()))};";
}
