namespace LanguageCore.Compiler;

using Parser;

public class CompiledParameterConstant : ParameterDefinition,
    IConstant
{
    public CompiledValue Value { get; }

    public new string Identifier => base.Identifier.Content;
    public bool IsExport => false;

    public CompiledParameterConstant(CompiledValue value, ParameterDefinition declaration) : base(declaration)
    {
        Value = value;
    }
}
