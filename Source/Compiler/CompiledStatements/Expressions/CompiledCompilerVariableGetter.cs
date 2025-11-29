namespace LanguageCore.Compiler;

public class CompiledCompilerVariableAccess : CompiledExpression
{
    public required string Identifier { get; init; }

    public override string Stringify(int depth = 0) => $"@{Identifier}";
    public override string ToString() => $"@{Identifier}";
}
