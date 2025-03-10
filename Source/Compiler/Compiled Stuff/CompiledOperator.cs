﻿using LanguageCore.Parser;
using LanguageCore.Parser.Statement;

namespace LanguageCore.Compiler;

public class CompiledOperator : FunctionDefinition,
    IDefinition<CompiledOperator>,
    IReferenceable<StatementWithValue>,
    IHaveCompiledType,
    IInContext<CompiledStruct?>,
    ITemplateable<CompiledOperator>,
    ICompiledFunction,
    IHaveInstructionOffset,
    IExternalFunctionDefinition
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;

    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct? Context { get; }
    public List<Reference<StatementWithValue>> References { get; }

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);
    IReadOnlyList<ParameterDefinition> ICompiledFunction.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunction.ParameterTypes => ParameterTypes;

    public CompiledOperator(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = context;
        References = new List<Reference<StatementWithValue>>();
    }

    public CompiledOperator(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledOperator other) : base(other)
    {
        Type = type;
        ParameterTypes = parameterTypes.ToImmutableArray();
        Context = other.Context;
        References = new List<Reference<StatementWithValue>>(other.References);
    }

    public bool DefinitionEquals(CompiledOperator other) => Extensions.IsSame(this, other);

    public CompiledOperator InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledOperator(newType, newParameters, this);
    }
}
