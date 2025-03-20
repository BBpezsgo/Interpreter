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

    public required string? BasePath { get; set; }
    public IEnumerable<string>? PreprocessorVariables { get; set; }
    public FileParser? FileParser { get; set; }
    public IEnumerable<string>? AdditionalImports { get; set; }
    public IEnumerable<UserDefinedAttribute>? UserDefinedAttributes { get; set; }

    [SetsRequiredMembers]
    public CompilerSettings(CompilerSettings other)
    {
        DontOptimize = other.DontOptimize;
        ExternalFunctions = other.ExternalFunctions;
        ExitCodeType = other.ExitCodeType;
        PointerSize = other.PointerSize;
        BooleanType = other.BooleanType;
        SizeofStatementType = other.SizeofStatementType;

        BasePath = other.BasePath;
        ArrayLengthType = other.ArrayLengthType;
        PreprocessorVariables = other.PreprocessorVariables;
        FileParser = other.FileParser;
        AdditionalImports = other.AdditionalImports;
        UserDefinedAttributes = other.UserDefinedAttributes;
    }
}
