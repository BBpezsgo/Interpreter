using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledParameterConstant : ParameterDefinition,
    IConstant
{
    public CompiledValue Value { get; }
    public new GeneralType Type { get; }

    public new string Identifier => base.Identifier.Content;
    public bool IsExported => false;

    public CompiledParameterConstant(CompiledValue value, GeneralType type, ParameterDefinition declaration) : base(declaration)
    {
        Value = value;
        Type = type;
    }
}
