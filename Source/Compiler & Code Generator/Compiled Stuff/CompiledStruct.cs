namespace LanguageCore.Compiler;

using Parser;

public class CompiledStruct : StructDefinition,
    IReferenceable<TypeInstance>,
    IDuplicatable<CompiledStruct>
{
    public new ImmutableArray<CompiledField> Fields { get; }
    public List<Reference<TypeInstance>> References { get; }

    public CompiledStruct(IEnumerable<CompiledField> fields, StructDefinition definition) : base(definition)
    {
        this.Fields = fields.ToImmutableArray();
        foreach (CompiledField field in fields) field.Context = this;

        this.References = new List<Reference<TypeInstance>>();
    }

    public CompiledStruct(IEnumerable<CompiledField> fields, CompiledStruct other) : base(other)
    {
        this.Fields = fields.ToImmutableArray();
        foreach (CompiledField field in fields) field.Context = this;

        this.References = new List<Reference<TypeInstance>>(other.References);
    }

    public bool GetField(string name, [NotNullWhen(true)] out CompiledField? compiledField)
    {
        compiledField = null;

        foreach (CompiledField _field in Fields)
        {
            if (_field.Identifier.Content != name) continue;

            if (compiledField is not null)
            { return false; }

            compiledField = _field;
        }

        return compiledField is not null;
    }

    public CompiledStruct Duplicate() => new(Fields, this);

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

        result.Append(this.Identifier.Content);

        if (Template is null)
        { return result.ToString(); }

        result.Append('<');
        result.AppendJoin(", ", this.Template.Parameters);
        result.Append('>');
        return result.ToString();
    }
}
