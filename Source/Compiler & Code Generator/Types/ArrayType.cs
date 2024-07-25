namespace LanguageCore.Compiler;

using Parser.Statement;
using Runtime;
using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class ArrayType : GeneralType,
    IEquatable<ArrayType>
{
    public GeneralType Of { get; }
    public StatementWithValue? Length { get; }
    public int? ComputedLength { get; }

    public override int Size => ComputedLength.HasValue ? ComputedLength.Value * Of.Size : throw new InvalidOperationException("Array type's length isn't defined");
    public override int SizeBytes => ComputedLength.HasValue ? ComputedLength.Value * Of.SizeBytes : throw new InvalidOperationException("Array type's length isn't defined");
    public override BitWidth BitWidth => throw new InvalidOperationException();

    public ArrayType(ArrayType other)
    {
        Of = other.Of;
        Length = other.Length;
    }

    public ArrayType(GeneralType of, StatementWithValue? length, int? computedLength)
    {
        Of = of;
        Length = length;
        ComputedLength = computedLength;
    }

    public override bool Equals(object? other) => Equals(other as ArrayType);
    public override bool Equals(GeneralType? other) => Equals(other as ArrayType);
    public bool Equals(ArrayType? other)
    {
        if (other is null) return false;
        if (!Of.Equals(other.Of)) return false;
        if (Length is not null)
        {
            if (other.Length is null) return false;
            if (ComputedLength.HasValue)
            {
                if (!other.ComputedLength.HasValue) return false;
                if (ComputedLength.Value != other.ComputedLength.Value) return false;
            }
            else
            {
                if (other.ComputedLength.HasValue) return false;
            }
        }
        else
        {
            if (other.Length is not null) return false;
            if (other.ComputedLength.HasValue) return false;
        }
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
