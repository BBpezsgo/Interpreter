using LanguageCore.Compiler;
using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class TypeInstanceFunction : TypeInstance, IEquatable<TypeInstanceFunction?>
{
    public TypeInstance FunctionReturnType { get; }
    public ImmutableArray<TypeInstance> FunctionParameterTypes { get; }
    public Token? ClosureModifier { get; }
    public TokenPair Brackets { get; }

    public override Position Position =>
        new Position(FunctionReturnType)
        .Union(FunctionParameterTypes);

    public TypeInstanceFunction(TypeInstance returnType, ImmutableArray<TypeInstance> parameters, Token? closureModifier, Uri file, TokenPair brackets) : base(file)
    {
        FunctionReturnType = returnType;
        FunctionParameterTypes = parameters;
        ClosureModifier = closureModifier;
        Brackets = brackets;
    }

    public override bool Equals(object? obj) => obj is TypeInstanceFunction other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstanceFunction other_ && Equals(other_);
    public bool Equals(TypeInstanceFunction? other)
    {
        if (other is null) return false;
        if (!FunctionReturnType.Equals(other.FunctionReturnType)) return false;
        if (FunctionParameterTypes.Length != other.FunctionParameterTypes.Length) return false;
        for (int i = 0; i < FunctionParameterTypes.Length; i++)
        {
            if (!FunctionParameterTypes[i].Equals(other.FunctionParameterTypes[i]))
            { return false; }
        }
        if ((ClosureModifier is null) != (other.ClosureModifier is null)) return false;
        return true;
    }

    public override int GetHashCode() => HashCode.Combine((byte)2, FunctionReturnType, FunctionParameterTypes);

    public override string ToString() => $"{FunctionReturnType}({string.Join<TypeInstance>(", ", FunctionParameterTypes)})";
    public override string ToString(IReadOnlyDictionary<string, GeneralType>? typeArguments)
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
