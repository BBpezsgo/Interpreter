using LanguageCore.Parser;
using LanguageCore.Parser.Statement;

namespace LanguageCore.Compiler;

public class CompiledGeneralFunction : GeneralFunctionDefinition,
    IDefinition<CompiledGeneralFunction>,
    IReferenceable<Statement?>,
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

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);
    IReadOnlyList<ParameterDefinition> ICompiledFunction.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunction.ParameterTypes => ParameterTypes;

    public CompiledGeneralFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct context, GeneralFunctionDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = context;
        References = new List<Reference<Statement?>>();
    }

    public CompiledGeneralFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledGeneralFunction other) : base(other)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = other.Context;
        References = new List<Reference<Statement?>>(other.References);
    }

    public bool DefinitionEquals(CompiledGeneralFunction other)
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

        result.Append(Identifier.Content);

        result.Append('(');
        if (ParameterTypes.Length > 0)
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
