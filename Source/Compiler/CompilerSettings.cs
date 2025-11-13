using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

[Flags]
public enum OptimizationSettings : uint
{
    None = 0,
    All = uint.MaxValue,
    FunctionEvaluating = 1,
    StatementEvaluating = 2,
    FunctionInlining = 4,
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
    }
}
