﻿using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledField : FieldDefinition,
    IHaveCompiledType,
    IInContext<CompiledStruct>,
    IReferenceable<Statement>
{
    public new CompiledStruct Context { get; set; }

    public new GeneralType Type { get; }
    public List<Reference<Statement>> References { get; }

    public CompiledField(GeneralType type, CompiledStruct context, FieldDefinition definition) : base(definition)
    {
        Type = type;
        Context = context;
        References = new List<Reference<Statement>>();
    }
}
