using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledParameterConstant : ParameterDefinition,
    IConstant
{
    public CompiledValue Value { get; }

    public new string Identifier => base.Identifier.Content;
    public bool IsExported => false;

    public new GeneralType Type => new BuiltinType(Value.Type);

    public CompiledParameterConstant(CompiledValue value, ParameterDefinition declaration) : base(declaration)
    {
        Value = value;
    }
}
