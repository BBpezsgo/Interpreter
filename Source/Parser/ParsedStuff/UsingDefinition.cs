namespace LanguageCore.Parser;

using Tokenizing;

public class UsingDefinition : IPositioned
{
    public ImmutableArray<Token> Path { get; }
    public Token Keyword { get; }
    /// <summary> Set by the Compiler </summary>
    public string? CompiledUri { get; set; }
    /// <summary> Set by the Compiler </summary>
    public double? DownloadTime { get; set; }

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
        new Position(Path)
        .Union(Keyword);

    public UsingDefinition(Token keyword, IEnumerable<Token> path)
    {
        Path = path.ToImmutableArray();
        Keyword = keyword;
    }

    public static UsingDefinition CreateAnonymous(params string[] path)
    {
        Token[] pathTokens = new Token[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            pathTokens[i] = Token.CreateAnonymous(path[i]);
        }
        return new UsingDefinition(Token.CreateAnonymous(DeclarationKeywords.Using), pathTokens);
    }

    public static UsingDefinition CreateAnonymous(Uri uri) => new(
        Token.CreateAnonymous(DeclarationKeywords.Using), new Token[]
        {
            Token.CreateAnonymous(uri.ToString(), TokenType.LiteralString)
        });

    public override string ToString() => $"{Keyword} {string.Join('.', Path.Select(token => token.ToOriginalString()))};";
}
