namespace LanguageCore.Compiler;

using Parser.Statement;

public class CompiledVariable : VariableDeclaration, IHaveCompiledType
{
    public bool IsInitialized { get; set; }
    public new GeneralType Type { get; }
    /// <summary>
    /// This is a stack direction independent address.
    /// </summary>
    public int MemoryAddress { get; }

    public CompiledVariable(int memoryAddress, GeneralType type, VariableDeclaration declaration) : base(declaration)
    {
        CompiledType = type;
        Type = type;
        MemoryAddress = memoryAddress;
        IsInitialized = false;
    }
}
