using LanguageCore.Parser;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class CompiledParameter : ParameterDefinition,
    IHaveCompiledType
{
    public new GeneralType Type { get; }
    public int Index { get; }
    public HashSet<CompiledParameterGetter> Getters { get; } = new();
    public HashSet<CompiledParameterSetter> Setters { get; } = new();

    public bool IsAnonymous => Index == -1;

    public CompiledParameter(int index, GeneralType type, ParameterDefinition definition) : base(definition)
    {
        Type = type;
        Index = index;
    }

    public override string ToString() => $"{(IsRef ? "ref " : string.Empty)}{Type} {Identifier} {{ Index: {Index} }}";
}
