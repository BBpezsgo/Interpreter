using LanguageCore.Compiler;

namespace LanguageCore.Parser;

public class TypeInstanceFunction : TypeInstance, IEquatable<TypeInstanceFunction?>
{
    public TypeInstance FunctionReturnType { get; }
    public ImmutableArray<TypeInstance> FunctionParameterTypes { get; }
    public override Position Position =>
        new Position(FunctionReturnType)
        .Union(FunctionParameterTypes);

    public TypeInstanceFunction(TypeInstance returnType, IEnumerable<TypeInstance> parameters, Uri file) : base(file)
    {
        FunctionReturnType = returnType;
        FunctionParameterTypes = parameters.ToImmutableArray();
    }

    public override bool Equals(object? obj) => obj is TypeInstanceFunction other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstanceFunction other_ && Equals(other_);
    public bool Equals(TypeInstanceFunction? other)
    {
        if (other is null) return false;
        if (!this.FunctionReturnType.Equals(other.FunctionReturnType)) return false;
        if (this.FunctionParameterTypes.Length != other.FunctionParameterTypes.Length) return false;
        for (int i = 0; i < this.FunctionParameterTypes.Length; i++)
        {
            if (!this.FunctionParameterTypes[i].Equals(other.FunctionParameterTypes[i]))
            { return false; }
        }
        return true;
    }

    public override int GetHashCode() => HashCode.Combine((byte)2, FunctionReturnType, FunctionParameterTypes);

    public override void SetAnalyzedType(GeneralType type)
    {
        if (!type.Is(out FunctionType? functionType)) return;

        FunctionReturnType.SetAnalyzedType(functionType.ReturnType);

        if (FunctionParameterTypes.Length != functionType.Parameters.Length) return;

        for (int i = 0; i < functionType.Parameters.Length; i++)
        {
            FunctionParameterTypes[i].SetAnalyzedType(functionType.Parameters[i]);
        }
    }

    public override string ToString() => $"{FunctionReturnType}({string.Join<TypeInstance>(", ", FunctionParameterTypes)})";
    public override string ToString(IReadOnlyDictionary<string, GeneralType> typeArguments)
    {
        StringBuilder result = new();
        result.Append(FunctionReturnType.ToString(typeArguments));
        result.Append('(');
        for (int i = 0; i < FunctionParameterTypes.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(FunctionParameterTypes[i].ToString(typeArguments));
        }
        result.Append(')');
        return result.ToString();
    }
}
