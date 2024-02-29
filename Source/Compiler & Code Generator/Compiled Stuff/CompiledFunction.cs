﻿namespace LanguageCore.Compiler;

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
    ICompiledFunctionThingy
{
    public new GeneralType Type { get; }
    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public new CompiledStruct? Context { get; }
    public int InstructionOffset { get; set; } = -1;
    public bool ReturnSomething => Type != BasicType.Void;
    public List<Reference<StatementWithValue>> References { get; }
    public TypeInstance TypeToken => base.Type;

    public override bool IsTemplate
    {
        get
        {
            if (TemplateInfo != null) return true;
            if (Context != null && Context.TemplateInfo != null) return true;
            return false;
        }
    }

    [MemberNotNullWhen(true, nameof(ExternalFunctionName))]
    public bool IsExternal => Attributes.TryGetAttribute<string>("External", out _);
    public string? ExternalFunctionName
    {
        get
        {
            if (Attributes.TryGetAttribute<string>("External", out string? name))
            { return name; }
            return null;
        }
    }

    [MemberNotNullWhen(true, nameof(BuiltinFunctionName))]
    public bool IsBuiltin => Attributes.TryGetAttribute<string>("Builtin", out _);
    public string? BuiltinFunctionName
    {
        get
        {
            if (Attributes.TryGetAttribute("Builtin", out string? name))
            { return name; }
            return null;
        }
    }
    IReadOnlyList<ParameterDefinition> ICompiledFunctionThingy.Parameters => Parameters;

    public CompiledFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledStruct? context, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.Context = context;
        this.References = new List<Reference<StatementWithValue>>();
    }

    public CompiledFunction(GeneralType type, IEnumerable<GeneralType> parameterTypes, CompiledFunction other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.Context = other.Context;
        this.References = new List<Reference<StatementWithValue>>(other.References);
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
}
