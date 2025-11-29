using LanguageCore.Compiler;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Parser;

public class TypeInstanceStackArray : TypeInstance, IEquatable<TypeInstanceStackArray?>
{
    public Expression? StackArraySize { get; }
    public TypeInstance StackArrayOf { get; }

    public TypeInstanceStackArray(TypeInstance stackArrayOf, Expression? sizeValue, Uri file) : base(file)
    {
        StackArrayOf = stackArrayOf;
        StackArraySize = sizeValue;
    }

    public override bool Equals(object? obj) => obj is TypeInstanceStackArray other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstanceStackArray other_ && Equals(other_);
    public bool Equals(TypeInstanceStackArray? other)
    {
        if (other is null) return false;
        if (!StackArrayOf.Equals(other.StackArrayOf)) return false;

        if ((StackArraySize is null) != (other.StackArraySize is null)) return false;

        return true;
    }

    public override int GetHashCode() => HashCode.Combine((byte)1, StackArrayOf, StackArraySize);

    public override Position Position => new(StackArrayOf, StackArraySize);

    public override void SetAnalyzedType(GeneralType type)
    {
        if (!type.Is(out ArrayType? arrayType)) return;

        StackArrayOf.SetAnalyzedType(arrayType.Of);
    }

    public override string ToString() => $"{StackArrayOf}[{StackArraySize}]";
    public override string ToString(IReadOnlyDictionary<string, GeneralType>? typeArguments) => $"{StackArrayOf.ToString(typeArguments)}[{StackArraySize}]";
}
