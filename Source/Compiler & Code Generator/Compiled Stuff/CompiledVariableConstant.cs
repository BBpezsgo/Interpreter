using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;

namespace LanguageCore.BBCode.Compiler
{
    public class CompiledVariableConstant : CompiledConstant
    {
        public readonly VariableDeclaration Declaration;
        public override string Identifier => Declaration.VariableName.Content;
        public override string? FilePath => Declaration.FilePath;

        public CompiledVariableConstant(VariableDeclaration declaration, DataItem value) : base(value)
        {
            Declaration = declaration;
        }

        public override Position Position => Declaration.Position;
    }
}
