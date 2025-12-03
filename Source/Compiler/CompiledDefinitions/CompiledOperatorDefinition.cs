using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledOperatorDefinition : FunctionDefinition,
    IDefinition<CompiledOperatorDefinition>,
    IReferenceable<Expression>,
    IHaveCompiledType,
    IInContext<CompiledStruct?>,
    ITemplateable<CompiledOperatorDefinition>,
    ICompiledFunctionDefinition,
    IHaveInstructionOffset,
    IExternalFunctionDefinition
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;
    public bool IsMsilCompatible { get; set; } = true;

    public new GeneralType Type { get; }
    public new ImmutableArray<CompiledParameter> Parameters { get; }
    public new CompiledStruct? Context { get; }
    public List<Reference<Expression>> References { get; }

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);

    public CompiledOperatorDefinition(GeneralType type, ImmutableArray<CompiledParameter> parameters, CompiledStruct? context, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        Parameters = parameters;
        Context = context;
        References = new List<Reference<Expression>>();
    }

    public CompiledOperatorDefinition(GeneralType type, ImmutableArray<CompiledParameter> parameters, CompiledOperatorDefinition other) : base(other)
    {
        Type = type;
        Parameters = parameters;
        Context = other.Context;
        References = new List<Reference<Expression>>(other.References);
    }

    public bool DefinitionEquals(CompiledOperatorDefinition other) => Extensions.IsSame(this, other);

    public CompiledOperatorDefinition InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        ImmutableArray<CompiledParameter>.Builder newParameters = ImmutableArray.CreateBuilder<CompiledParameter>(Parameters.Length);
        foreach (CompiledParameter parameter in Parameters)
        {
            newParameters.Add(new CompiledParameter(GeneralType.InsertTypeParameters(parameter.Type, parameters), parameter));
        }
        return new CompiledOperatorDefinition(
            newType,
            newParameters.MoveToImmutable(),
            this
        );
    }

    public override string ToReadable(IReadOnlyDictionary<string, GeneralType>? typeArguments = null)
    {
        StringBuilder result = new();
        result.Append((GeneralType.InsertTypeParameters(Type, typeArguments) ?? Type).ToString());
        result.Append(' ');
        result.Append(Identifier.ToString());
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append((GeneralType.InsertTypeParameters(Parameters[i].Type, typeArguments) ?? Parameters[i].Type).ToString());
        }
        result.Append(')');
        return result.ToString();
    }
}
