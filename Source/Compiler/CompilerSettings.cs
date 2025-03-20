using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public struct CompilerSettings
{
    public required string? BasePath { get; set; }
    public required bool DontOptimize { get; set; }
    public required ImmutableArray<IExternalFunction> ExternalFunctions { get; set; }
    public required BuiltinType ExitCodeType { get; set; }
    public required int PointerSize { get; set; }
    public required BuiltinType BooleanType { get; set; }
    public required BuiltinType SizeofStatementType { get; set; }
    public required BuiltinType ArrayLengthType { get; set; }

    [SetsRequiredMembers]
    public CompilerSettings(CompilerSettings other)
    {
        BasePath = other.BasePath;
        DontOptimize = other.DontOptimize;
        ExternalFunctions = other.ExternalFunctions;
        ExitCodeType = other.ExitCodeType;
        PointerSize = other.PointerSize;
        BooleanType = other.BooleanType;
        SizeofStatementType = other.SizeofStatementType;
        ArrayLengthType = other.ArrayLengthType;
    }
}
