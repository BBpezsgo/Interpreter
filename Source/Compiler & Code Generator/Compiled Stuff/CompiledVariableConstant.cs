namespace LanguageCore.Compiler;

using Parser.Statement;

public class CompiledVariableConstant : VariableDeclaration,
    IConstant,
    IHaveCompiledType
{
    public CompiledValue Value { get; }

    public new string Identifier => base.Identifier.Content;
    public new GeneralType Type => new BuiltinType(Value.Type);

    public CompiledVariableConstant(CompiledValue value, VariableDeclaration declaration) : base(declaration)
    {
        Value = value;
    }
}
