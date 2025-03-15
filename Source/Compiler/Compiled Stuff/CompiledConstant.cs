using LanguageCore.Parser.Statement;

namespace LanguageCore.Compiler;

public class CompiledVariableConstant : VariableDeclaration,
    IHaveCompiledType,
    IPositioned,
    IIdentifiable<string>
{
    public CompiledValue Value { get; }
    public new GeneralType Type { get; }

    public new string Identifier => base.Identifier.Content;

    public CompiledVariableConstant(CompiledValue value, GeneralType type, VariableDeclaration declaration) : base(declaration)
    {
        Value = value;
        Type = type;
    }
}
