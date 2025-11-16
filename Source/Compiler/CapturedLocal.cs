namespace LanguageCore.Compiler;

public class CapturedLocal
{
    public required bool ByRef { get; init; }
    public required CompiledVariableDeclaration? Variable { get; init; }
    public required CompiledParameter? Parameter { get; init; }
}
