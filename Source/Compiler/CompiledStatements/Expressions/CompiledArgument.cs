namespace LanguageCore.Compiler;

public class CompiledArgument : CompiledExpression
{
    public required CompiledExpression Value { get; init; }
    public required CompiledCleanup Cleanup { get; init; }

    public override string Stringify(int depth = 0) => Value.Stringify(depth + 1);
    public override string ToString() => Value.ToString();

    public static CompiledArgument Wrap(CompiledExpression value) => new()
    {
        Value = value,
        Location = value.Location,
        Cleanup = new CompiledCleanup()
        {
            Location = value.Location,
            TrashType = value.Type,
        },
        Type = value.Type,
        SaveValue = value.SaveValue,
    };
}
