﻿namespace LanguageCore.Compiler;

using Parser;

public class CompiledField : FieldDefinition,
    IHaveCompiledType,
    IInContext<CompiledStruct>
{
    public new GeneralType Type { get; }
    public CompiledStruct Context { get; set; }

    public Protection Protection => ProtectionToken?.Content switch
    {
        "private" => Protection.Private,
        "public" => Protection.Public,
        null => Protection.Public,
        _ => Protection.Public,
    };

    public CompiledField(GeneralType type, CompiledStruct context, FieldDefinition definition) : base(definition)
    {
        Type = type;
        Context = context;
    }
}
