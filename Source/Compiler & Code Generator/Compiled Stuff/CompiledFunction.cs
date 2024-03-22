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
    ITemplateable<CompiledFunction>,
    ICompiledFunction,
    IHaveInstructionOffset
{
    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct? Context { get; }
    public int InstructionOffset { get; set; } = -1;
    public List<Reference<StatementWithValue>> References { get; }

    public bool ReturnSomething => Type != BasicType.Void;
    public TypeInstance TypeToken => base.Type;
    IReadOnlyList<ParameterDefinition> ICompiledFunction.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunction.ParameterTypes => ParameterTypes;

    public CompiledFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();

        Context = context;
        References = new List<Reference<StatementWithValue>>();
    }

    public CompiledFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledFunction other) : base(other)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();

        Context = other.Context;
        References = new List<Reference<StatementWithValue>>(other.References);
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

    public new CompiledFunction Duplicate() => new(Type, ParameterTypes, Context, this);

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

    public static string ToReadable(string identifier, IEnumerable<GeneralType> parameters)
    {
        StringBuilder result = new();
        result.Append(identifier);
        result.Append('(');
        result.AppendJoin(", ", parameters);
        result.Append(')');
        return result.ToString();
    }

    public static string ToReadable(string identifier, FunctionType type)
    {
        StringBuilder result = new();
        result.Append(type.ReturnType);
        result.Append(' ');
        result.Append(identifier);
        result.Append('(');
        result.AppendJoin(", ", type.Parameters);
        result.Append(')');
        return result.ToString();
    }
}
