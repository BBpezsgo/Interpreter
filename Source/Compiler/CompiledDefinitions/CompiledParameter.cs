using LanguageCore.Parser;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class CompiledParameter : ParameterDefinition,
    IHaveCompiledType
{
    public new GeneralType Type { get; }
    public HashSet<CompiledParameterGetter> Getters { get; } = new();
    public HashSet<CompiledParameterSetter> Setters { get; } = new();

    public CompiledParameter(GeneralType type, ParameterDefinition definition) : base(definition)
    {
        Type = type;
    }

    public override string ToString() => $"{(IsRef ? "ref " : string.Empty)}{Type} {Identifier}";
}
