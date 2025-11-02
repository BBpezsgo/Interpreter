using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledStruct : StructDefinition,
    IReferenceable<TypeInstance>
{
    public new ImmutableArray<CompiledField> Fields { get; private set; }
    public List<Reference<TypeInstance>> References { get; }

    public CompiledStruct(ImmutableArray<CompiledField> fields, StructDefinition definition) : base(definition)
    {
        Fields = fields;
        foreach (CompiledField field in fields) field.Context = this;

        References = new List<Reference<TypeInstance>>();
    }

    public CompiledStruct(ImmutableArray<CompiledField> fields, CompiledStruct other) : base(other)
    {
        Fields = fields;
        foreach (CompiledField field in fields) field.Context = this;

        References = new List<Reference<TypeInstance>>(other.References);
    }

    public void SetFields(ImmutableArray<CompiledField> fields)
    {
        Fields = fields;
        foreach (CompiledField field in fields) field.Context = this;
    }

    public bool TryGetTypeArgumentIndex(string typeArgumentName, out int index)
    {
        index = 0;
        if (Template is null) return false;
        for (int i = 0; i < Template.Parameters.Length; i++)
        {
            if (Template.Parameters[i].Content == typeArgumentName)
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append("struct ");

        result.Append(Identifier.Content);

        if (Template is null)
        { return result.ToString(); }

        result.Append('<');
        result.AppendJoin(", ", Template.Parameters);
        result.Append('>');
        return result.ToString();
    }
}
