namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledGeneralFunction : GeneralFunctionDefinition,
    ISameCheck,
    ISameCheck<CompiledGeneralFunction>,
    IReferenceable<Statement?>,
    IDuplicatable<CompiledGeneralFunction>,
    IHaveCompiledType,
    IInContext<CompiledStruct>,
    ITemplateable<CompiledGeneralFunction>,
    IHaveInstructionOffset,
    ICompiledFunction
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;

    public GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct Context { get; }
    public List<Reference<Statement?>> References { get; }

    public bool ReturnSomething => Type != BasicType.Void;
    IReadOnlyList<ParameterDefinition> ICompiledFunction.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunction.ParameterTypes => ParameterTypes;

    public CompiledGeneralFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct context, GeneralFunctionDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();
        this.Context = context;
        this.References = new List<Reference<Statement?>>();
    }

    public CompiledGeneralFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledGeneralFunction other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();
        this.Context = other.Context;
        this.References = new List<Reference<Statement?>>(other.References);
    }

    public bool IsSame(CompiledGeneralFunction other)
    {
        if (this.Type != other.Type) return false;
        if (this.Identifier.Content != other.Identifier.Content) return false;
        if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
        for (int i = 0; i < this.ParameterTypes.Length; i++)
        { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

        return true;
    }
    public bool IsSame(ISameCheck? other) => other is CompiledGeneralFunction other2 && IsSame(other2);

    public new CompiledGeneralFunction Duplicate() => new(Type, ParameterTypes, Context, this);

    public override string ToString()
    {
        StringBuilder result = new();

        if (IsExport)
        { result.Append("export "); }

        result.Append(this.Identifier.Content);

        result.Append('(');
        if (this.ParameterTypes.Length > 0)
        {
            for (int i = 0; i < ParameterTypes.Length; i++)
            {
                if (i > 0) result.Append(", ");
                if (Parameters[i].Modifiers.Length > 0)
                {
                    result.AppendJoin(' ', Parameters[i].Modifiers);
                    result.Append(' ');
                }
                result.Append(ParameterTypes[i].ToString());
            }
        }
        result.Append(')');
        result.Append(' ');

        result.Append(Block?.ToString() ?? ";");

        return result.ToString();
    }

    public CompiledGeneralFunction InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledGeneralFunction(newType, newParameters, this);
    }
}
