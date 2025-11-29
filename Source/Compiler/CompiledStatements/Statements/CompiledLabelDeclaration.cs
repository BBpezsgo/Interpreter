namespace LanguageCore.Compiler;

public class CompiledLabelDeclaration : CompiledStatement
{
    public static readonly FunctionType Type = new(BuiltinType.Void, ImmutableArray<GeneralType>.Empty, false);
    public required string Identifier { get; init; }
    public HashSet<CompiledLabelReference> Getters { get; } = new();

    public override string Stringify(int depth = 0) => $"{Identifier}:";
    public override string ToString() => $"{Identifier}:";
}
