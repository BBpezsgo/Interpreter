namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledFunction : FunctionDefinition,
    IDefinition<CompiledFunction>,
    IReferenceable<StatementWithValue?>,
    IDuplicatable<CompiledFunction>,
    IHaveCompiledType,
    IInContext<CompiledStruct?>,
    ITemplateable<CompiledFunction>,
    ICompiledFunction,
    IHaveInstructionOffset
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;

    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct? Context { get; }
    public List<Reference<StatementWithValue?>> References { get; }

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);
    public TypeInstance TypeToken => base.Type;
    IReadOnlyList<ParameterDefinition> ICompiledFunction.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunction.ParameterTypes => ParameterTypes;

    public CompiledFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();

        Context = context;
        References = new List<Reference<StatementWithValue?>>();
    }

    public CompiledFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledFunction other) : base(other)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();

        Context = other.Context;
        References = new List<Reference<StatementWithValue?>>(other.References);
    }

    public bool DefinitionEquals(CompiledFunction other)
    {
        if (!Type.Equals(other.Type)) return false;
        if (Identifier.Content != other.Identifier.Content) return false;
        if (ParameterTypes.Length != other.ParameterTypes.Length) return false;
        for (int i = 0; i < ParameterTypes.Length; i++)
        { if (!ParameterTypes[i].Equals(other.ParameterTypes[i])) return false; }

        return true;
    }

    public new CompiledFunction Duplicate() => new(Type, ParameterTypes, Context, this);

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

    public CompiledFunction InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledFunction(newType, newParameters, this);
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
