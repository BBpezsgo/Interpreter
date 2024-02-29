namespace LanguageCore.Compiler;

using Runtime;

public interface IConstant : IPositioned, IHaveCompiledType
{
    public DataItem Value { get; }
    public string Identifier { get; }
    public Uri? FilePath { get; }
    public new GeneralType Type => new BuiltinType(Value.Type);
    GeneralType IHaveCompiledType.Type => Type;
    GeneralType? IProbablyHaveCompiledType.Type => Type;
}
