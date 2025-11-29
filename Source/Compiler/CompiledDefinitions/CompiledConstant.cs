using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledVariableConstant : VariableDefinition,
    IHaveCompiledType,
    IPositioned,
    IIdentifiable<string>
{
    public CompiledValue Value { get; }
    public new GeneralType Type { get; }

    public new string Identifier => base.Identifier.Content;

    public CompiledVariableConstant(CompiledValue value, GeneralType type, VariableDefinition declaration) : base(declaration)
    {
        Value = value;
        Type = type;
    }
}
