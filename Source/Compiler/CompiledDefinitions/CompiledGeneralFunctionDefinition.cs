using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledGeneralFunctionDefinition : GeneralFunctionDefinition,
    IDefinition<CompiledGeneralFunctionDefinition>,
    IReferenceable<Expression?>,
    IHaveCompiledType,
    IInContext<CompiledStruct>,
    ITemplateable<CompiledGeneralFunctionDefinition>,
    IHaveInstructionOffset,
    ICompiledFunctionDefinition
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;
    public bool IsMsilCompatible { get; set; } = true;

    public GeneralType Type { get; }
    public new ImmutableArray<CompiledParameter> Parameters { get; }
    public new CompiledStruct Context { get; }
    public List<Reference<Expression?>> References { get; }

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);

    public CompiledGeneralFunctionDefinition(GeneralType type, ImmutableArray<CompiledParameter> parameters, CompiledStruct context, GeneralFunctionDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        Parameters = parameters;
        Context = context;
        References = new List<Reference<Expression?>>();
    }

    public CompiledGeneralFunctionDefinition(GeneralType type, ImmutableArray<CompiledParameter> parameters, CompiledGeneralFunctionDefinition other) : base(other)
    {
        Type = type;
        Parameters = parameters;
        Context = other.Context;
        References = new List<Reference<Expression?>>(other.References);
    }

    public bool DefinitionEquals(CompiledGeneralFunctionDefinition other)
    {
        if (!Type.Equals(other.Type)) return false;
        if (Identifier.Content != other.Identifier.Content) return false;
        if (Parameters.Length != other.Parameters.Length) return false;
        for (int i = 0; i < Parameters.Length; i++)
        { if (!Parameters[i].Type.Equals(other.Parameters[i].Type)) return false; }

        return true;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (IsExported)
        { result.Append("export "); }

        result.Append(Identifier.Content);

        result.Append('(');
        if (Parameters.Length > 0)
        {
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0) result.Append(", ");
                if (Parameters[i].Modifiers.Length > 0)
                {
                    result.AppendJoin(' ', Parameters[i].Modifiers);
                    result.Append(' ');
                }
                result.Append(Parameters[i].Type.ToString());
            }
        }
        result.Append(')');
        result.Append(' ');

        result.Append(Block?.ToString() ?? ";");

        return result.ToString();
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

    public CompiledGeneralFunctionDefinition InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        ImmutableArray<CompiledParameter>.Builder newParameters = ImmutableArray.CreateBuilder<CompiledParameter>(Parameters.Length);
        foreach (CompiledParameter parameter in Parameters)
        {
            newParameters.Add(new CompiledParameter(GeneralType.InsertTypeParameters(parameter.Type, parameters), parameter));
        }
        return new CompiledGeneralFunctionDefinition(
            newType,
            newParameters.MoveToImmutable(),
            this
        );
    }
}
