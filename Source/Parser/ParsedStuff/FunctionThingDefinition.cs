namespace LanguageCore.Parser;

using Compiler;
using Tokenizing;

public abstract class FunctionThingDefinition :
    IExportable,
    IPositioned,
    ISimpleReadable,
    IInFile
{
    public ImmutableArray<Token> Modifiers { get; }
    public Token Identifier { get; }
    public ParameterDefinitionCollection Parameters { get; set; }
    public Statement.Block? Block { get; init; }
    public TemplateInfo? TemplateInfo { get; }
    public Uri? FilePath { get; set; }

    /// <summary>
    /// The first parameter is labeled as 'this'
    /// </summary>
    public bool IsMethod => (Parameters.Count > 0) && Parameters[0].Modifiers.Contains(ModifierKeywords.This);
    public int ParameterCount => Parameters.Count;
    public bool IsExport => Modifiers.Contains(ProtectionKeywords.Export);
    public bool IsMacro => Modifiers.Contains("macro");
    public bool IsInlineable => Modifiers.Contains(ModifierKeywords.Inline);
    public virtual bool IsTemplate => TemplateInfo is not null;

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
        parameters.Context = this;
        foreach (ParameterDefinition parameter in parameters) parameter.Context = this;

        Modifiers = modifiers.ToImmutableArray();
        Identifier = identifier;
        Parameters = parameters;
        TemplateInfo = templateInfo;
    }

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
                result.AppendJoin(' ', Parameters[j].Modifiers);
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

    public string ToReadable(IReadOnlyDictionary<string, GeneralType>? typeArguments, ToReadableFlags flags = ToReadableFlags.None)
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
                result.AppendJoin(' ', Parameters[j].Modifiers);
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
