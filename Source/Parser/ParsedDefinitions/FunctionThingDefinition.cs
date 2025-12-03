using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public abstract class FunctionThingDefinition :
    IExportable,
    IPositioned,
    ISimpleReadable,
    IIdentifiable<Token>,
    ILocated,
    IHaveAttributes,
    ICallableDefinition
{
    public ImmutableArray<Token> Modifiers { get; }
    public Token Identifier { get; }
    public ParameterDefinitionCollection Parameters { get; }
    public Statements.Block? Block { get; init; }
    public TemplateInfo? Template { get; }
    public Uri File { get; }
    public abstract ImmutableArray<AttributeUsage> Attributes { get; }

    public Location Location => new(Position, File);
    /// <summary>
    /// The first parameter is labeled as "this"
    /// </summary>
    public bool IsExtension => (Parameters.Count > 0) && Parameters[0].Modifiers.Contains(ModifierKeywords.This);
    public int ParameterCount => Parameters.Count;
    public bool IsExported => Modifiers.Contains(ProtectionKeywords.Export);
    public bool IsInlineable => Modifiers.Contains(ModifierKeywords.Inline);
    public virtual bool IsTemplate => Template is not null;

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
        Template = other.Template;
        File = other.File;
    }

    protected FunctionThingDefinition(
        ImmutableArray<Token> modifiers,
        Token identifier,
        ParameterDefinitionCollection parameters,
        TemplateInfo? template,
        Uri file)
    {
        parameters.Context = this;
        foreach (ParameterDefinition parameter in parameters.Parameters) parameter.Context = this;

        Modifiers = modifiers;
        Identifier = identifier;
        Parameters = parameters;
        Template = template;
        File = file;
    }

    string ISimpleReadable.ToReadable() => ToReadable();
    public abstract string ToReadable(IReadOnlyDictionary<string, GeneralType>? typeArguments = null);
}
