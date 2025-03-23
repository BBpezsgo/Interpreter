using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public struct CompilerSettings
{
    public required bool DontOptimize { get; set; }
    public required ImmutableArray<IExternalFunction> ExternalFunctions { get; set; }
    public required BuiltinType ExitCodeType { get; set; }
    public required int PointerSize { get; set; }
    public required BuiltinType BooleanType { get; set; }
    public required BuiltinType SizeofStatementType { get; set; }
    public required BuiltinType ArrayLengthType { get; set; }

    public IEnumerable<string>? PreprocessorVariables { get; set; }
    public IEnumerable<string>? AdditionalImports { get; set; }
    public IEnumerable<UserDefinedAttribute>? UserDefinedAttributes { get; set; }
    public required ImmutableArray<ISourceProvider> SourceProviders { get; set; }

    [SetsRequiredMembers]
    public CompilerSettings(CompilerSettings other)
    {
        DontOptimize = other.DontOptimize;
        ExternalFunctions = other.ExternalFunctions;
        ExitCodeType = other.ExitCodeType;
        PointerSize = other.PointerSize;
        BooleanType = other.BooleanType;
        SizeofStatementType = other.SizeofStatementType;

        ArrayLengthType = other.ArrayLengthType;
        PreprocessorVariables = other.PreprocessorVariables;
        AdditionalImports = other.AdditionalImports;
        UserDefinedAttributes = other.UserDefinedAttributes;
        SourceProviders = other.SourceProviders;
    }
}
