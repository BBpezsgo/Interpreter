namespace LanguageCore.Compiler;

using Parser;

public class CompiledField : FieldDefinition,
    IHaveCompiledType,
    IInContext<CompiledStruct>
{
    public new GeneralType Type { get; }
    public new CompiledStruct Context { get; set; }

    public Protection Protection => ProtectionToken?.Content switch
    {
        ProtectionKeywords.Private => Protection.Private,
        null => Protection.Public,
        _ => Protection.Public,
    };

    public CompiledField(GeneralType type, CompiledStruct context, FieldDefinition definition) : base(definition)
    {
        Type = type;
        Context = context;
    }
}
