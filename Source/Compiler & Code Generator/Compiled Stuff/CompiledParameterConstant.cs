using LanguageCore.Parser;
using LanguageCore.Runtime;

namespace LanguageCore.BBCode.Compiler
{
    public class CompiledParameterConstant : CompiledConstant
    {
        public readonly ParameterDefinition Declaration;
        public override string Identifier => Declaration.Identifier.Content;
        public override string? FilePath => null;

        public CompiledParameterConstant(ParameterDefinition declaration, DataItem value) : base(value)
        {
            Declaration = declaration;
        }

        public override Position Position => Declaration.Position;
    }
}
