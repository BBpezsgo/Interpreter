using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledField : FieldDefinition,
    IHaveCompiledType,
    IInContext<CompiledStruct>
{
    public new CompiledStruct Context { get; set; }
    public new GeneralType Type { get; }

    public HashSet<CompiledFieldGetter> Getters { get; } = new();
    public HashSet<CompiledFieldSetter> Setters { get; } = new();

    public CompiledField(GeneralType type, CompiledStruct context, FieldDefinition definition) : base(definition)
    {
        Type = type;
        Context = context;
    }
}
