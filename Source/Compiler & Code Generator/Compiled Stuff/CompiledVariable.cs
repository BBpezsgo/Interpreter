namespace LanguageCore.Compiler
{
    using Parser.Statement;

    public class CompiledVariable : VariableDeclaration
    {
        public new readonly CompiledType Type;

        public readonly int MemoryAddress;
        public bool IsInitialized;

        public CompiledVariable(int memoryOffset, CompiledType type, VariableDeclaration declaration) : base(declaration)
        {
            base.CompiledType = type;

            this.Type = type;

            this.MemoryAddress = memoryOffset;
            this.IsInitialized = false;
        }
    }
}
