﻿namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledOperator : FunctionDefinition,
    ISameCheck,
    ISameCheck<CompiledOperator>,
    IReferenceable<StatementWithValue>,
    IDuplicatable<CompiledOperator>,
    IHaveCompiledType,
    IInContext<CompiledStruct?>,
    ITemplateable<CompiledOperator>,
    ICompiledFunction,
    IHaveInstructionOffset
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;

    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct? Context { get; }
    public List<Reference<StatementWithValue>> References { get; }

    public bool ReturnSomething => Type != BasicType.Void;
    IReadOnlyList<ParameterDefinition> ICompiledFunction.Parameters => Parameters;
    IReadOnlyList<GeneralType> ICompiledFunction.ParameterTypes => ParameterTypes;

    public CompiledOperator(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.Context = context;
        this.References = new List<Reference<StatementWithValue>>();
    }

    public CompiledOperator(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledOperator other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.Context = other.Context;
        this.References = new List<Reference<StatementWithValue>>(other.References);
    }

    public bool IsSame(CompiledOperator other) => Extensions.IsSame(this, other);
    public bool IsSame(ISameCheck? other) => other is CompiledOperator other2 && IsSame(other2);

    public new CompiledOperator Duplicate() => new(Type, new List<GeneralType>(ParameterTypes).ToArray(), Context, this);

    public CompiledOperator InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters)
    {
        IEnumerable<GeneralType> newParameters = GeneralType.InsertTypeParameters(ParameterTypes, parameters);
        GeneralType newType = GeneralType.InsertTypeParameters(Type, parameters) ?? Type;
        return new CompiledOperator(newType, newParameters, this);
    }
}
