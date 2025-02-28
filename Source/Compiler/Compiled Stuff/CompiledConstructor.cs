using LanguageCore.Parser;
using LanguageCore.Parser.Statement;

namespace LanguageCore.Compiler;

public class CompiledConstructor : ConstructorDefinition,
    IDefinition<CompiledConstructor>,
    IReferenceable<ConstructorCall>,
    IHaveCompiledType,
    IInContext<CompiledStruct>,
    ITemplateable<CompiledConstructor>,
    IHaveInstructionOffset,
    ICompiledFunction,
    IIdentifiable<GeneralType>,
    IExternalFunctionDefinition
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;

    public bool ReturnSomething => true;
    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct Context { get; }
    public List<Reference<ConstructorCall>> References { get; }

    IReadOnlyList<ParameterDefinition> ICompiledFunction.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunction.ParameterTypes => ParameterTypes;
    GeneralType IIdentifiable<GeneralType>.Identifier => Type;

    bool IExternalFunctionDefinition.IsExternal => false;
    string? IExternalFunctionDefinition.ExternalFunctionName => null;

    public CompiledConstructor(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct context, ConstructorDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = context;
        References = new List<Reference<ConstructorCall>>();
    }

    public CompiledConstructor(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledConstructor other) : base(other)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = other.Context;
        References = new List<Reference<ConstructorCall>>(other.References);
    }

    public bool DefinitionEquals(CompiledConstructor other)
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
