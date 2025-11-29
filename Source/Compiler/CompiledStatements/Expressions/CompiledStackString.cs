namespace LanguageCore.Compiler;

public class CompiledStackString : CompiledExpression
{
    public required string Value { get; init; }
    public required bool IsNullTerminated { get; init; }
    public required bool IsASCII { get; init; }

    public int Length => IsNullTerminated ? Value.Length + 1 : Value.Length;

    public override string Stringify(int depth = 0) => $"\"{Value}\"";
    public override string ToString() => $"\"{Value}\"";
}
