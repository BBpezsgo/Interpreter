namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledOperator : FunctionDefinition,
    ISameCheck,
    ISameCheck<CompiledOperator>,
    IReferenceable<OperatorCall>,
    IDuplicatable<CompiledOperator>,
    IHaveCompiledType,
    IInContext<CompiledStruct?>,
    ITemplateable<CompiledOperator>
{
    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public CompiledStruct? Context { get; }
    public ImmutableDictionary<string, CompiledAttribute> CompiledAttributes { get; }
    public int InstructionOffset { get; set; } = -1;
    public bool ReturnSomething => Type != BasicType.Void;
    public List<Reference<OperatorCall>> References { get; }

    public override bool IsTemplate
    {
        get
        {
            if (TemplateInfo != null) return true;
            if (Context != null && Context.TemplateInfo != null) return true;
            return false;
        }
    }

    public bool IsExternal => CompiledAttributes.ContainsKey("External");
    public string ExternalFunctionName => CompiledAttributes.TryGetAttribute("External", out string? name) ? name : string.Empty;

    public CompiledOperator(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, IEnumerable<KeyValuePair<string, CompiledAttribute>> compiledAttributes, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.CompiledAttributes = compiledAttributes.ToImmutableDictionary();
        this.Context = context;
        this.References = new List<Reference<OperatorCall>>();
    }

    public CompiledOperator(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledOperator other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.CompiledAttributes = other.CompiledAttributes;
        this.Context = other.Context;
        this.References = new List<Reference<OperatorCall>>(other.References);
    }

    public bool IsSame(CompiledOperator other)
    {
        if (this.Type != other.Type) return false;
        if (this.Identifier.Content != other.Identifier.Content) return false;
        if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
        for (int i = 0; i < this.ParameterTypes.Length; i++)
        { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

        return true;
    }
    public bool IsSame(ISameCheck? other) => other is CompiledOperator other2 && IsSame(other2);

    public new CompiledOperator Duplicate() => new(Type, new List<GeneralType>(ParameterTypes).ToArray(), Context, CompiledAttributes, this);

    public CompiledOperator InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledOperator(newType, newParameters, this);
    }
}
