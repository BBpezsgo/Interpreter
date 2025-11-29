namespace LanguageCore.Compiler;

public class CompiledCleanup : CompiledStatement
{
    public CompiledGeneralFunctionDefinition? Destructor { get; init; }
    public CompiledFunctionDefinition? Deallocator { get; init; }
    public required GeneralType TrashType { get; init; }

    public override string Stringify(int depth = 0) => "";
    public override string ToString() => "::cleanup::";
}
