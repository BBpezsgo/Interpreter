namespace LanguageCore.Compiler;

using Parser;

public class CompiledField : FieldDefinition
{
    public new readonly CompiledType Type;
    public readonly CompiledStruct? Context;

    public Protection Protection => ProtectionToken?.Content switch
    {
        "private" => Protection.Private,
        "public" => Protection.Public,
        null => Protection.Public,
        _ => Protection.Public,
    };

    public CompiledField(CompiledType type, CompiledStruct? context, FieldDefinition definition) : base(definition)
    {
        Type = type;
        Context = context;
    }
}
