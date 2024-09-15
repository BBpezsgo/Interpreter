﻿using LanguageCore.Runtime;
using LanguageCore.Parser;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class FunctionType : GeneralType,
    IEquatable<FunctionType>,
    IEquatable<CompiledFunction>
{
    public GeneralType ReturnType { get; }
    public ImmutableArray<GeneralType> Parameters { get; }

    public bool ReturnSomething => !ReturnType.SameAs(BasicType.Void);

    public FunctionType(CompiledFunction function)
    {
        ReturnType = function.Type;
        Parameters = function.ParameterTypes;
    }

    public FunctionType(FunctionType other)
    {
        ReturnType = other.ReturnType;
        Parameters = other.Parameters;
    }

    public FunctionType(GeneralType returnType, IEnumerable<GeneralType> parameters)
    {
        ReturnType = returnType;
        Parameters = parameters.ToImmutableArray();
    }

    public override int GetSize(IRuntimeInfoProvider runtime)
        => runtime.PointerSize;

    public override BitWidth GetBitWidth(IRuntimeInfoProvider runtime)
        => (BitWidth)runtime.PointerSize;

    public override bool Equals(object? other) => Equals(other as FunctionType);
    public override bool Equals(GeneralType? other) => Equals(other as FunctionType);
    public bool Equals(FunctionType? other)
    {
        if (other is null) return false;
        if (!other.ReturnType.Equals(ReturnType)) return false;
        if (!Utils.SequenceEquals(Parameters, other.Parameters)) return false;
        return true;
    }
    public bool Equals(CompiledFunction? other)
    {
        if (other is null) return false;
        if (!other.Type.Equals(ReturnType)) return false;
        if (!Utils.SequenceEquals(Parameters, other.ParameterTypes)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstanceFunction otherFunction) return false;
        if (!ReturnType.Equals(otherFunction.FunctionReturnType)) return false;
        if (!Utils.SequenceEquals(Parameters, otherFunction.FunctionParameterTypes)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(ReturnType, Parameters, ReturnSomething);

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(ReturnType.ToString());
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(Parameters[i].ToString());
        }
        result.Append(')');

        return result.ToString();
    }
}
