namespace LanguageCore.Compiler
{
    using Parser.Statement;
    using Runtime;

    public class CompiledVariableConstant : CompiledConstant
    {
        public readonly VariableDeclaration Declaration;

        public override string Identifier => Declaration.VariableName.Content;
        public override string? FilePath => Declaration.FilePath;
        public override Position Position => Declaration.Position;

        public CompiledVariableConstant(VariableDeclaration declaration, DataItem value) : base(value)
        {
            Declaration = declaration;
        }
    }
}
