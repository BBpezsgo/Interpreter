using System.Diagnostics;

namespace LanguageCore.BBCode.Generator;

using Compiler;
using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class CompiledParameter : ParameterDefinition
{
    public new CompiledType Type;

    public readonly int Index;
    public readonly int MemoryAddress;

    public bool IsAnonymous => Index == -1;
    public bool IsRef => Modifiers.Contains("ref");
    public TypeInstance TypeToken => base.Type;

    public CompiledParameter(int index, int memoryAddress, CompiledType type, ParameterDefinition definition) : base(definition)
    {
        this.Index = index;
        this.Type = type;
        this.MemoryAddress = memoryAddress;
    }

    public CompiledParameter(CompiledType type, ParameterDefinition definition)
        : this(-1, -1, type, definition)
    { }

    public override string ToString() => $"{(IsRef ? "ref " : string.Empty)}{Type} {Identifier} {{ Index: {Index} RealIndex: {MemoryAddress} }}";
}
