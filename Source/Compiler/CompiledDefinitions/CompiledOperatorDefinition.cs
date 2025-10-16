﻿using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public class CompiledOperatorDefinition : FunctionDefinition,
    IDefinition<CompiledOperatorDefinition>,
    IReferenceable<StatementWithValue>,
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
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct? Context { get; }
    public List<Reference<StatementWithValue>> References { get; }

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);
    IReadOnlyList<ParameterDefinition> ICompiledFunctionDefinition.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunctionDefinition.ParameterTypes => ParameterTypes;

    public CompiledOperatorDefinition(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = context;
        References = new List<Reference<StatementWithValue>>();
    }

    public CompiledOperatorDefinition(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledOperatorDefinition other) : base(other)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = other.Context;
        References = new List<Reference<StatementWithValue>>(other.References);
    }

    public bool DefinitionEquals(CompiledOperatorDefinition other) => Extensions.IsSame(this, other);

    public CompiledOperatorDefinition InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledOperatorDefinition(newType, newParameters, this);
    }
}
