namespace LanguageCore.Compiler;

using LanguageCore.Runtime;
using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class ArrayType : GeneralType,
    IEquatable<ArrayType>
{
    public GeneralType Of { get; }
    public int? Length { get; }

    public override GeneralType FinalValue => new ArrayType(Of.FinalValue, Length);
    public override int Size => Length.HasValue ? Length.Value * Of.Size : throw new InvalidOperationException("Array type's length isn't defined");
    public override int SizeBytes => Length.HasValue ? Length.Value * Of.SizeBytes : throw new InvalidOperationException("Array type's length isn't defined");
    public override BitWidth BitWidth => throw new InvalidOperationException();

    public ArrayType(ArrayType other)
    {
        Of = other.Of;
        Length = other.Length;
    }

    public ArrayType(GeneralType of, int? size)
    {
        Of = of;
        Length = size;
    }

    public override bool Equals(object? other) => Equals(other as ArrayType);
    public override bool Equals(GeneralType? other) => Equals(other as ArrayType);
    public bool Equals(ArrayType? other)
    {
        if (other is null) return false;
        if (!Of.Equals(other.Of)) return false;
        if (!Length.Equals(other.Length)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstanceStackArray otherStackArray) return false;
        if (!Of.Equals(otherStackArray.StackArrayOf)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(Of, Length);
    public override string ToString() => $"{Of}[{Length}]";
}
