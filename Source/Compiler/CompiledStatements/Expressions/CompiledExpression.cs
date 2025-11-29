namespace LanguageCore.Compiler;

public abstract class CompiledExpression : CompiledStatement
{
    public required bool SaveValue { get; set; } = true;
    public virtual required GeneralType Type { get; init; }
}
