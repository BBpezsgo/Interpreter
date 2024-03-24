namespace LanguageCore.Compiler;

using LanguageCore.Parser;
using Runtime;

public interface IConstant : IPositioned, IHaveCompiledType, IExportable
{
    public DataItem Value { get; }
    public string Identifier { get; }
    
    public new GeneralType Type => new BuiltinType(Value.Type);
    GeneralType IHaveCompiledType.Type => Type;
    GeneralType? IProbablyHaveCompiledType.Type => Type;
}
