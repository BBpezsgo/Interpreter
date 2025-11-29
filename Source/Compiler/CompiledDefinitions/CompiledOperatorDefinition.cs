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
}
