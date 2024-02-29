namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledFunction : FunctionDefinition,
    ISameCheck,
    ISameCheck<CompiledFunction>,
    IReferenceable<StatementWithValue>,
    IDuplicatable<CompiledFunction>,
    IHaveCompiledType,
    IInContext<CompiledStruct?>,
    ITemplateable<CompiledFunction>
{
    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public CompiledStruct? Context { get; }
    public ImmutableDictionary<string, CompiledAttribute> CompiledAttributes { get; }
    public int InstructionOffset { get; set; } = -1;
    public bool ReturnSomething => Type != LanguageCore.Compiler.BasicType.Void;
    public List<Reference<StatementWithValue>> References { get; }
    public TypeInstance TypeToken => base.Type;

    public override bool IsTemplate
    {
        get
        {
            if (TemplateInfo != null) return true;
            if (Context != null && Context.TemplateInfo != null) return true;
            return false;
        }
    }

    [MemberNotNullWhen(true, nameof(ExternalFunctionName))]
    public bool IsExternal => CompiledAttributes.ContainsKey("External");
    public string? ExternalFunctionName
    {
        get
        {
            if (CompiledAttributes.TryGetValue("External", out CompiledAttribute? attributeValues))
            {
                if (attributeValues.TryGetValue(0, out string name))
                { return name; }
            }
            return null;
        }
    }

    [MemberNotNullWhen(true, nameof(BuiltinFunctionName))]
    public bool IsBuiltin => CompiledAttributes.ContainsKey("Builtin");
    public string? BuiltinFunctionName
    {
        get
        {
            if (CompiledAttributes.TryGetValue("Builtin", out CompiledAttribute? attributeValues))
            {
                if (attributeValues.TryGetValue(0, out string name))
                { return name; }
            }
            return null;
        }
    }

    public CompiledFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, IEnumerable<KeyValuePair<string, CompiledAttribute>> compiledAttributes, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.CompiledAttributes = compiledAttributes.ToImmutableDictionary();
        this.Context = context;
        this.References = new List<Reference<StatementWithValue>>();
    }

    public CompiledFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledFunction other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.CompiledAttributes = other.CompiledAttributes;
        this.Context = other.Context;
        this.References = new List<Reference<StatementWithValue>>(other.References);
    }

    public bool IsSame(CompiledFunction other)
    {
        if (this.Type != other.Type) return false;
        if (this.Identifier.Content != other.Identifier.Content) return false;
        if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
        for (int i = 0; i < this.ParameterTypes.Length; i++)
        { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

        return true;
    }
    public bool IsSame(ISameCheck? other) => other is CompiledFunction other2 && IsSame(other2);

    public new CompiledFunction Duplicate() => new(this.Type, new List<GeneralType>(this.ParameterTypes).ToArray(), Context, CompiledAttributes, this);

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExport)
        { result.Append("export "); }

        result.Append(this.Type.ToString());
        result.Append(' ');

        result.Append(this.Identifier.Content);

        result.Append('(');
        if (this.ParameterTypes.Length > 0)
        {
            for (int i = 0; i < ParameterTypes.Length; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(ParameterTypes[i].ToString());
            }
        }
        result.Append(')');

        if (Block != null)
        {
            result.Append(' ');
            result.Append(Block.ToString());
        }
        else
        { result.Append(';'); }

        return result.ToString();
    }

    public CompiledFunction InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledFunction(newType, newParameters, this);
    }
}
