using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

[Flags]
public enum OptimizationSettings : uint
{
    None = 0,
    All = uint.MaxValue,
    FunctionEvaluating = 0x1,
    StatementEvaluating = 0x2,
    FunctionInlining = 0x4,
    TrimUnreachable = 0x8,
}

public readonly struct ExpressionVariable
{
    public readonly string Name;
    public readonly int Address;
    public readonly GeneralType Type;

    public ExpressionVariable(string name, int address, GeneralType type)
    {
        Name = name;
        Address = address;
        Type = type;
    }

    public override string ToString() => $"{Type} {Name} ({Address})";
}

public struct CompilerSettings
{
    public required OptimizationSettings Optimizations { get; set; }
    public required ImmutableArray<IExternalFunction> ExternalFunctions { get; set; }
    public required ImmutableArray<ExternalConstant> ExternalConstants { get; set; }
    public required BuiltinType ExitCodeType { get; set; }
    public required int PointerSize { get; set; }
    public required BuiltinType BooleanType { get; set; }
    public required BuiltinType SizeofStatementType { get; set; }
    public required BuiltinType ArrayLengthType { get; set; }
    public required ImmutableArray<ISourceProvider> SourceProviders { get; set; }

    public Tokenizing.TokenizerSettings? TokenizerSettings { get; set; }
    public ImmutableHashSet<string> PreprocessorVariables { get; set; }
    public ImmutableArray<string> AdditionalImports { get; set; }
    public ImmutableArray<UserDefinedAttribute> UserDefinedAttributes { get; set; }
    public bool CompileEverything { get; set; }
    public bool IsExpression { get; set; }
    public ImmutableArray<ExpressionVariable> ExpressionVariables { get; set; }
    public Dictionary<Uri, CacheItem>? Cache { get; set; }

    [SetsRequiredMembers]
    public CompilerSettings(CompilerSettings other)
    {
        Optimizations = other.Optimizations;
        ExternalFunctions = other.ExternalFunctions;
        ExternalConstants = other.ExternalConstants;
        ExitCodeType = other.ExitCodeType;
        PointerSize = other.PointerSize;
        BooleanType = other.BooleanType;
        SizeofStatementType = other.SizeofStatementType;
        ArrayLengthType = other.ArrayLengthType;
        PreprocessorVariables = other.PreprocessorVariables;
        AdditionalImports = other.AdditionalImports;
        UserDefinedAttributes = other.UserDefinedAttributes;
        SourceProviders = other.SourceProviders;
        TokenizerSettings = other.TokenizerSettings;
        CompileEverything = other.CompileEverything;
        IsExpression = other.IsExpression;
        ExpressionVariables = other.ExpressionVariables;
        Cache = other.Cache;
    }
}
