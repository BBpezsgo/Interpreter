namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledConstructor : ConstructorDefinition,
    ISameCheck<CompiledConstructor>,
    IReferenceable<ConstructorCall>,
    IDuplicatable<CompiledConstructor>,
    IHaveCompiledType,
    IInContext<CompiledStruct>,
    ITemplateable<CompiledConstructor>,
    IHaveInstructionOffset
{
    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct Context { get; }
    public int InstructionOffset { get; set; } = -1;
    public List<Reference<ConstructorCall>> References { get; }

    public CompiledConstructor(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct context, ConstructorDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();
        this.Context = context;
        this.References = new List<Reference<ConstructorCall>>();
    }

    public CompiledConstructor(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledConstructor other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();
        this.Context = other.Context;
        this.References = new List<Reference<ConstructorCall>>(other.References);
    }

    public bool IsSame(CompiledConstructor other)
    {
        if (!Type.Equals(other.Type)) return false;
        if (!GeneralType.AreEquals(ParameterTypes, other.ParameterTypes)) return false;
        return true;
    }

    public CompiledConstructor Duplicate() => new(Type, ParameterTypes, Context, this);

    public override string ToString()
    {
        StringBuilder result = new();

        if (IsExport)
        { result.Append("export "); }
        result.Append(Type);
        result.Append(Parameters.ToString(ParameterTypes));
        result.Append(Block?.ToString() ?? ";");

        return result.ToString();
    }

    public CompiledConstructor InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledConstructor(newType, newParameters, this);
    }

    public static string ToReadable(GeneralType identifier, IEnumerable<GeneralType> parameters)
    {
        StringBuilder result = new();
        result.Append(identifier.ToString());
        result.Append('(');
        result.AppendJoin(", ", parameters);
        result.Append(')');
        return result.ToString();
    }

    public static string ToReadable(TypeInstance identifier, IEnumerable<GeneralType> parameters)
    {
        StringBuilder result = new();
        result.Append(identifier.ToString());
        result.Append('(');
        result.AppendJoin(", ", parameters);
        result.Append(')');
        return result.ToString();
    }
}
