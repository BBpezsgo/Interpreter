using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledArrayTypeExpression : CompiledTypeExpression,
    IEquatable<CompiledArrayTypeExpression>
{
    public CompiledTypeExpression Of { get; }
    public CompiledExpression? Length { get; }
    public int? ComputedLength => Length is CompiledConstantValue evaluatedValue ? (int)evaluatedValue.Value : null;

    [SetsRequiredMembers]
    public CompiledArrayTypeExpression(CompiledTypeExpression of, CompiledExpression? length, Location location) : base(location)
    {
        Of = of;
        Length = length;
    }

    public override bool Equals(object? other) => Equals(other as CompiledArrayTypeExpression);
    public override bool Equals(CompiledTypeExpression? other) => Equals(other as CompiledArrayTypeExpression);
    public bool Equals(CompiledArrayTypeExpression? other)
    {
        if (other is null) return false;
        if (!Of.Equals(other.Of)) return false;

        if (ComputedLength.HasValue && other.ComputedLength.HasValue && ComputedLength.Value == other.ComputedLength.Value) return true;

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
    public override string ToString() => $"{Of}[{Length?.ToString()}]";
    public override string Stringify(int depth = 0) => $"{Of}[{Length?.Stringify(depth)}]";

    public static CompiledArrayTypeExpression CreateAnonymous(ArrayType type, ILocated location)
    {
        return new(
            CreateAnonymous(type.Of, location),
            type.Length.HasValue ? new CompiledConstantValue()
            {
                Value = type.Length.Value,
                Location = location.Location,
                SaveValue = true,
                Type = new BuiltinType(BasicType.I32),
            } : null,
            location.Location
        );
    }
}
