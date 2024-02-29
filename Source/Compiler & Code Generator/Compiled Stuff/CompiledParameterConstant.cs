namespace LanguageCore.Compiler;

using Parser;
using Runtime;

public class CompiledParameterConstant : ParameterDefinition,
    IConstant
{
    public DataItem Value { get; }
    string IConstant.Identifier => base.Identifier.Content;
    public Uri? FilePath { get; set; }

    public CompiledParameterConstant(DataItem value, ParameterDefinition declaration) : base(declaration)
    {
        Value = value;
    }
}
