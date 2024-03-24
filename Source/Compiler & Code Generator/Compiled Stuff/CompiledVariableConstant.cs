namespace LanguageCore.Compiler;

using Parser.Statement;
using Runtime;

public class CompiledVariableConstant : VariableDeclaration, IConstant, IHaveCompiledType
{
    public DataItem Value { get; }

    public new string Identifier => base.Identifier.Content;
    public new GeneralType Type => new BuiltinType(Value.Type);

    public CompiledVariableConstant(DataItem value, VariableDeclaration declaration) : base(declaration)
    {
        Value = value;
    }
}
