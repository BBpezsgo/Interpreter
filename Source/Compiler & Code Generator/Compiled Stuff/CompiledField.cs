namespace LanguageCore.Compiler;

using System.Collections.Generic;
using Parser;
using Parser.Statement;

public class CompiledField : FieldDefinition,
    IHaveCompiledType,
    IInContext<CompiledStruct>,
    IReferenceable<Statement>
{
    public new GeneralType Type { get; }
    public new CompiledStruct Context { get; set; }
    public List<Reference<Statement>> References { get; }

    public CompiledField(GeneralType type, CompiledStruct context, FieldDefinition definition) : base(definition)
    {
        Type = type;
        Context = context;
        References = new List<Reference<Statement>>();
    }
}
