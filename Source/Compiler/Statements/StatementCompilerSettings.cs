using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public struct StatementCompilerSettings
{
    public bool DontOptimize { get; set; }
    public ImmutableArray<IExternalFunction> ExternalFunctions { get; set; }
    public required BuiltinType ExitCodeType { get; set; }
    public required int PointerSize { get; set; }
    public required BuiltinType BooleanType { get; set; }
    public required BuiltinType SizeofStatementType { get; set; }
    public required BuiltinType ArrayLengthType { get; set; }
}
