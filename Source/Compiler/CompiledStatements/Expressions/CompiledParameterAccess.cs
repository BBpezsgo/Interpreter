namespace LanguageCore.Compiler;

public class CompiledParameterAccess : CompiledAccessExpression
{
    public required CompiledParameter Parameter { get; init; }

    public override string Stringify(int depth = 0) => $"{Parameter.Identifier}";
    public override string ToString() => $"{Parameter.Identifier}";
}
