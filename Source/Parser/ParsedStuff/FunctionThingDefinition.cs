using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace LanguageCore.Parser;

using Tokenizing;

public abstract class FunctionThingDefinition :
    IExportable,
    IPositioned,
    ISimpleReadable,
    IInFile
{
    public ImmutableArray<Token> Modifiers;
    public readonly Token Identifier;
    public ParameterDefinitionCollection Parameters;
    public Statement.Block? Block;

    public readonly TemplateInfo? TemplateInfo;

    /// <summary>
    /// The first parameter is labeled as 'this'
    /// </summary>
    public bool IsMethod => (Parameters.Count > 0) && Parameters[0].Modifiers.Contains("this");

    public int ParameterCount => Parameters.Count;

    public bool IsExport => Modifiers.Contains("export");

    public bool IsMacro => Modifiers.Contains("macro");

    public bool IsInlineable => Modifiers.Contains("inline");

    public virtual bool IsTemplate => TemplateInfo is not null;

    public Uri? FilePath { get; set; }

    public virtual Position Position =>
        new Position(Identifier)
        .Union(Parameters.Position)
        .Union(Block)
        .Union(Modifiers);

    protected FunctionThingDefinition(FunctionThingDefinition other)
    {
        Modifiers = other.Modifiers;
        Identifier = other.Identifier;
        Parameters = other.Parameters;
        Block = other.Block;
        TemplateInfo = other.TemplateInfo;
        FilePath = other.FilePath;
    }

    protected FunctionThingDefinition(
        IEnumerable<Token> modifiers,
        Token identifier,
        ParameterDefinitionCollection parameters,
        TemplateInfo? templateInfo)
    {
        Modifiers = modifiers.ToImmutableArray();
        Identifier = identifier;
        Parameters = parameters;
        TemplateInfo = templateInfo;
    }

    public bool CanUse(Uri? sourceFile) => IsExport || sourceFile == null || sourceFile == FilePath;

    string ISimpleReadable.ToReadable() => ToReadable();
    public string ToReadable(ToReadableFlags flags = ToReadableFlags.None)
    {
        StringBuilder result = new();
        result.Append(Identifier.ToString());
        result.Append('(');
        for (int j = 0; j < Parameters.Count; j++)
        {
            if (j > 0) result.Append(", ");
            if (flags.HasFlag(ToReadableFlags.Modifiers) && Parameters[j].Modifiers.Length > 0)
            {
                result.Append(string.Join<Token>(' ', Parameters[j].Modifiers));
                result.Append(' ');
            }

            result.Append(Parameters[j].Type.ToString());

            if (flags.HasFlag(ToReadableFlags.ParameterIdentifiers))
            {
                result.Append(' ');
                result.Append(Parameters[j].Identifier.ToString());
            }
        }
        result.Append(')');
        return result.ToString();
    }

    public string ToReadable(TypeArguments? typeArguments, ToReadableFlags flags = ToReadableFlags.None)
    {
        if (typeArguments == null) return ToReadable(flags);
        StringBuilder result = new();
        result.Append(Identifier.ToString());

        result.Append('(');
        for (int j = 0; j < Parameters.Count; j++)
        {
            if (j > 0) { result.Append(", "); }
            if (flags.HasFlag(ToReadableFlags.Modifiers) && Parameters[j].Modifiers.Length > 0)
            {
                result.Append(string.Join<Token>(' ', Parameters[j].Modifiers));
                result.Append(' ');
            }

            result.Append(Parameters[j].Type.ToString(typeArguments));

            if (flags.HasFlag(ToReadableFlags.ParameterIdentifiers))
            {
                result.Append(' ');
                result.Append(Parameters[j].Identifier.ToString());
            }
        }
        result.Append(')');
        return result.ToString();
    }
}
