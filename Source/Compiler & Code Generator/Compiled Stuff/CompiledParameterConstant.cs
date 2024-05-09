namespace LanguageCore.Compiler;

using Parser;
using Runtime;

public class CompiledParameterConstant : ParameterDefinition,
    IConstant
{
    public DataItem Value { get; }
    public Uri? FilePath { get; }

    public new string Identifier => base.Identifier.Content;
    public bool IsExport => false;

    public CompiledParameterConstant(DataItem value, ParameterDefinition declaration) : base(declaration)
    {
        Value = value;
    }
}
