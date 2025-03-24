using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledFunctionDefinition : FunctionDefinition,
    IDefinition<CompiledFunctionDefinition>,
    IReferenceable<StatementWithValue?>,
    IHaveCompiledType,
    IInContext<CompiledStruct?>,
    ITemplateable<CompiledFunctionDefinition>,
    ICompiledFunctionDefinition,
    IHaveInstructionOffset,
    IExternalFunctionDefinition,
    IExposeable
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;

    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct? Context { get; }
    public List<Reference<StatementWithValue?>> References { get; }

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);
    public TypeInstance TypeToken => base.Type;
    IReadOnlyList<ParameterDefinition> ICompiledFunctionDefinition.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunctionDefinition.ParameterTypes => ParameterTypes;

    public CompiledFunctionDefinition(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();

        Context = context;
        References = new List<Reference<StatementWithValue?>>();
    }

    public CompiledFunctionDefinition(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledFunctionDefinition other) : base(other)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();

        Context = other.Context;
        References = new List<Reference<StatementWithValue?>>(other.References);
    }

    public bool DefinitionEquals(CompiledFunctionDefinition other)
    {
        if (!Type.Equals(other.Type)) return false;
        if (Identifier.Content != other.Identifier.Content) return false;
        if (ParameterTypes.Length != other.ParameterTypes.Length) return false;
        for (int i = 0; i < ParameterTypes.Length; i++)
        { if (!ParameterTypes[i].Equals(other.ParameterTypes[i])) return false; }

        return true;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExported)
        { result.Append("export "); }

        result.Append(Type.ToString());
        result.Append(' ');

        result.Append(Identifier.Content);

        result.Append('(');
        if (ParameterTypes.Length > 0)
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

    public CompiledFunctionDefinition InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledFunctionDefinition(newType, newParameters, this);
    }

    public static string ToReadable(string identifier, IEnumerable<string?>? parameters, string? returnType)
    {
        StringBuilder result = new();
        if (returnType is not null)
        {
            result.Append(returnType);
            result.Append(' ');
        }
        result.Append(identifier);
        result.Append('(');
        if (parameters is null)
        { result.Append("..."); }
        else
        { result.AppendJoin(", ", parameters.Select(v => v ?? "?")); }
        result.Append(')');
        return result.ToString();
    }
}
