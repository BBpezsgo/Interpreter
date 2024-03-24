namespace LanguageCore.Compiler;

using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class CompiledParameter : ParameterDefinition,
    IHaveCompiledType
{
    public new GeneralType Type { get; }
    public int Index { get; }
    public int MemoryAddress { get; }

    public bool IsAnonymous => Index == -1;

    public CompiledParameter(int index, int memoryAddress, GeneralType type, ParameterDefinition definition) : base(definition)
    {
        Type = type;
        Index = index;
        MemoryAddress = memoryAddress;
    }

    public CompiledParameter(GeneralType type, ParameterDefinition definition)
        : this(-1, -1, type, definition)
    { }

    public override string ToString() => $"{(IsRef ? "ref " : string.Empty)}{Type} {Identifier} {{ Index: {Index} RealIndex: {MemoryAddress} }}";
}
