namespace LanguageCore.Compiler;

using Parser;
using Runtime;

public class CompiledEnumMember : EnumMemberDefinition,
    IHaveCompiledType,
    IInContext<CompiledEnum>
{
    public DataItem ComputedValue { get; set; }
    [NotNull] public new CompiledEnum? Context { get; set; }

    public GeneralType Type => new BuiltinType(ComputedValue.Type);

    public CompiledEnumMember(EnumMemberDefinition definition) : base(definition) { }
}
