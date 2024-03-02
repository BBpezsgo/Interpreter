namespace LanguageCore.Parser;

using Statement;
using Tokenizing;

public class MacroDefinition : IExportable, IInFile
{
    public Token Keyword { get; }
    public ImmutableArray<Token> Modifiers { get; }
    public Token Identifier { get; }
    public ImmutableArray<Token> Parameters { get; }
    public Block Block { get; }
    public Uri? FilePath { get; set; }
    public bool IsExport => Modifiers.Contains(ProtectionKeywords.Export);

    public MacroDefinition(MacroDefinition other)
    {
        Keyword = other.Keyword;
        Modifiers = other.Modifiers;
        Identifier = other.Identifier;
        Parameters = other.Parameters;
        Block = other.Block;
        FilePath = other.FilePath;
    }

    public MacroDefinition(IEnumerable<Token> modifiers, Token keyword, Token identifier, IEnumerable<Token> parameters, Block block)
    {
        Keyword = keyword;
        Identifier = identifier;
        Parameters = parameters.ToImmutableArray();
        Block = block;
        Modifiers = modifiers.ToImmutableArray();
    }

    public string ToReadable()
    {
        StringBuilder result = new();
        result.Append(Identifier.ToString());
        result.Append('(');
        for (int j = 0; j < Parameters.Length; j++)
        {
            if (j > 0) { result.Append(", "); }
            result.Append("any"); // this.Parameters[j].ToString();
        }
        result.Append(')');
        return result.ToString();
    }

    public bool IsSame(MacroDefinition other)
    {
        if (this.Identifier.Content != other.Identifier.Content) return false;
        if (this.Parameters.Length != other.Parameters.Length) return false;
        return true;
    }
}
