namespace LanguageCore.Compiler;

public class CompiledConstantValue : CompiledExpression
{
    public required CompiledValue Value { get; init; }

    public override string Stringify(int depth = 0)
    {
        return Value.Type switch
        {
            RuntimeType.U8 => Value.U8.ToString(),
            RuntimeType.I8 => Value.I8.ToString(),
            RuntimeType.U16 => $"'{((char)Value.U16).Escape()}'",
            RuntimeType.I16 => Value.I16.ToString(),
            RuntimeType.U32 => Value.U32.ToString(),
            RuntimeType.I32 => Value.I32.ToString(),
            RuntimeType.F32 => Value.F32.ToString(),
            RuntimeType.Null => "null",
            _ => Value.ToString(),

        };
    }

    public override string ToString()
    {
        return Value.Type switch
        {
            RuntimeType.U8 => Value.U8.ToString(),
            RuntimeType.I8 => Value.I8.ToString(),
            RuntimeType.U16 => $"'{((char)Value.U16).Escape()}'",
            RuntimeType.I16 => Value.I16.ToString(),
            RuntimeType.U32 => Value.U32.ToString(),
            RuntimeType.I32 => Value.I32.ToString(),
            RuntimeType.F32 => Value.F32.ToString(),
            RuntimeType.Null => "null",
            _ => Value.ToString(),

        };
    }

    public static CompiledConstantValue Create(CompiledValue value, CompiledExpression statement) => new()
    {
        Value = value,
        Location = statement.Location,
        SaveValue = statement.SaveValue,
        Type = statement.Type,
    };
}
