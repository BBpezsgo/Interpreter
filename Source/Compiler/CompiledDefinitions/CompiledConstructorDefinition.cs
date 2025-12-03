using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledConstructorDefinition : ConstructorDefinition,
    IDefinition<CompiledConstructorDefinition>,
    IReferenceable<ConstructorCallExpression>,
    IHaveCompiledType,
    IInContext<CompiledStruct>,
    ITemplateable<CompiledConstructorDefinition>,
    IHaveInstructionOffset,
    ICompiledFunctionDefinition,
    IIdentifiable<GeneralType>,
    IExternalFunctionDefinition
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;
    public bool IsMsilCompatible { get; set; } = true;

    public bool ReturnSomething => true;
    public new GeneralType Type { get; }
    public new ImmutableArray<CompiledParameter> Parameters { get; }
    public new CompiledStruct Context { get; set; }
    public List<Reference<ConstructorCallExpression>> References { get; }

    GeneralType IIdentifiable<GeneralType>.Identifier => Type;

    string? IExternalFunctionDefinition.ExternalFunctionName => null;

    public CompiledConstructorDefinition(GeneralType type, ImmutableArray<CompiledParameter> parameters, CompiledStruct context, ConstructorDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        Parameters = parameters;
        Context = context;
        References = new List<Reference<ConstructorCallExpression>>();
    }

    public CompiledConstructorDefinition(GeneralType type, ImmutableArray<CompiledParameter> parameters, CompiledConstructorDefinition other) : base(other)
    {
        Type = type;
        Parameters = parameters;
        Context = other.Context;
        References = new List<Reference<ConstructorCallExpression>>(other.References);
    }

    public bool DefinitionEquals(CompiledConstructorDefinition other)
    {
        if (!Type.Equals(other.Type)) return false;
        if (!Utils.SequenceEquals(Parameters.Select(v => v.Type), other.Parameters.Select(v => v.Type))) return false;
        return true;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (IsExported)
        { result.Append("export "); }
        result.Append(Type);
        result.AppendJoin(", ", Parameters.Select(v => $"{v.Type} {v.Identifier}"));
        result.Append(Block?.ToString() ?? ";");

        return result.ToString();
    }

    public CompiledConstructorDefinition InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        ImmutableArray<CompiledParameter>.Builder newParameters = ImmutableArray.CreateBuilder<CompiledParameter>(Parameters.Length);
        foreach (CompiledParameter parameter in Parameters)
        {
            newParameters.Add(new CompiledParameter(GeneralType.InsertTypeParameters(parameter.Type, parameters), parameter));
        }
        return new CompiledConstructorDefinition(
            newType,
            newParameters.MoveToImmutable(),
            this
        );
    }

    public override string ToReadable(IReadOnlyDictionary<string, GeneralType>? typeArguments = null)
    {
        StringBuilder result = new();
        result.Append((GeneralType.InsertTypeParameters(Type, typeArguments) ?? Type).ToString());
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append((GeneralType.InsertTypeParameters(Parameters[i].Type, typeArguments) ?? Parameters[i].Type).ToString());
        }
        result.Append(')');
        return result.ToString();
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
