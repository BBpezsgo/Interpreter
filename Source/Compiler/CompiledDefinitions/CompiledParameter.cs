using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledParameter : ParameterDefinition,
    IHaveCompiledType
{
    public new GeneralType Type { get; }
    public HashSet<CompiledParameterAccess> Getters { get; } = new();
    public HashSet<CompiledParameterAccess> Setters { get; } = new();

    public CompiledParameter(GeneralType type, ParameterDefinition definition) : base(definition)
    {
        Type = type;
    }

    public override string ToString() => $"{(IsRef ? "ref " : string.Empty)}{Type} {Identifier}";
}
