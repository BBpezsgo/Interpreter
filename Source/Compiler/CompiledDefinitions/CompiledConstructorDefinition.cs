using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledConstructorDefinition : ConstructorDefinition,
    IDefinition<CompiledConstructorDefinition>,
    IReferenceable<ConstructorCall>,
    IHaveCompiledType,
    IInContext<CompiledStruct>,
    ITemplateable<CompiledConstructorDefinition>,
    IHaveInstructionOffset,
    ICompiledFunctionDefinition,
    IIdentifiable<GeneralType>,
    IExternalFunctionDefinition
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;

    public bool ReturnSomething => true;
    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct Context { get; }
    public List<Reference<ConstructorCall>> References { get; }

    IReadOnlyList<ParameterDefinition> ICompiledFunctionDefinition.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunctionDefinition.ParameterTypes => ParameterTypes;
    GeneralType IIdentifiable<GeneralType>.Identifier => Type;

    string? IExternalFunctionDefinition.ExternalFunctionName => null;

    public CompiledConstructorDefinition(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct context, ConstructorDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = context;
        References = new List<Reference<ConstructorCall>>();
    }

    public CompiledConstructorDefinition(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledConstructorDefinition other) : base(other)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = other.Context;
        References = new List<Reference<ConstructorCall>>(other.References);
    }

    public bool DefinitionEquals(CompiledConstructorDefinition other)
    {
        if (!Type.Equals(other.Type)) return false;
        if (!Utils.SequenceEquals(ParameterTypes, other.ParameterTypes)) return false;
        return true;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (IsExported)
        { result.Append("export "); }
        result.Append(Type);
        result.Append(Parameters.ToString(ParameterTypes));
        result.Append(Block?.ToString() ?? ";");

        return result.ToString();
    }

    public CompiledConstructorDefinition InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledConstructorDefinition(newType, newParameters, this);
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
