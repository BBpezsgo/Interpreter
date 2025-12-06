using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class ArrayType : GeneralType,
    IEquatable<ArrayType>
{
    public GeneralType Of { get; }
    public int? Length { get; }

    public ArrayType(GeneralType of, int? length)
    {
        Of = of;
        Length = length;
    }

    public override bool Equals(object? other) => Equals(other as ArrayType);
    public override bool Equals(GeneralType? other) => Equals(other as ArrayType);
    public bool Equals(ArrayType? other)
    {
        if (other is null) return false;
        if (!Of.Equals(other.Of)) return false;

        if (Length.HasValue && other.Length.HasValue && Length.Value == other.Length.Value) return true;

        if (Length is not null)
        {
            if (other.Length is null) return false;
            if (Length.HasValue)
            {
                if (!other.Length.HasValue) return false;
                if (Length.Value != other.Length.Value) return false;
            }
            else
            {
                if (other.Length.HasValue) return false;
            }
        }
        else
        {
            if (other.Length is not null) return false;
            if (other.Length.HasValue) return false;
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
    public override string ToString() => $"{Of}[{(Length.HasValue ? Length.Value.ToString() : Length?.ToString())}]";
}
