namespace LanguageCore.Compiler;

public class CompiledSizeof : CompiledExpression
{
    public required GeneralType Of { get; init; }

    public override string Stringify(int depth = 0) => $"sizeof({Of})";
    public override string ToString() => $"sizeof({Of})";
}
