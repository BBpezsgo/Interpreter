namespace LanguageCore.Compiler;

using Parser.Statement;

public class CompiledVariable : VariableDeclaration, IHaveCompiledType
{
    public new GeneralType Type { get; }
    public int MemoryAddress { get; }
    public bool IsInitialized { get; set; }

    public CompiledVariable(int memoryOffset, GeneralType type, VariableDeclaration declaration) : base(declaration)
    {
        base.CompiledType = type;
        this.Type = type;
        this.MemoryAddress = memoryOffset;
        this.IsInitialized = false;
    }
}
