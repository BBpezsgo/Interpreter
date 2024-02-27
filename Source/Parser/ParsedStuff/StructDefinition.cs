using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LanguageCore.Parser;

using Tokenizing;

public class StructDefinition : IExportable, IInFile, IPositioned
{
    public readonly ImmutableArray<AttributeUsage> Attributes;
    public readonly Token Identifier;
    public readonly Token BracketStart;
    public readonly Token BracketEnd;
    public Uri? FilePath { get; set; }
    public readonly ImmutableArray<FieldDefinition> Fields;
    public ImmutableArray<Token> Modifiers;
    public TemplateInfo? TemplateInfo;

    public IReadOnlyList<FunctionDefinition> Methods => methods;
    public IReadOnlyList<GeneralFunctionDefinition> GeneralMethods => generalMethods;
    public IReadOnlyList<FunctionDefinition> Operators => operators;
    public IReadOnlyList<ConstructorDefinition> Constructors => constructors;

    public bool IsExport => Modifiers.Contains("export");

    readonly FunctionDefinition[] methods;
    readonly GeneralFunctionDefinition[] generalMethods;
    readonly FunctionDefinition[] operators;
    readonly ConstructorDefinition[] constructors;

    public virtual Position Position => new(Identifier, BracketStart, BracketEnd);

    public StructDefinition(StructDefinition other)
    {
        Attributes = other.Attributes;
        Identifier = other.Identifier;
        BracketStart = other.BracketStart;
        BracketEnd = other.BracketEnd;
        FilePath = other.FilePath;
        Fields = other.Fields;
        Modifiers = other.Modifiers;
        TemplateInfo = other.TemplateInfo;
        methods = other.methods;
        generalMethods = other.generalMethods;
        operators = other.operators;
        constructors = other.constructors;
    }

    public StructDefinition(
        Token name,
        Token bracketStart,
        Token bracketEnd,
        IEnumerable<AttributeUsage> attributes,
        IEnumerable<Token> modifiers,
        IEnumerable<FieldDefinition> fields,
        IEnumerable<FunctionDefinition> methods,
        IEnumerable<GeneralFunctionDefinition> generalMethods,
        IEnumerable<FunctionDefinition> operators,
        IEnumerable<ConstructorDefinition> constructors)
    {
        this.Identifier = name;
        this.BracketStart = bracketStart;
        this.BracketEnd = bracketEnd;
        this.Fields = fields.ToImmutableArray();
        this.methods = methods.ToArray();
        this.generalMethods = generalMethods.ToArray();
        this.Attributes = attributes.ToImmutableArray();
        this.operators = operators.ToArray();
        this.constructors = constructors.ToArray();
        this.Modifiers = modifiers.ToImmutableArray();
    }

    public override string ToString() => $"struct {Identifier.Content}";

    public bool CanUse(Uri sourceFile) => IsExport || sourceFile == FilePath;
}
