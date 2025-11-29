namespace LanguageCore.Compiler;

public class CompiledString : CompiledExpression
{
    public required string Value { get; init; }
    public required bool IsASCII { get; init; }
    public required CompiledExpression Allocator { get; init; }

    public override string Stringify(int depth = 0) => $"\"{Value}\"";
    public override string ToString() => $"\"{Value}\"";
}
