using LanguageCore.Runtime;
using LanguageCore.Parser;
using LanguageCore.Parser.Statement;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class ArrayType : GeneralType,
    IEquatable<ArrayType>
{
    public GeneralType Of { get; }
    public StatementWithValue? Length { get; }
    public int? ComputedLength { get; }

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

    public override bool GetSize(IRuntimeInfoProvider runtime, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;

        if (!ComputedLength.HasValue)
        {
            error = new PossibleDiagnostic("Array type's length isn't defined");
            return false;
        }

        if (!Of.GetSize(runtime, out int itemSize, out error))
        {
            return false;
        }

        size = ComputedLength.Value * itemSize;
        return true;
    }

    public override bool GetBitWidth(IRuntimeInfoProvider runtime, out BitWidth bitWidth, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        bitWidth = default;
        error = new PossibleDiagnostic("Arrays cannot have a bitwidth because they are not a primitive type");
        return false;
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
