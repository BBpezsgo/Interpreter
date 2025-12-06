using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledFunctionTypeExpression : CompiledTypeExpression,
    IEquatable<CompiledFunctionTypeExpression>
{
    public CompiledTypeExpression ReturnType { get; }
    public ImmutableArray<CompiledTypeExpression> Parameters { get; }
    public bool HasClosure { get; }

    public bool ReturnSomething => !ReturnType.SameAs(BasicType.Void);

    [SetsRequiredMembers]
    public CompiledFunctionTypeExpression(CompiledTypeExpression returnType, ImmutableArray<CompiledTypeExpression> parameters, bool hasClosure, Location location) : base(location)
    {
        ReturnType = returnType;
        Parameters = parameters;
        HasClosure = hasClosure;
    }

    public override bool Equals(object? other) => Equals(other as CompiledFunctionTypeExpression);
    public override bool Equals(CompiledTypeExpression? other) => Equals(other as CompiledFunctionTypeExpression);
    public bool Equals(CompiledFunctionTypeExpression? other)
    {
        if (other is null) return false;
        if (!other.ReturnType.Equals(ReturnType)) return false;
        if (!Utils.SequenceEquals(Parameters, other.Parameters)) return false;
        if (HasClosure != other.HasClosure) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstanceFunction otherFunction) return false;
        if (!ReturnType.Equals(otherFunction.FunctionReturnType)) return false;
        if (!Utils.SequenceEquals(Parameters, otherFunction.FunctionParameterTypes)) return false;
        if (HasClosure != (otherFunction.ClosureModifier is not null)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(ReturnType, Parameters, ReturnSomething);

    public override string ToString()
    {
        StringBuilder result = new();
        if (HasClosure) result.Append('@');
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
    public override string Stringify(int depth = 0)
    {
        StringBuilder result = new();
        if (HasClosure) result.Append('@');
        result.Append(ReturnType.Stringify(depth));
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(Parameters[i].Stringify(depth));
        }
        result.Append(')');

        return result.ToString();
    }

    public static CompiledFunctionTypeExpression CreateAnonymous(FunctionType type, ILocated location)
    {
        return new(
            CreateAnonymous(type.ReturnType, location),
            type.Parameters.ToImmutableArray(v => CreateAnonymous(v, location)),
            type.HasClosure,
            location.Location
        );
    }
}
