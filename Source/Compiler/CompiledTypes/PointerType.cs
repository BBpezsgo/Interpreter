using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class PointerType : GeneralType,
    IEquatable<PointerType>
{
    public GeneralType To { get; }

    public static readonly PointerType Any = new(BuiltinType.Any);

    public PointerType(GeneralType to)
    {
        To = to;
    }

    public override bool Equals(object? other) => Equals(other as PointerType);
    public override bool Equals(GeneralType? other) => Equals(other as PointerType);
    public bool Equals(PointerType? other)
    {
        if (other is null) return false;
        if (!To.Equals(other.To)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstancePointer otherPointer) return false;
        if (!To.Equals(otherPointer.To)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(To);
    public override string ToString() => $"{To}*";
}
