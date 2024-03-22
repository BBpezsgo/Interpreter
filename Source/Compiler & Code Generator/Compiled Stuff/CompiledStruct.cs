namespace LanguageCore.Compiler;

using Parser;
using Tokenizing;

public class CompiledStruct : StructDefinition,
    IReferenceable<TypeInstance>,
    IDuplicatable<CompiledStruct>
{
    public new ImmutableArray<CompiledField> Fields { get; }
    public List<Reference<TypeInstance>> References { get; }
    public IReadOnlyDictionary<string, GeneralType> CurrentTypeArguments => _currentTypeArguments;
    public IReadOnlyDictionary<string, int> FieldOffsets
    {
        get
        {
            Dictionary<string, int> result = new();
            int currentOffset = 0;
            foreach (CompiledField field in Fields)
            {
                result.Add(field.Identifier.Content, currentOffset);
                currentOffset += GetType(field.Type, field).Size;
            }
            return result;
        }
    }
    public int Size
    {
        get
        {
            int size = 0;
            foreach (CompiledField field in Fields)
            { size += GetType(field.Type, field).Size; }
            return size;
        }
    }

    readonly Dictionary<string, GeneralType> _currentTypeArguments;

    public CompiledStruct(IEnumerable<CompiledField> fields, StructDefinition definition) : base(definition)
    {
        this.Fields = fields.ToImmutableArray();
        foreach (CompiledField field in fields) field.Context = this;

        this._currentTypeArguments = new Dictionary<string, GeneralType>();
        this.References = new List<Reference<TypeInstance>>();
    }

    public CompiledStruct(IEnumerable<CompiledField> fields, CompiledStruct other) : base(other)
    {
        this.Fields = fields.ToImmutableArray();
        foreach (CompiledField field in fields) field.Context = this;

        this._currentTypeArguments = new Dictionary<string, GeneralType>(other._currentTypeArguments);
        this.References = new List<Reference<TypeInstance>>(other.References);
    }

    public int SizeWithTypeArguments(IReadOnlyDictionary<string, GeneralType> typeParameters)
    {
        int size = 0;
        foreach (CompiledField field in Fields)
        { size += GetType(field.Type, field, typeParameters).Size; }
        return size;
    }

    public void SetTypeArguments(IEnumerable<GeneralType> typeParameters)
         => SetTypeArguments(typeParameters.ToArray());
    public void SetTypeArguments(GeneralType[] typeParameters)
    {
        _currentTypeArguments.Clear();
        AddTypeArguments(typeParameters);
    }
    public void SetTypeArguments(IReadOnlyDictionary<string, GeneralType> typeParameters)
    {
        _currentTypeArguments.Clear();
        AddTypeArguments(typeParameters);
    }

    public void AddTypeArguments(IEnumerable<GeneralType> typeParameters)
         => AddTypeArguments(typeParameters.ToArray());
    public void AddTypeArguments(GeneralType[] typeParameters)
    {
        if (TemplateInfo == null)
        { return; }

        if (typeParameters == null || typeParameters.Length == 0)
        { return; }

        string[] typeParameterNames = TemplateInfo.TypeParameters.Select(v => v.Content).ToArray();

        if (typeParameters.Length != typeParameterNames.Length)
        { throw new CompilerException("Ah", null, null); }

        for (int i = 0; i < typeParameters.Length; i++)
        {
            GeneralType value = typeParameters[i];
            string key = typeParameterNames[i];

            _currentTypeArguments[key] = GeneralType.From(value);
        }
    }
    public void AddTypeArguments(IReadOnlyDictionary<string, GeneralType> typeParameters)
    {
        if (TemplateInfo == null)
        { return; }

        string[] typeParameterNames = TemplateInfo.TypeParameters.Select(v => v.Content).ToArray();

        for (int i = 0; i < typeParameterNames.Length; i++)
        {
            if (!typeParameters.TryGetValue(typeParameterNames[i], out GeneralType? typeParameterValue))
            { continue; }
            _currentTypeArguments[typeParameterNames[i]] = GeneralType.From(typeParameterValue);
        }
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

    public void ClearTypeArguments() => _currentTypeArguments.Clear();

    GeneralType GetType(GeneralType type, IPositioned position)
    {
        if (type is not GenericType genericType) return type;
        if (!_currentTypeArguments.TryGetValue(genericType.Identifier, out GeneralType? result))
        { throw new CompilerException($"Type argument \"{genericType.Identifier}\" not found", position, FilePath); }
        return result;
    }
    GeneralType GetType(GeneralType type, IPositioned position, IReadOnlyDictionary<string, GeneralType> typeParameters)
    {
        if (type is not GenericType genericType) return type;
        if (!typeParameters.TryGetValue(genericType.Identifier, out GeneralType? result) &&
            !_currentTypeArguments.TryGetValue(genericType.Identifier, out result))
        { throw new CompilerException($"Type argument \"{genericType.Identifier}\" not found", position, FilePath); }
        return result;
    }

    public CompiledStruct Duplicate() => new(Fields, this);

    public bool TryGetTypeArgumentIndex(string typeArgumentName, out int index)
    {
        index = 0;
        if (TemplateInfo == null) return false;
        for (int i = 0; i < TemplateInfo.TypeParameters.Length; i++)
        {
            if (TemplateInfo.TypeParameters[i].Content == typeArgumentName)
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

        if (this.TemplateInfo != null)
        {
            result.Append('<');
            if (this._currentTypeArguments.Count > 0)
            {
                for (int i = 0; i < this.TemplateInfo.TypeParameters.Length; i++)
                {
                    if (i > 0) result.Append(", ");

                    string typeParameterName = this.TemplateInfo.TypeParameters[i].Content;
                    if (this._currentTypeArguments.TryGetValue(typeParameterName, out GeneralType? typeParameterValue))
                    {
                        result.Append(typeParameterValue.ToString());
                    }
                    else
                    {
                        result.Append('?');
                    }
                }
            }
            else
            {
                result.AppendJoin(", ", this.TemplateInfo.TypeParameters);
            }
            result.Append('>');
        }
        return result.ToString();
    }
}
