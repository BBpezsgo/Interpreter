using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace LanguageCore.Parser;

using Statement;
using Tokenizing;

public class MacroDefinition : IExportable, IInFile
{
    public readonly Token Keyword;
    public readonly ImmutableArray<Token> Modifiers;
    public readonly Token Identifier;
    public readonly ImmutableArray<Token> Parameters;
    public readonly Block Block;

    public int ParameterCount => Parameters.Length;

    public bool IsExport => Modifiers.Contains("export");

    public Uri? FilePath { get; set; }

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

    public bool CanUse(Uri sourceFile) => IsExport || sourceFile == null || sourceFile == FilePath;

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
