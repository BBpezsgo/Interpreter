using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public interface IConstant :
    IPositioned,
    IHaveCompiledType,
    IExportable,
    IIdentifiable<string>
{
    public CompiledValue Value { get; }
}
