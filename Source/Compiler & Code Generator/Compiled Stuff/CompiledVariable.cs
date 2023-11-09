using LanguageCore.Parser.Statement;

namespace LanguageCore.BBCode.Compiler
{
    public class CompiledVariable : VariableDeclaration
    {
        public readonly new CompiledType Type;

        public readonly int MemoryAddress;
        public readonly bool IsGlobal;
        public bool IsInitialized;

        public CompiledVariable(int memoryOffset, CompiledType type, bool isGlobal, VariableDeclaration declaration)
            : base(declaration.Modifiers, declaration.Type, declaration.VariableName, declaration.InitialValue)
        {
            this.Type = type;

            this.MemoryAddress = memoryOffset;
            this.IsGlobal = isGlobal;

            base.FilePath = declaration.FilePath;

            this.IsInitialized = false;
        }
    }
}
