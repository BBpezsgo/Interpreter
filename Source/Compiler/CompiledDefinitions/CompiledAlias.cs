using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledAlias : AliasDefinition,
    IReferenceable<TypeInstance>
{
    public new GeneralType Value { get; }
    public List<Reference<TypeInstance>> References { get; }

    public CompiledAlias(GeneralType value, AliasDefinition aliasDefinition) : base(aliasDefinition)
    {
        Value = value;
        References = new List<Reference<TypeInstance>>();
    }
}
