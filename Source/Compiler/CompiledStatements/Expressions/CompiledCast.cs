namespace LanguageCore.Compiler;

public class CompiledCast : CompiledExpression
{
    public required CompiledExpression? Allocator { get; init; }
    public required CompiledExpression Value { get; init; }

    public override string Stringify(int depth = 0) => $"({Type}){Value.Stringify(depth + 1)}";

    public override string ToString() => $"({Type}){Value}";

    public static CompiledCast Wrap(CompiledExpression value, GeneralType type) => new()
    {
        Value = value,
        Type = type,
        Allocator = null,
        Location = value.Location,
        SaveValue = value.SaveValue,
    };
}
